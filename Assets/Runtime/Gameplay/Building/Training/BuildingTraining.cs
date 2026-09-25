using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 兵营/精英兵营的训练组件：点击建筑打开兵种选择面板、选中后建筑变绿，
/// 左上角显示所选兵种头像与冷却遮罩，冷却结束后在建筑正中间召唤指定数量的兵种并持续循环产出。
/// 训练状态归本组件所有；点击走 EventSystem（IPointerDownHandler），命中建筑根物体上的 BoxCollider2D。
/// 本组件不创建运行时对象、不操作相机或射线器：头像角标与冷却遮罩由兵营预制体预先配置。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(GameObjectProperty))]
public sealed class BuildingTraining : MonoBehaviour, IPointerDownHandler
{
    [Header("兵种列表")]
    [SerializeField] private TroopDefinition[] troops = new TroopDefinition[0];
    [SerializeField, Tooltip("勾选后本建筑按“黑暗兵营”结算等级（建筑与产出兵种使用黑暗兵营等级）")]
    private bool isDarkBarracks;

    [Header("左上角头像角标")]
    [SerializeField, Min(.05f)] private float avatarSize = 1f;
    [SerializeField] private Vector2 avatarOffset = new Vector2(-1.4f, 2.05f);
    [SerializeField, Range(0f, 1f)] private float cooldownMaskAlpha = .65f;

    [Header("选中表现")]
    [SerializeField] private Color selectedTint = new Color(.35f, 1f, .35f, 1f);

    [Header("面板资源")]
    [ResourceKey(typeof(GameObject))]
    [SerializeField] private string panelPrefabKey = "TroopTrainingPanel";

    [Header("预制体配置的表现子对象（同预制体内部引用，允许序列化）")]
    [SerializeField] private SpriteRenderer avatarRenderer;
    [SerializeField] private SpriteRenderer cooldownMaskRenderer;
    [Tooltip("升级模式下显示的升级金币标记对象（预制体内部引用）；可见时点击仅用于升级，不打开训练面板。")]
    [SerializeField] private GameObject upgradeCoinMark;

    private GameObjectProperty prop;
    private BuildingBase buildingBase;
    private SpriteRenderer body;
    private Color bodyColor;

    private Vector3 avatarBaseScale = Vector3.one;
    private float avatarWorldHeight = 1f;

    private TroopDefinition currentTroop;
    private float readyTime = float.NegativeInfinity;
    private bool selected;

    private static BuildingTraining activeBuilding;

    /// <summary>当前产出中的兵种；未选择时为 null。</summary>
    public TroopDefinition CurrentTroop => currentTroop;
    public bool IsTraining => currentTroop != null;
    /// <summary>下一轮产出就绪时间（Time.time 基准）；未选择时为负无穷。</summary>
    public float ReadyTime => readyTime;
    public bool Selected => selected;
    /// <summary>当前建筑显示等级（1-3），读取框架数据 BuildingBase.Level（由建筑升级系统写入）。</summary>
    public int Level => buildingBase != null ? buildingBase.Level : 1;
    public TroopDefinition[] Troops => troops;

    /// <summary>当前被选中（面板打开中）的训练建筑。</summary>
    public static BuildingTraining ActiveBuilding => activeBuilding;

    /// <summary>本建筑是否按黑暗兵营结算等级（供等级注入方读取）。</summary>
    public bool IsDarkBarracks => isDarkBarracks;

    /// <summary>取解锁等级为 row 的兵种中第 index 个；不存在时返回 null（面板按解锁等级 1/2/3 分排）。</summary>
    public TroopDefinition GetTroop(int row, int index)
    {
        int seen = 0;
        for (int i = 0; i < troops.Length; i++)
        {
            TroopDefinition troop = troops[i];
            if (!troop || troop.UnlockLevel != row) continue;
            if (seen == index) return troop;
            seen++;
        }
        return null;
    }

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        buildingBase = GetComponent<BuildingBase>();
        body = GetComponent<SpriteRenderer>();
        if (body) bodyColor = body.color;

        // 初始化预制体已配置的表现子对象；不创建任何运行时对象。
        if (cooldownMaskRenderer)
        {
            cooldownMaskRenderer.color = new Color(0f, 0f, 0f, cooldownMaskAlpha);
            cooldownMaskRenderer.enabled = false;
        }
        if (avatarRenderer) avatarRenderer.enabled = false;
    }

    private void OnEnable()
    {
        PreloadResources();
        if (activeBuilding == this) activeBuilding = null;
    }

    private void OnDisable()
    {
        if (activeBuilding == this)
        {
            activeBuilding = null;
            TroopTrainingPanel.DeselectBuilding(this);
        }
        SetSelected(false);
        currentTroop = null;
        readyTime = float.NegativeInfinity;
        if (avatarRenderer) avatarRenderer.enabled = false;
        if (cooldownMaskRenderer) cooldownMaskRenderer.enabled = false;

        // 建筑失效/被摧毁/回收时注销兵种维护费，收入恢复全额。
        if (Coins.Instance) Coins.Instance.UnregisterUpkeep(this);
    }

    private void Update()
    {
        if (!currentTroop) return;

        float remaining = readyTime - Time.time;
        if (remaining > 0f)
        {
            ApplyCooldownMask(Mathf.Clamp01(remaining / Mathf.Max(.1f, currentTroop.Cooldown)));
            return;
        }

        // 冷却刷新：产出兵种并立刻进入下一轮冷却（持续产出，直到更换兵种或建筑失效）。
        SpawnTroops();
        readyTime = Time.time + currentTroop.Cooldown;
        ApplyCooldownMask(1f);
    }

    #region 点击与选中
    /// <summary>点击处理：命中建筑根物体上的 BoxCollider2D（由预制体配置）时打开训练面板。</summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        RaycastResult raycast = eventData.pointerPressRaycast;
        if (raycast.gameObject == null) return;
        if (raycast.gameObject != gameObject) return;
        HandleBuildingClicked();
    }

    /// <summary>
    /// 建筑被点击：升级金币标记可见（升级模式）时点击仅用于升级，不打开训练面板；
    /// 施工完成（BuildingBase 框架状态，未完成的建筑不响应）时打开训练面板。
    /// 队伍规则：玩家只能操作队伍 1 的建筑（敌方/队友建筑不响应）。
    /// </summary>
    private void HandleBuildingClicked()
    {
        if (upgradeCoinMark && upgradeCoinMark.activeInHierarchy) return;
        if (!buildingBase || !buildingBase.IsCompleted) return;
        if (prop == null || prop.side != 1) return;
        TroopTrainingPanel.Open(this);
    }

    /// <summary>设置选中状态；同一时间只保留一个选中建筑（旧的自动取消）；选中时建筑本体变绿。</summary>
    public void SetSelected(bool value)
    {
        if (selected == value) return;
        selected = value;

        if (selected)
        {
            if (activeBuilding && activeBuilding != this)
                activeBuilding.SetSelected(false);
            activeBuilding = this;
        }
        else if (activeBuilding == this)
        {
            activeBuilding = null;
        }

        if (body) body.color = value ? selectedTint : bodyColor;
    }
    #endregion

    #region 训练与召唤
    /// <summary>
    /// 是否满足训练条件：当前建筑显示等级已解锁该兵种，且按当前收入足以承担其维护费
    /// （维护费不足的兵种不可点击训练，避免把每秒净收入压到 0）。
    /// </summary>
    public bool CanTrain(TroopDefinition troop)
    {
        return troop && troop.UnlockLevel <= Level && CanAffordUpkeep(troop);
    }

    /// <summary>
    /// 按当前毛收入与已登记维护费判断：开始/更换该兵种后每秒净收入仍大于 0。
    /// 经 TeamEconomy 路由：敌方建筑走队伍账本，玩家建筑走全局 Coins。
    /// 玩家侧金币系统未就绪时不拦截（先按可训练处理）。
    /// </summary>
    private bool CanAffordUpkeep(TroopDefinition troop)
    {
        int side = prop != null ? prop.side : 1;
        if (!TeamRules.IsEnemySide(side) && Coins.Instance == null)
            return true;

        int currentUpkeep = currentTroop ? currentTroop.Upkeep : 0;
        int projected = TeamEconomy.TotalUpkeep(prop) - currentUpkeep + troop.Upkeep;
        return TeamEconomy.CurrentCoinPerSec(prop) > projected;
    }

    /// <summary>
    /// 取消当前出兵：清除正在训练的兵种，隐藏头像角标与冷却遮罩，
    /// 并注销该兵种维护费（每秒收入恢复全额）。面板打开时由面板刷新槽位高亮。
    /// </summary>
    public void CancelTraining()
    {
        if (currentTroop == null)
            return;

        currentTroop = null;
        readyTime = float.NegativeInfinity;
        TeamEconomy.UnregisterUpkeep(prop, this);

        if (avatarRenderer)
            avatarRenderer.enabled = false;
        if (cooldownMaskRenderer)
            cooldownMaskRenderer.enabled = false;
    }

    /// <summary>指定兵种是否处于当前产出中（供面板标记当前兵种）。</summary>
    public bool IsTrainingTroop(TroopDefinition troop)
    {
        return troop && currentTroop == troop;
    }

    /// <summary>
    /// 开始持续产出指定兵种；成功后头像角标出现并进入冷却循环，
    /// 同时把该兵种维护费登记进 Coins（每秒收入按维护费实时抵扣；更换兵种自动换成新值）。
    /// 已在产出同一兵种时不重复触发。
    /// </summary>
    public bool TryStartTraining(TroopDefinition troop)
    {
        if (!CanTrain(troop)) return false;
        if (currentTroop == troop) return false;
        currentTroop = troop;
        readyTime = Time.time + troop.Cooldown;
        ApplyAvatar(troop);
        ApplyCooldownMask(1f);
        TeamEconomy.RegisterUpkeep(prop, this, troop.Upkeep);

        // 选定即预载（幂等）：让异步资源在首个冷却周期内加载完成，
        // 避免“出生时资源还没进缓存 → 本周期跳过生产”（部分兵种生产不出来的时序根因）。
        TroopPreloader.Preload(troop);
        return true;
    }

    /// <summary>冷却结束后在建筑正中间召唤 trainCount 个兵种（对象池生成；避免边缘出生卡住）。</summary>
    private void SpawnTroops()
    {
        if (!currentTroop || !ResourceManager.Instance || !GameObjectPool.Instance) return;

        GameObject prefab = ResourceManager.Instance.GetGameObject(currentTroop.PrefabKey);
        if (!prefab)
        {
            // 资源还没进缓存：重新发起预载（幂等，已缓存直接返回），下个冷却周期再产出，
            // 避免异步预载未完成/曾失败时兵种被永久跳过。
            Debug.LogWarning($"[BuildingTraining] 兵种预制体未预载：{currentTroop.PrefabKey}，已重新发起预载，下个冷却周期重试。", this);
            TroopPreloader.Preload(currentTroop);
            return;
        }

        // 图鉴“获得”标记：兵营产出该兵种即视为玩家获得了该兵种，解锁对应图鉴。
        BookProgress.MarkOwned(currentTroop.PrefabKey);

        int count = Mathf.Max(1, currentTroop.TrainCount);
        for (int i = 0; i < count; i++)
        {
            GameObject unit = GameObjectPool.Instance.Get(prefab);
            if (!unit)
            {
                // 对象池没有可用实例（全部在场上/池上限）：告警并跳过本轮，
                // 下个冷却周期重试；不再静默失败。
                Debug.LogWarning($"[BuildingTraining] 对象池无法提供兵种实例：{currentTroop.PrefabKey}，下个冷却周期重试。", this);
                continue;
            }

            // 出生点：建筑朝向敌方的正面外沿（我方 → 右侧，敌方翻转 → 左侧）。
            // 原实现出生在建筑正中间（落在自家建筑占地内），寻路第一步就被自己建筑挡死。
            // 正面被邻接建筑占用时依次尝试背面、最后回落建筑中心。
            Vector3 spawnPos = transform.position;
            if (prop != null)
            {
                int dir = TeamRules.IsEnemySide(prop.side) ? -1 : 1;
                float offset = prop.occupySpace.x * 0.5f + 0.6f;
                Vector3 front = transform.position + Vector3.right * (dir * offset);
                Vector3 back = transform.position - Vector3.right * (dir * offset);

                if (IsSpawnCellFree(front))
                    spawnPos = front;
                else if (IsSpawnCellFree(back))
                    spawnPos = back;
            }
            unit.transform.position = spawnPos;

            // 等级注入：兵种继承兵营的等级上下文（兵营等级缩放 + Buff 等级），并按其缩放生命/攻击/魔攻。
            // ApplyLevelScale 内部经 CharacterHealth 受控 API 同步满血。
            GameObjectProperty unitProp = unit.GetComponent<GameObjectProperty>();
            if (unitProp && prop)
            {
                unitProp.side = prop.side;
                unitProp.barracksLevel = prop.barracksLevel;
                unitProp.darkBarracksLevel = prop.darkBarracksLevel;
                unitProp.sentryTowerLevel = prop.sentryTowerLevel;
                unitProp.defenseMagicLevel = prop.defenseMagicLevel;
                unitProp.attackMagicLevel = prop.attackMagicLevel;
                unitProp.troopTag = currentTroop.Tag;               // AI 定位标签（敌方 AI 判定作用/转产）。
                unitProp.threatScore = currentTroop.ThreatScore;    // 威胁权重（敌方 AI 威胁评估）。
                unitProp.ApplyLevelScale(prop.barracksLevel);
            }
        }
    }
    #endregion

    #region 表现层（头像角标、冷却遮罩）
    private void ApplyAvatar(TroopDefinition troop)
    {
        Sprite icon = ResourceManager.Instance ? ResourceManager.Instance.GetSprite(troop.IconKey) : null;
        if (!avatarRenderer) return;
        avatarRenderer.sprite = icon;
        avatarRenderer.enabled = icon != null;

        if (!icon) return;
        float worldHeight = icon.rect.height / Mathf.Max(.01f, icon.pixelsPerUnit);
        float scale = avatarSize / Mathf.Max(.01f, worldHeight);
        avatarBaseScale = new Vector3(scale, scale, 1f);
        avatarWorldHeight = avatarSize;
        avatarRenderer.transform.localScale = avatarBaseScale;
        avatarRenderer.transform.localPosition = new Vector3(avatarOffset.x, avatarOffset.y, 0f);
        if (cooldownMaskRenderer)
        {
            cooldownMaskRenderer.sprite = icon;
            cooldownMaskRenderer.transform.localScale = avatarBaseScale;
        }
    }

    private void ApplyCooldownMask(float fraction)
    {
        if (!cooldownMaskRenderer || !avatarRenderer || !avatarRenderer.sprite)
        {
            if (cooldownMaskRenderer) cooldownMaskRenderer.enabled = false;
            return;
        }

        if (fraction <= 0f)
        {
            cooldownMaskRenderer.enabled = false;
            return;
        }

        cooldownMaskRenderer.enabled = true;
        cooldownMaskRenderer.transform.localScale =
            new Vector3(avatarBaseScale.x, avatarBaseScale.y * fraction, 1f);
        cooldownMaskRenderer.transform.localPosition =
            new Vector3(avatarOffset.x, avatarOffset.y, 0f) +
            Vector3.up * (avatarWorldHeight * (1f - fraction) * .5f);
    }
    #endregion

    /// <summary>出生候选点所在网格是否可通行（地图未就绪时按可通行处理）。</summary>
    private static bool IsSpawnCellFree(Vector3 worldPos)
    {
        MapCells mapCells = MapCells.Instance;
        if (mapCells == null)
            return true;

        Vector2Int cell = new Vector2Int(
            Mathf.FloorToInt(worldPos.x),
            Mathf.FloorToInt(worldPos.y));
        return !mapCells.IsPathBlocked(cell, null, null);
    }

    #region 资源预载（仅框架 API：ResourceManager.LoadExtraResourceAsync）
    /// <summary>
    /// 预载本建筑全部兵种依赖（统一走 TroopPreloader：预制体/图标/动画/投射物/音效自动扫描），
    /// 以及训练面板预制体。加载期通常已按种族预载过，这里是幂等兜底（已缓存直接返回）。
    /// </summary>
    private void PreloadResources()
    {
        if (!ResourceManager.Instance) return;

        foreach (TroopDefinition troop in troops)
            TroopPreloader.Preload(troop);

        ResourceManager.Instance.LoadExtraResourceAsync<GameObject>(panelPrefabKey);
    }
    #endregion
}
