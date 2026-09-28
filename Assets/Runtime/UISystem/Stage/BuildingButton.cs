using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 建筑类型（种族建筑表匹配用）：决定建筑按钮按所选种族解析哪种建筑变体。
/// 种族资产 RaceDefinition.buildings 的条目用同一枚举匹配。
/// </summary>
public enum BuildingType
{
    None = 0,          // 不使用种族变体（始终使用按钮默认预制体）。
    Barracks = 1,      // 普通兵营
    DarkBarracks = 2,  // 黑暗兵营
    Sentry = 3,        // 哨塔
    MainBase = 4,      // 大本营（在种族建筑表内配置，进入关卡时按该条目自动生成）
}

public class BuildingButton : UISystemBase, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private GameObject targetBuilding;                         // 按下按钮时创建并进入放置模式的建筑预制体。
    [SerializeField, Tooltip("建筑类型（与种族资产 Buildings 表匹配）；None = 始终使用默认预制体")]
    private BuildingType buildingType = BuildingType.None;

    [Header("建造花费角标")]
    [SerializeField, Tooltip("按钮右下角金币角标上的花费文本；显示本按钮当前解析建筑（种族变体优先）的 1 级建造费用")]
    private TMP_Text costText;

    /// <summary>设置本按钮创建的目标建筑预制体（测试工具种族切换使用）。</summary>
    public void SetTargetBuilding(GameObject prefab)
    {
        targetBuilding = prefab;
    }

    /// <summary>
    /// 解析本按钮本次点击要生成的建筑预制体：
    /// 优先取当前所选种族的同类型变体（种族→建筑→兵种链），未配置/未预载时回落默认预制体。
    /// </summary>
    private GameObject ResolvePrefab()
    {
        if (buildingType != BuildingType.None && UserGlobalInfo.Instance != null)
        {
            GameObject raceBuilding = TryResolveBuilding(
                UserGlobalInfo.Instance.SelectedRaceId, buildingType);
            if (raceBuilding != null)
                return raceBuilding;
        }

        return targetBuilding;
    }

    /// <summary>
    /// 按队伍种族解析建筑变体预制体：从种族定义的 buildings 表中按建筑类型取资源键，
    /// 经 ResourceManager 取得预制体；未配置/未找到时返回 null（调用方回落默认预制体）。
    /// 玩家侧传 SelectedRaceId；阶段 3 的敌方 AI 传对应队伍的种族 ID，共用同一条链。
    /// </summary>
    public static GameObject TryResolveBuilding(string raceId, BuildingType type)
    {
        if (string.IsNullOrEmpty(raceId) || type == BuildingType.None ||
            ResourceManager.Instance == null)
            return null;

        if (!ResourceManager.Instance.TryGetRace(raceId, out RaceDefinition race) ||
            race == null || race.Buildings == null)
            return null;

        for (int i = 0; i < race.Buildings.Count; i++)
        {
            RaceBuildingEntry entry = race.Buildings[i];
            if (entry == null || entry.buildingType != type)
                continue;

            if (string.IsNullOrEmpty(entry.prefabKey))
                return null;

            GameObject prefab = ResourceManager.Instance.GetGameObject(entry.prefabKey);
            if (prefab == null)
            {
                Debug.LogWarning(
                    $"[BuildingButton] 种族 {raceId} 的建筑类型 {type} 预制体未预载：{entry.prefabKey}，回落默认建筑。");
            }
            return prefab;
        }

        return null;
    }

    #region 建造花费角标
    /// <summary>角标刷新兜底节流时间点；种族建筑预载晚于 Start 完成时由周期刷新自动纠正角标。</summary>
    private float _nextBadgeRefreshAt;

    /// <summary>启动时刷新角标；资源未就绪时按当前可解析值显示，后续由周期刷新补齐。</summary>
    private void Start()
    {
        RefreshCostBadge();
    }

    /// <summary>按钮每次激活时刷新角标（面板切换可见性会再次触发本回调）。</summary>
    private void OnEnable()
    {
        RefreshCostBadge();
    }

    /// <summary>每 0.5 秒兜底刷新一次角标，覆盖种族建筑预载晚于按钮启动的场景。</summary>
    private void Update()
    {
        if (Time.unscaledTime < _nextBadgeRefreshAt)
            return;

        _nextBadgeRefreshAt = Time.unscaledTime + 0.5f;
        RefreshCostBadge();
    }

    /// <summary>按当前解析的预制体重算角标花费。</summary>
    private void RefreshCostBadge()
    {
        RefreshCostBadge(ResolvePrefab());
    }

    /// <summary>从建筑预制体读取 1 级建造费用（BuildUP.levels[0].cost）并写入角标文本。</summary>
    private void RefreshCostBadge(GameObject prefab)
    {
        if (costText == null || prefab == null)
            return;

        BuildUP buildUp = prefab.GetComponent<BuildUP>();
        int cost = buildUp != null && buildUp.levels != null && buildUp.levels.Length > 0
            ? Mathf.Max(0, buildUp.levels[0].cost)
            : 0;
        costText.text = cost.ToString();
    }
    #endregion

    #region 生命周期与回调
    /// <summary>
    /// 将按下位置转换到按钮局部坐标；转换成功后经对象池生成目标建筑，并用当前指针进入建筑放置模式。
    /// </summary>
    /// <param name="eventData">包含按下位置、指针编号和事件摄像机的数据。</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        GameObject prefab = ResolvePrefab();
        if (prefab == null || BuildingPlace.Instance == null) return;

        // 点击时按本次实际解析结果同步角标，保证角标费用与将要建造的费用一致（种族变体同源解析）。
        RefreshCostBadge(prefab);

        RectTransform rectTransform = transform as RectTransform;            // 当前建筑按钮的矩形变换。
        Vector2 localPosition;                                               // 指针相对按钮的局部坐标。

        // 参考 JoyStick 的做法，使用 ScreenPointToLocalPointInRectangle 将屏幕点转换为 UI 局部坐标
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out localPosition))
        {
            // // 在局部坐标系下判断点击点距离中心的长度是否在半径内
            // if (localPosition.magnitude <= buttonRadius)
            // {
                // 经对象池生成建筑并进入放置模式（池服务未就绪时安全失败，不实例化新对象）。
                GameObject obj = GameObjectPool.Instance != null
                    ? GameObjectPool.Instance.Get(prefab)
                    : null;
                if (obj == null) return;

                // 测试工具设定过阵营时，放置建筑沿用该阵营（产兵经 prop.side 自动跟随）。
                if (FightTest.DesiredSide >= 0)
                {
                    GameObjectProperty prop = obj.GetComponent<GameObjectProperty>();
                    if (prop != null)
                        prop.side = FightTest.DesiredSide;
                }

                BuildingBase building = obj.GetComponent<BuildingBase>();    // 建筑的放置与建造逻辑组件。

                if (building != null)
                {
                    BuildingPlace.Instance.EnterPlaceMode(building, eventData.pointerId);
                }
            // }
        }
    }

    /// <summary>
    /// 指针抬起时请求放置系统结束当前放置模式，并根据当前位置决定建造或取消。
    /// </summary>
    /// <param name="eventData">本次抬起事件的指针数据；放置系统使用此前绑定的指针状态。</param>
    public void OnPointerUp(PointerEventData eventData)
    {
        // 手指抬起，尝试在当前位置放下建筑
        if (BuildingPlace.Instance != null)
        {
            BuildingPlace.Instance.ExitPlaceMode();
        }
    }
    #endregion
}
