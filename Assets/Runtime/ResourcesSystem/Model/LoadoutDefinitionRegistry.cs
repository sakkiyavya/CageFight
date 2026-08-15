using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 工程师、种族和法术定义的稳定 ID 注册表。它是 ResourceManager 的唯一选装定义来源，
/// 不保存玩家当前选择，也不执行任何局内行为。
/// </summary>
[CreateAssetMenu(fileName = "LoadoutDefinitionRegistry", menuName = "ResourcesSystem/Loadout Definition Registry")]
public sealed class LoadoutDefinitionRegistry : ScriptableObject
{
    [SerializeField] private List<EngineerDefinition> engineerDefinitions = new List<EngineerDefinition>();
    [SerializeField] private List<RaceDefinition> raceDefinitions = new List<RaceDefinition>();
    [SerializeField] private List<SpellDefinition> spellDefinitions = new List<SpellDefinition>();

    [Header("默认选择（稳定 ID）")]
    [SerializeField] private string defaultEngineerId;
    [SerializeField] private string defaultRaceId;
    [SerializeField] private string defaultSpellSlot1Id;
    [SerializeField] private string defaultSpellSlot2Id;

    private Dictionary<string, EngineerDefinition> engineerById;
    private Dictionary<string, RaceDefinition> raceById;
    private Dictionary<string, SpellDefinition> spellById;
    private bool initialized;

    public IReadOnlyList<EngineerDefinition> EngineerDefinitions => engineerDefinitions;
    public IReadOnlyList<RaceDefinition> RaceDefinitions => raceDefinitions;
    public IReadOnlyList<SpellDefinition> SpellDefinitions => spellDefinitions;

    /// <summary>构建只读查找索引；可重复调用且只在定义更新后重新执行。</summary>
    public void Initialize()
    {
        if (initialized) return;

        engineerById = new Dictionary<string, EngineerDefinition>();
        raceById = new Dictionary<string, RaceDefinition>();
        spellById = new Dictionary<string, SpellDefinition>();
        AddEngineers();
        AddRaces();
        AddSpells();
        NormalizeDefaults();
        initialized = true;
    }

    /// <summary>按稳定 ID 查找工程师定义。</summary>
    public bool TryGetEngineer(string id, out EngineerDefinition definition)
    {
        Initialize();
        definition = null;
        return !string.IsNullOrWhiteSpace(id) && engineerById.TryGetValue(id, out definition);
    }

    /// <summary>按稳定 ID 查找种族定义。</summary>
    public bool TryGetRace(string id, out RaceDefinition definition)
    {
        Initialize();
        definition = null;
        return !string.IsNullOrWhiteSpace(id) && raceById.TryGetValue(id, out definition);
    }

    /// <summary>按稳定 ID 查找法术定义。</summary>
    public bool TryGetSpell(string id, out SpellDefinition definition)
    {
        Initialize();
        definition = null;
        return !string.IsNullOrWhiteSpace(id) && spellById.TryGetValue(id, out definition);
    }

    /// <summary>返回已验证的默认 ID；注册表缺少必要定义时返回 false。</summary>
    public bool TryGetDefaultLoadout(
        out string engineerId,
        out string raceId,
        out string spellSlot1Id,
        out string spellSlot2Id)
    {
        Initialize();
        engineerId = defaultEngineerId;
        raceId = defaultRaceId;
        spellSlot1Id = defaultSpellSlot1Id;
        spellSlot2Id = defaultSpellSlot2Id;
        return !string.IsNullOrWhiteSpace(engineerId) &&
            engineerById.ContainsKey(engineerId) &&
            !string.IsNullOrWhiteSpace(raceId) &&
            raceById.ContainsKey(raceId);
    }

    /// <summary>供 RegistryBuilder 原子替换当前注册内容；运行时不应调用。</summary>
    public void ReplaceDefinitions(
        List<EngineerDefinition> engineers,
        List<RaceDefinition> races,
        List<SpellDefinition> spells)
    {
        engineerDefinitions = engineers ?? new List<EngineerDefinition>();
        raceDefinitions = races ?? new List<RaceDefinition>();
        spellDefinitions = spells ?? new List<SpellDefinition>();
        initialized = false;
        Initialize();
    }

    private void OnValidate()
    {
        initialized = false;
    }

    private void AddEngineers()
    {
        for (int i = 0; i < engineerDefinitions.Count; i++)
        {
            EngineerDefinition definition = engineerDefinitions[i];
            if (!definition || string.IsNullOrWhiteSpace(definition.Id)) continue;
            if (engineerById.ContainsKey(definition.Id))
            {
                Debug.LogWarning($"[LoadoutDefinitionRegistry] 重复工程师 ID：{definition.Id}", this);
                continue;
            }

            engineerById.Add(definition.Id, definition);
        }
    }

    private void AddRaces()
    {
        for (int i = 0; i < raceDefinitions.Count; i++)
        {
            RaceDefinition definition = raceDefinitions[i];
            if (!definition || string.IsNullOrWhiteSpace(definition.Id)) continue;
            if (raceById.ContainsKey(definition.Id))
            {
                Debug.LogWarning($"[LoadoutDefinitionRegistry] 重复种族 ID：{definition.Id}", this);
                continue;
            }

            raceById.Add(definition.Id, definition);
        }
    }

    private void AddSpells()
    {
        for (int i = 0; i < spellDefinitions.Count; i++)
        {
            SpellDefinition definition = spellDefinitions[i];
            if (!definition || string.IsNullOrWhiteSpace(definition.Id)) continue;
            if (spellById.ContainsKey(definition.Id))
            {
                Debug.LogWarning($"[LoadoutDefinitionRegistry] 重复法术 ID：{definition.Id}", this);
                continue;
            }

            spellById.Add(definition.Id, definition);
        }
    }

    private void NormalizeDefaults()
    {
        if (!engineerById.ContainsKey(defaultEngineerId))
        {
            defaultEngineerId = string.Empty;
            foreach (KeyValuePair<string, EngineerDefinition> entry in engineerById)
            {
                defaultEngineerId = entry.Key;
                break;
            }
        }

        if (!raceById.ContainsKey(defaultRaceId))
        {
            defaultRaceId = string.Empty;
            foreach (KeyValuePair<string, RaceDefinition> entry in raceById)
            {
                defaultRaceId = entry.Key;
                break;
            }
        }
        if (!string.IsNullOrWhiteSpace(defaultSpellSlot1Id) &&
            !spellById.ContainsKey(defaultSpellSlot1Id))
            defaultSpellSlot1Id = string.Empty;
        if (!string.IsNullOrWhiteSpace(defaultSpellSlot2Id) &&
            !spellById.ContainsKey(defaultSpellSlot2Id))
            defaultSpellSlot2Id = string.Empty;
    }
}
