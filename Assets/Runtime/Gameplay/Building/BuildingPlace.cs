using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingPlace : MonoBehaviour
{
    public static BuildingPlace Instance { get; private set; }
    #region 生命周期与回调
    /// <summary>
    /// 建立建筑放置管理器单例；场景中存在重复实例时销毁后创建的对象。
    /// </summary>
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            // 重复实例：不销毁场景对象，仅停用本组件（规范禁止业务脚本 Destroy）。
            Debug.LogWarning("[BuildingPlace] 场景中存在重复实例，本组件已停用。", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        SubscribeIfReady();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }
    #endregion

    #region 玩家等级实时同步
    /// <summary>懒订阅：UserGlobalInfo 就绪后订阅等级变化（每帧重试一次，成本可忽略）。</summary>
    private void SubscribeIfReady()
    {
        if (_subscribedInfo != null)
            return;

        UserGlobalInfo info = UserGlobalInfo.Instance;
        if (info == null)
            return;

        _subscribedInfo = info;
        info.Changed += OnPlayerLevelsChanged;
    }

    private void Unsubscribe()
    {
        if (_subscribedInfo != null)
        {
            _subscribedInfo.Changed -= OnPlayerLevelsChanged;
            _subscribedInfo = null;
        }
    }

    /// <summary>
    /// 玩家在升级面板升级后（UserGlobalInfo.Changed），立即把新等级应用到局内
    /// 已放置的队伍 1 建筑——局内等级始终与面板/存档一致，不用等下一座建筑。
    /// 经 MapCells 全图枚举，只处理玩家建筑（兵种不处理：新产出的兵种自然继承新等级）。
    /// </summary>
    private void OnPlayerLevelsChanged()
    {
        MapCells map = MapCells.Instance;
        if (map == null)
            return;

        var candidates = new System.Collections.Generic.HashSet<GameObject>();
        map.CollectOccupiersInBounds(
            new Vector2Int(0, 0),
            new Vector2Int(map.width - 1, map.height - 1),
            candidates);

        foreach (GameObject obj in candidates)
        {
            if (obj == null)
                continue;

            GameObjectProperty prop = obj.GetComponent<GameObjectProperty>();
            if (prop == null || prop.side != 1 ||
                (prop.objectType & GameObjectType.Building) == 0)
                continue;

            BuildingBase building = obj.GetComponent<BuildingBase>();
            if (building != null)
                ApplyPlayerLevels(building);
        }
    }
    #endregion

    [Header("拖拽取消")]
    [SerializeField, Tooltip("右上角取消区（RectTransform）；拖拽建筑进入该区域后抬起即取消放置")]
    private RectTransform cancelZone;

    private BuildingBase currentBuilding;                                                        // 当前正在拖动预览的建筑。
    private bool isInPlaceMode = false;                                                          // 是否正在处理建筑放置输入。
    private bool cancelRequested;                                                                // 指针是否已拖入取消区。
    private static bool warnedMissingCamera;                                                     // 主相机缺失的一次性警告标记。
    private UserGlobalInfo _subscribedInfo;                                                      // 已订阅等级变化事件的玩家全局信息（懒订阅）。

    // 手指处理器
    private FingerIDHander fingerHandler = new FingerIDHander();                                 // 放置流程独占触摸输入的手指绑定器。

    #region 公开接口
    /// <summary>
    /// 判断指定建筑是否正处于拖拽放置预览阶段。
    /// 预览中的建筑只应展示放置合法性，不应执行战斗或生产行为。
    /// </summary>
    /// <param name="building">需要判断的建筑实例。</param>
    /// <returns>该建筑正在被拖拽预览时返回 <see langword="true"/>。</returns>
    public bool IsBuildingInPreview(BuildingBase building)
    {
        return isInPlaceMode && currentBuilding != null && currentBuilding == building;
    }

    /// <summary>
    /// 设置待放置建筑，重置旧触摸绑定，显示取消区，并按需立即占用创建建筑的指针。
    /// </summary>
    /// <param name="building">需要进入拖动预览的建筑实例。</param>
    /// <param name="initialFingerId">创建建筑时已经按下的指针编号；-1 表示等待新的有效触摸（编辑器鼠标）。</param>
    public void EnterPlaceMode(BuildingBase building, int initialFingerId = -1)
    {
        // 预览即注入玩家队伍与等级上下文：拖拽期间建筑朝向/队伍即正确
        // （否则预览对象沿用预制体默认或上一轮池化的阵营，偶数队会被翻转成敌方外观）。
        // 测试工具指定过阵营时跳过注入（保留 DesiredSide）。
        if (building != null && FightTest.DesiredSide < 0)
            ApplyPlayerLevels(building);

        currentBuilding = building;
        isInPlaceMode = true;
        cancelRequested = false;
        SetCancelZoneVisible(true);

        fingerHandler.Unbind();
        if (initialFingerId != -1)
        {
            fingerHandler.TryBind(initialFingerId);
        }
    }

    /// <summary>
    /// 检查建筑当前位置是否合法；合法时启动施工，拖入取消区或位置非法时回收预览对象，
    /// 最后退出放置模式、隐藏取消区并释放触摸绑定。
    /// </summary>
    /// <returns>建筑通过合法性检查并成功开始施工时返回 <see langword="true"/>。</returns>
    public bool ExitPlaceMode()
    {
        if (!isInPlaceMode || currentBuilding == null) return false;

        // 拖入取消区抬起：取消放置并回收预览建筑。
        if (cancelRequested)
        {
            if (GameObjectPool.Instance != null)
                GameObjectPool.Instance.Release(currentBuilding.gameObject);
            else
                currentBuilding.gameObject.SetActive(false);
            currentBuilding = null;
            isInPlaceMode = false;
            cancelRequested = false;
            SetCancelZoneVisible(false);
            fingerHandler.Unbind();
            return false;
        }

        // 检测当前位置是否合法
        if (currentBuilding.ChechValid())
        {
            // 黑暗兵营建造限制（局内规则）：本方大本营局内升级到 2 级才可建造，
            // 否则弹“大本营等级不足”并按取消放置回收预览建筑。
            BuildingTraining training = currentBuilding.GetComponent<BuildingTraining>();
            if (training != null && training.IsDarkBarracks &&
                !UpgradeLevelRules.CanBuildDarkBarracks(
                    currentBuilding.GetComponent<GameObjectProperty>()))
            {
                if (DamageTextPool.Instance != null)
                    DamageTextPool.Instance.ShowText("大本营等级不足", currentBuilding.transform.position, Color.red);
                CurrencyFeedbackAudio.PlayWrong();   // 内容不满足要求：Wrong UI AD。

                if (GameObjectPool.Instance != null)
                    GameObjectPool.Instance.Release(currentBuilding.gameObject);
                else
                    currentBuilding.gameObject.SetActive(false);
                currentBuilding = null;
                isInPlaceMode = false;
                SetCancelZoneVisible(false);
                fingerHandler.Unbind();
                return false;
            }

            // 建造费用 = 预制体 BuildUP 三个等级中 Element 0 的 Cost。
            // 金币不足：不建造，弹红色“金币不足”，并按取消放置回收预览建筑。
            int cost = GetBuildCost(currentBuilding);
            if (cost > 0 && Coins.Instance != null && Coins.Instance.CurrentCoins < cost)
            {
                if (DamageTextPool.Instance != null)
                    DamageTextPool.Instance.ShowCoinLack(currentBuilding.transform.position);

                if (GameObjectPool.Instance != null)
                    GameObjectPool.Instance.Release(currentBuilding.gameObject);
                else
                    currentBuilding.gameObject.SetActive(false);
                currentBuilding = null;
                isInPlaceMode = false;
                SetCancelZoneVisible(false);
                fingerHandler.Unbind();
                return false;
            }

            // 扣除建造费用（0 费用建筑跳过）。
            if (cost > 0 && Coins.Instance != null)
                Coins.Instance.ConsumeCoins(cost);

            // 正式占用地图网格
            // 完成放置
            ApplyPlayerLevels(currentBuilding);   // 注入玩家成长等级（建筑等级缩放 + Buff 等级上下文）。
            currentBuilding.StartBuild();
            currentBuilding = null;
            isInPlaceMode = false;
            SetCancelZoneVisible(false);
            fingerHandler.Unbind();
            return true;
        }

        // 位置不合法：经对象池归还预览建筑（池服务未就绪时仅停用，不直接 Destroy）。
        if (GameObjectPool.Instance != null)
            GameObjectPool.Instance.Release(currentBuilding.gameObject);
        else
            currentBuilding.gameObject.SetActive(false);
        currentBuilding = null;
        isInPlaceMode = false;
        SetCancelZoneVisible(false);
        fingerHandler.Unbind();
        return false;
    }
    #endregion

    private Vector2Int lastBasePos = new Vector2Int(-999, -999);                                 // 记录上次检测时的网格起始坐标

    #region 生命周期与回调
    /// <summary>
    /// 放置模式下绑定第一个未被 UI 占用的触摸（或编辑器鼠标），并持续将建筑中心吸附到对应地图网格。
    /// 指针拖入右上角取消区时标记取消；抬起后由 ExitPlaceMode 决定建造或取消。
    /// 仅在基准网格变化时重新检查合法性，触摸结束后释放绑定。
    /// </summary>
    private void Update()
    {
        // 懒订阅重试：UserGlobalInfo 后于本组件就绪时，在这里补上订阅（每帧一次判空，成本可忽略）。
        SubscribeIfReady();

        if (!isInPlaceMode || currentBuilding == null) return;

        // 1. 如果还没绑定手指，寻找第一个有效手指（不在 UI 上）
        if (!fingerHandler.IsOccupied)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);                                                     // 当前检查的触摸。
                if (t.phase == TouchPhase.Began)
                {
                    // 过滤 UI 点击
                    if (UnityEngine.EventSystems.EventSystem.current != null && 
                        UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(t.fingerId))
                        continue;

                    if (fingerHandler.TryBind(t.fingerId)) 
                    {
                        lastBasePos = new Vector2Int(-999, -999); // 绑定新手指时重置记录
                        break;
                    }
                }
            }
        }

        // 2. 解析当前指针屏幕坐标（触屏或编辑器鼠标；编辑器鼠标的指针编号为 -1）
        Vector2? pointer = null;
        if (fingerHandler.IsOccupied)
        {
            if (fingerHandler.BoundFingerId == -1)
            {
                if (Input.GetMouseButtonUp(0))
                    fingerHandler.Unbind();
                else if (Input.GetMouseButton(0))
                    pointer = Input.mousePosition;
            }
            else
            {
                Touch? activeTouch = fingerHandler.GetActiveTouch();                             // 当前放置流程绑定的活动触摸。
                if (activeTouch.HasValue)
                {
                    Touch touch = activeTouch.Value;                                             // 当前帧的触摸数据。
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                        fingerHandler.Unbind();
                    else
                        pointer = touch.position;
                }
            }
        }

        if (!pointer.HasValue)
            return;

        // 3. 拖入取消区标记取消（抬起时由 ExitPlaceMode 取消放置）
        cancelRequested = IsInCancelZone(pointer.Value);

        // 4. 移动预览建筑并吸附网格
        // 相机上下文缺失时中止本次坐标更新，禁止回退到原点放置（规范要求安全失败）。
        if (AudioManager.Instance == null || AudioManager.Instance.MainCamera == null)
        {
            if (!warnedMissingCamera)
            {
                warnedMissingCamera = true;
                Debug.LogWarning("[BuildingPlace] 主相机不可用，放置坐标更新已中止。", this);
            }
            return;
        }

        Vector3 worldPos = AudioManager.Instance.MainCamera.ScreenToWorldPoint(pointer.Value);
        worldPos.z = 0;

        GameObjectProperty prop = currentBuilding.GetComponent<GameObjectProperty>();    // 当前建筑的占地属性。
        Vector2Int occupySpace = prop != null ? prop.occupySpace : Vector2Int.one;       // 建筑占用的网格尺寸。

        // 计算对齐后的网格左下角起始坐标
        Vector2Int currentBasePos = new Vector2Int(
            Mathf.FloorToInt(worldPos.x - occupySpace.x / 2f),
            Mathf.FloorToInt(worldPos.y - occupySpace.y / 2f)
        );

        // 计算建筑中心点位置（用于视觉同步）
        Vector2 snappedPos = new Vector2(
            currentBasePos.x + occupySpace.x / 2f,
            currentBasePos.y + occupySpace.y / 2f
        );
        currentBuilding.transform.position = snappedPos;
        currentBuilding.RefreshOccupancy();

        // 性能优化：只有在网格坐标发生变化时，才重新检测合法性
        if (currentBasePos != lastBasePos)
        {
            currentBuilding.ChechValid();
            lastBasePos = currentBasePos;
        }
    }

    /// <summary>判断屏幕坐标是否位于取消区内；未配置取消区时始终返回 false。</summary>
    private bool IsInCancelZone(Vector2 screenPoint)
    {
        if (cancelZone == null)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(cancelZone, screenPoint);
    }

    /// <summary>建筑建造费用 = 预制体 BuildUP 三个等级中 Element 0（第一级）的 Cost。</summary>
    private static int GetBuildCost(BuildingBase building)
    {
        BuildUP buildUp = building != null ? building.GetComponent<BuildUP>() : null;
        if (buildUp != null && buildUp.levels != null && buildUp.levels.Length > 0)
            return Mathf.Max(0, buildUp.levels[0].cost);

        return 0;
    }

    /// <summary>
    /// 按玩家成长等级注入建筑等级上下文：
    /// 玩家固定为队伍 1；哨塔 → 哨塔等级，兵营 → 兵营/黑暗兵营等级；
    /// 防御/攻击魔法等级注入 Buff 等级上下文。注入后经 BuildUP 重算统计（1.1 缩放与局内升级叠加）。
    /// </summary>
    private void ApplyPlayerLevels(BuildingBase building)
    {
        GameObjectProperty prop = building.GetComponent<GameObjectProperty>();
        if (prop == null)
            return;

        UserGlobalInfo info = UserGlobalInfo.Instance;
        prop.side = 1;
        prop.defenseMagicLevel = info != null ? Mathf.Max(1, info.DefenseMagicLevel) : 1;
        prop.attackMagicLevel = info != null ? Mathf.Max(1, info.AttackMagicLevel) : 1;

        if (building.GetComponent<BuildingTowerAI>() != null)
        {
            prop.sentryTowerLevel = info != null ? Mathf.Max(1, info.SentryTowerLevel) : 1;
        }
        else
        {
            BuildingTraining training = building.GetComponent<BuildingTraining>();
            if (training != null)
            {
                int level = training.IsDarkBarracks
                    ? (info != null ? info.DarkBarracksLevel : 1)
                    : (info != null ? info.BarracksLevel : 1);
                prop.barracksLevel = Mathf.Max(1, level);
            }
        }

        BuildUP buildUp = building.GetComponent<BuildUP>();
        if (buildUp != null)
            buildUp.RefreshLevelScale();
    }

    /// <summary>切换取消区的显示状态；仅在放置模式期间可见。</summary>
    private void SetCancelZoneVisible(bool visible)
    {
        if (cancelZone != null && cancelZone.gameObject != null)
            cancelZone.gameObject.SetActive(visible);
    }
    #endregion
}
