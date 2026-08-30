using System;
using UnityEngine;

/// <summary>
/// 玩家全局信息背景板。
/// 仅负责在内存中维护玩家信息以及 JSON 的序列化、反序列化；
/// 本地存档、抖音 SDK 和服务器通信由外部模块负责。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public sealed class UserGlobalInfo : MonoBehaviour
{
    private const int MaxLoadoutIdLength = 128;

    public static UserGlobalInfo Instance { get; private set; }

    [Header("玩家全局信息")]
    [SerializeField] private UserGlobalInfoData data = new UserGlobalInfoData();

    private UserGlobalInfoData Data
    {
        get
        {
            EnsureDataExists();
            return data;
        }
    }

    public int DefenseMagicLevel => Data.defenseMagicLevel;
    public int AttackMagicLevel => Data.attackMagicLevel;
    public int BarracksLevel => Data.barracksLevel;
    public int DarkBarracksLevel => Data.darkBarracksLevel;
    public int SentryTowerLevel => Data.sentryTowerLevel;
    public int DiamondCount => Data.diamondCount;
    public int GoldBarCount => Data.goldBarCount;
    public uint UnlockedStage => Data.unlockedStage;

    public float Volume => Data.volume;
    public bool ShowDamage => Data.showDamage;
    public string SelectedEngineerId => Data.selectedEngineerId;
    public string SelectedRaceId => Data.selectedRaceId;
    public string SelectedSpellSlot1Id => Data.selectedSpellSlot1Id;
    public string SelectedSpellSlot2Id => Data.selectedSpellSlot2Id;
    public string SelectedRaceMainBasePrefabKey => Data.selectedRaceMainBasePrefabKey;

    /// <summary>
    /// 任意一项信息被修改、成功导入 JSON 或重置后触发。
    /// </summary>
    public event Action Changed;

    #region 生命周期
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[UserGlobalInfo] 场景中存在重复实例，后创建的组件将被销毁。", this);
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureDataExists();
        NormalizeInspectorData();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        EnsureDataExists();
        NormalizeInspectorData();
    }
    #endregion

    #region 信息修改
    public bool SetDefenseMagicLevel(int value)
    {
        EnsureDataExists();
        return SetNonNegativeValue(ref data.defenseMagicLevel, value, nameof(DefenseMagicLevel));
    }

    public bool SetAttackMagicLevel(int value)
    {
        EnsureDataExists();
        return SetNonNegativeValue(ref data.attackMagicLevel, value, nameof(AttackMagicLevel));
    }

    public bool SetBarracksLevel(int value)
    {
        EnsureDataExists();
        return SetNonNegativeValue(ref data.barracksLevel, value, nameof(BarracksLevel));
    }

    public bool SetDarkBarracksLevel(int value)
    {
        EnsureDataExists();
        return SetNonNegativeValue(ref data.darkBarracksLevel, value, nameof(DarkBarracksLevel));
    }

    public bool SetSentryTowerLevel(int value)
    {
        EnsureDataExists();
        return SetNonNegativeValue(ref data.sentryTowerLevel, value, nameof(SentryTowerLevel));
    }

    public bool SetDiamondCount(int value)
    {
        EnsureDataExists();
        return SetNonNegativeValue(ref data.diamondCount, value, nameof(DiamondCount));
    }

    public bool SetGoldBarCount(int value)
    {
        EnsureDataExists();
        return SetNonNegativeValue(ref data.goldBarCount, value, nameof(GoldBarCount));
    }

    public bool SetUnlockedStage(uint value)
    {
        EnsureDataExists();

        if (value < 1u)
        {
            Debug.LogWarning("[UserGlobalInfo] UnlockedStage 不能小于 1。", this);
            return false;
        }

        if (data.unlockedStage == value)
        {
            return false;
        }

        data.unlockedStage = value;
        Changed?.Invoke();
        return true;
    }

    public bool SetVolume(float value)
    {
        EnsureDataExists();

        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f)
        {
            Debug.LogWarning("[UserGlobalInfo] Volume 必须是 0 到 1 之间的有效数值。", this);
            return false;
        }

        if (Mathf.Approximately(data.volume, value))
        {
            return false;
        }

        data.volume = value;
        Changed?.Invoke();
        return true;
    }

    public bool SetShowDamage(bool value)
    {
        EnsureDataExists();

        if (data.showDamage == value)
        {
            return false;
        }

        data.showDamage = value;
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// 原子地写入工程师、种族和两个自选法术的稳定 ID，并保留现有种族大本营 Key。
    /// </summary>
    public bool SetLoadoutSelection(
        string engineerId,
        string raceId,
        string spellSlot1Id,
        string spellSlot2Id)
    {
        return SetLoadoutSelection(
            engineerId,
            raceId,
            spellSlot1Id,
            spellSlot2Id,
            Data.selectedRaceMainBasePrefabKey);
    }

    /// <summary>
    /// 原子地写入工程师、种族、两个自选法术及所选种族大本营的预制体 Key。
    /// 大本营 Key 来自选装界面配置，空字符串表示该种族当前不生成大本营。
    /// </summary>
    public bool SetLoadoutSelection(
        string engineerId,
        string raceId,
        string spellSlot1Id,
        string spellSlot2Id,
        string raceMainBasePrefabKey)
    {
        EnsureDataExists();

        engineerId = NormalizeLoadoutId(engineerId);
        raceId = NormalizeLoadoutId(raceId);
        spellSlot1Id = NormalizeLoadoutId(spellSlot1Id);
        spellSlot2Id = NormalizeLoadoutId(spellSlot2Id);
        raceMainBasePrefabKey = NormalizeLoadoutId(raceMainBasePrefabKey);
        if (!AreLoadoutIdsValid(
                engineerId,
                raceId,
                spellSlot1Id,
                spellSlot2Id,
                raceMainBasePrefabKey,
                out string error))
        {
            Debug.LogWarning($"[UserGlobalInfo] {error}", this);
            return false;
        }

        if (data.selectedEngineerId == engineerId &&
            data.selectedRaceId == raceId &&
            data.selectedSpellSlot1Id == spellSlot1Id &&
            data.selectedSpellSlot2Id == spellSlot2Id &&
            data.selectedRaceMainBasePrefabKey == raceMainBasePrefabKey)
            return false;

        data.selectedEngineerId = engineerId;
        data.selectedRaceId = raceId;
        data.selectedSpellSlot1Id = spellSlot1Id;
        data.selectedSpellSlot2Id = spellSlot2Id;
        data.selectedRaceMainBasePrefabKey = raceMainBasePrefabKey;
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// 将内存数据恢复为该版本定义的默认值。此操作不删除任何外部存档。
    /// </summary>
    public void ResetToDefault()
    {
        data = new UserGlobalInfoData();
        Changed?.Invoke();
    }
    #endregion

    #region JSON 转换
    /// <summary>
    /// 将当前内存数据转换为 JSON。
    /// </summary>
    public string SerializeToJson(bool prettyPrint = false)
    {
        EnsureDataExists();
        data.schemaVersion = UserGlobalInfoData.CurrentSchemaVersion;
        return JsonUtility.ToJson(data, prettyPrint);
    }

    /// <summary>
    /// 尝试用 JSON 原子替换当前内存数据。
    /// 解析、版本迁移或数据校验失败时，当前数据保持不变。
    /// </summary>
    public bool TryDeserializeFromJson(string json, out string error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "JSON 不能为空。";
            return false;
        }

        string trimmedJson = json.Trim();
        if (trimmedJson.Length < 2 ||
            trimmedJson[0] != '{' ||
            trimmedJson[trimmedJson.Length - 1] != '}')
        {
            error = "JSON 根节点必须是对象。";
            return false;
        }

        UserGlobalInfoData candidate = new UserGlobalInfoData
        {
            // 没有 schemaVersion 的旧 JSON 必须从版本 0 开始迁移，不能被误认为当前版本。
            schemaVersion = 0
        };
        try
        {
            // 只覆盖临时默认对象，既保证导入原子性，也让旧 JSON 缺少的新增字段保留新版本默认值。
            JsonUtility.FromJsonOverwrite(trimmedJson, candidate);

            // 版本 0/1 的货币字段名为 coinCount；只在导入旧 JSON 时读取并迁移，
            // 当前版本导出的 JSON 仅包含 goldBarCount。
            if (candidate.schemaVersion <= 1)
            {
                LegacyCurrencyDataV1 legacyCurrency = new LegacyCurrencyDataV1();
                JsonUtility.FromJsonOverwrite(trimmedJson, legacyCurrency);
                candidate.goldBarCount = legacyCurrency.coinCount;
            }
        }
        catch (ArgumentException exception)
        {
            error = $"JSON 解析失败：{exception.Message}";
            return false;
        }

        if (!TryMigrate(candidate, out error) || !TryValidate(candidate, out error))
        {
            return false;
        }

        data = candidate;
        Changed?.Invoke();
        return true;
    }
    #endregion

    #region 内部辅助
    private bool SetNonNegativeValue(ref int currentValue, int newValue, string valueName)
    {
        if (newValue < 0)
        {
            Debug.LogWarning($"[UserGlobalInfo] {valueName} 不能小于 0。", this);
            return false;
        }

        if (currentValue == newValue)
        {
            return false;
        }

        currentValue = newValue;
        Changed?.Invoke();
        return true;
    }

    private static bool TryMigrate(UserGlobalInfoData candidate, out string error)
    {
        error = null;

        if (candidate.schemaVersion > UserGlobalInfoData.CurrentSchemaVersion)
        {
            error =
                $"存档版本 {candidate.schemaVersion} 高于当前支持版本 " +
                $"{UserGlobalInfoData.CurrentSchemaVersion}。";
            return false;
        }

        if (candidate.schemaVersion < 0)
        {
            error = $"存档版本 {candidate.schemaVersion} 非法。";
            return false;
        }

        while (candidate.schemaVersion < UserGlobalInfoData.CurrentSchemaVersion)
        {
            switch (candidate.schemaVersion)
            {
                // 版本 0 视为首版发布前、不带版本号但字段结构与版本 1 相同的数据。
                case 0:
                    candidate.schemaVersion = 1;
                    break;

                // 版本 2 将玩家货币字段从 coinCount 更名为 goldBarCount。
                // 旧字段值已在 JSON 解析阶段读入 goldBarCount。
                case 1:
                    candidate.schemaVersion = 2;
                    break;

                // 版本 3 新增玩家设置，以及最小为 1 的已解锁关卡记录。
                // 反序列化使用默认候选对象，旧 JSON 缺少这些字段时会保留默认值。
                case 2:
                    candidate.schemaVersion = 3;
                    break;

                // 版本 4 新增工程师、种族和两个可选法术的稳定 ID。
                // 旧存档缺少这些字段时，候选对象保留空字符串默认值。
                case 3:
                    candidate.selectedEngineerId = NormalizeLoadoutId(candidate.selectedEngineerId);
                    candidate.selectedRaceId = NormalizeLoadoutId(candidate.selectedRaceId);
                    candidate.selectedSpellSlot1Id = NormalizeLoadoutId(candidate.selectedSpellSlot1Id);
                    candidate.selectedSpellSlot2Id = NormalizeLoadoutId(candidate.selectedSpellSlot2Id);
                    candidate.schemaVersion = 4;
                    break;

                // 版本 5 新增由选装按钮配置的种族大本营预制体 Key。
                // 旧存档缺少该字段时保留空字符串，避免未配置的位置意外生成大本营。
                case 4:
                    candidate.selectedRaceMainBasePrefabKey =
                        NormalizeLoadoutId(candidate.selectedRaceMainBasePrefabKey);
                    candidate.schemaVersion = 5;
                    break;

                // 后续提升 CurrentSchemaVersion 时，必须在这里补充逐版本迁移分支。
                default:
                    error = $"缺少从存档版本 {candidate.schemaVersion} 开始的迁移逻辑。";
                    return false;
            }
        }

        return true;
    }

    private static bool TryValidate(UserGlobalInfoData candidate, out string error)
    {
        candidate.selectedEngineerId = NormalizeLoadoutId(candidate.selectedEngineerId);
        candidate.selectedRaceId = NormalizeLoadoutId(candidate.selectedRaceId);
        candidate.selectedSpellSlot1Id = NormalizeLoadoutId(candidate.selectedSpellSlot1Id);
        candidate.selectedSpellSlot2Id = NormalizeLoadoutId(candidate.selectedSpellSlot2Id);
        candidate.selectedRaceMainBasePrefabKey =
            NormalizeLoadoutId(candidate.selectedRaceMainBasePrefabKey);

        if (candidate.defenseMagicLevel < 0)
        {
            error = "defenseMagicLevel 不能小于 0。";
            return false;
        }

        if (candidate.attackMagicLevel < 0)
        {
            error = "attackMagicLevel 不能小于 0。";
            return false;
        }

        if (candidate.barracksLevel < 0)
        {
            error = "barracksLevel 不能小于 0。";
            return false;
        }

        if (candidate.darkBarracksLevel < 0)
        {
            error = "darkBarracksLevel 不能小于 0。";
            return false;
        }

        if (candidate.sentryTowerLevel < 0)
        {
            error = "sentryTowerLevel 不能小于 0。";
            return false;
        }

        if (candidate.diamondCount < 0)
        {
            error = "diamondCount 不能小于 0。";
            return false;
        }

        if (candidate.goldBarCount < 0)
        {
            error = "goldBarCount 不能小于 0。";
            return false;
        }

        if (candidate.unlockedStage < 1u)
        {
            error = "unlockedStage 不能小于 1。";
            return false;
        }

        if (float.IsNaN(candidate.volume) ||
            float.IsInfinity(candidate.volume) ||
            candidate.volume < 0f ||
            candidate.volume > 1f)
        {
            error = "volume 必须是 0 到 1 之间的有效数值。";
            return false;
        }

        if (!AreLoadoutIdsValid(
                candidate.selectedEngineerId,
                candidate.selectedRaceId,
                candidate.selectedSpellSlot1Id,
                candidate.selectedSpellSlot2Id,
                candidate.selectedRaceMainBasePrefabKey,
                out error))
            return false;

        error = null;
        return true;
    }

    private void EnsureDataExists()
    {
        if (data == null)
        {
            data = new UserGlobalInfoData();
        }
    }

    private void NormalizeInspectorData()
    {
        if (data.schemaVersion < 3)
        {
            data.unlockedStage = 1u;
            data.volume = 1f;
            data.showDamage = true;
        }

        data.schemaVersion = UserGlobalInfoData.CurrentSchemaVersion;
        data.defenseMagicLevel = Mathf.Max(0, data.defenseMagicLevel);
        data.attackMagicLevel = Mathf.Max(0, data.attackMagicLevel);
        data.barracksLevel = Mathf.Max(0, data.barracksLevel);
        data.darkBarracksLevel = Mathf.Max(0, data.darkBarracksLevel);
        data.sentryTowerLevel = Mathf.Max(0, data.sentryTowerLevel);
        data.diamondCount = Mathf.Max(0, data.diamondCount);
        data.goldBarCount = Mathf.Max(0, data.goldBarCount);

        if (data.unlockedStage < 1u)
        {
            data.unlockedStage = 1u;
        }

        data.volume = float.IsNaN(data.volume) || float.IsInfinity(data.volume)
            ? 1f
            : Mathf.Clamp01(data.volume);

        data.selectedEngineerId = NormalizeLoadoutId(data.selectedEngineerId);
        data.selectedRaceId = NormalizeLoadoutId(data.selectedRaceId);
        data.selectedSpellSlot1Id = NormalizeLoadoutId(data.selectedSpellSlot1Id);
        data.selectedSpellSlot2Id = NormalizeLoadoutId(data.selectedSpellSlot2Id);
        data.selectedRaceMainBasePrefabKey =
            NormalizeLoadoutId(data.selectedRaceMainBasePrefabKey);
    }

    private static string NormalizeLoadoutId(string value) => (value ?? string.Empty).Trim();

    private static bool AreLoadoutIdsValid(
        string engineerId,
        string raceId,
        string spellSlot1Id,
        string spellSlot2Id,
        string raceMainBasePrefabKey,
        out string error)
    {
        if (!IsLoadoutIdValid(engineerId) ||
            !IsLoadoutIdValid(raceId) ||
            !IsLoadoutIdValid(spellSlot1Id) ||
            !IsLoadoutIdValid(spellSlot2Id) ||
            !IsLoadoutIdValid(raceMainBasePrefabKey))
        {
            error = $"出战选择 ID 和大本营预制体 Key 可以为空，但不得超过 {MaxLoadoutIdLength} 个字符。";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsLoadoutIdValid(string value)
    {
        return value != null && value.Length <= MaxLoadoutIdLength;
    }

    /// <summary>
    /// 仅用于读取版本 0/1 JSON 中已经废弃的金币字段。
    /// </summary>
    [Serializable]
    private sealed class LegacyCurrencyDataV1
    {
        public int coinCount = 0;
    }
    #endregion
}
