using UnityEngine;

/// <summary>
/// 训练兵种的静态定义数据：阶数、解锁建筑等级、单次产出数量、冷却与资源键。
/// 运行时资源必须通过 ResourceManager 按资源键取得，不直接持有贴图/预制体引用。
/// </summary>
[CreateAssetMenu(fileName = "NewTroop", menuName = "Building/Troop")]
public sealed class TroopDefinition : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;

    [Header("训练规则")]
    [SerializeField, Range(1, 3)] private int tier = 1;
    [SerializeField, Min(0), Tooltip("解锁所需兵营等级（1 起；兵营显示等级 = BuildUP 等级 + 1）")]
    private int unlockLevel;
    [SerializeField, Min(1)] private int trainCount = 1;
    [SerializeField, Min(.1f)] private float cooldown = 5f;
    [SerializeField, Min(0), Tooltip("选定本兵种后每秒扣除的资源获取量（维护费）")]
    private int upkeep;
    [SerializeField, Tooltip("勾选后，该兵种需要玩家拥有后才能训练（未拥有时头像暗淡）")]
    private bool requiresOwnership;

    [Header("资源")]
    [ResourceKey(typeof(Sprite))]
    [SerializeField] private string iconKey;
    [ResourceKey(typeof(GameObject))]
    [SerializeField] private string prefabKey;
    [ResourceKey(typeof(RuntimeAnimatorController))]
    [SerializeField, Tooltip("兵种单位的动画控制器资源键（数据驱动预载；框架暂无单位依赖扫描 API，先由本配置提供）")]
    private string animatorControllerKey;
    [ResourceKey(typeof(Sprite))]
    [SerializeField, Tooltip("兵种单位的基础动画贴图资源键（同上，用于召唤前预载）")]
    private string animationSpriteKey;

#if UNITY_EDITOR
    [SerializeField] private Sprite editorIcon;
    [SerializeField] private GameObject editorPrefab;
#endif

    public string Id => id;
    public string DisplayName => displayName;
    public int Tier => tier;
    public int UnlockLevel => unlockLevel;
    public int TrainCount => trainCount;
    public float Cooldown => cooldown;
    public int Upkeep => upkeep;
    public string IconKey => iconKey;
    public string PrefabKey => prefabKey;
    public string AnimatorControllerKey => animatorControllerKey;
    public string AnimationSpriteKey => animationSpriteKey;
    public bool RequiresOwnership => requiresOwnership;

#if UNITY_EDITOR
    public Sprite EditorIcon => editorIcon;
    public GameObject EditorPrefab => editorPrefab;

    /// <summary>把旧 Inspector 引用迁移为资源键（与 EngineerDefinition 同规则）。</summary>
    private void OnValidate()
    {
        id = id?.Trim();
        displayName = displayName?.Trim();
        if (string.IsNullOrWhiteSpace(iconKey) && editorIcon) iconKey = editorIcon.name;
        if (string.IsNullOrWhiteSpace(prefabKey) && editorPrefab) prefabKey = editorPrefab.name;
    }
#endif
}
