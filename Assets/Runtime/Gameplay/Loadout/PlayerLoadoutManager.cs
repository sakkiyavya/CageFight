using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 选装解析入口。选择的唯一持久化归属是 UserGlobalInfo；本组件只负责
/// 通过 ResourceManager 的注册表解析 ID，并给局内与 UI 提供窄接口。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerLoadoutManager : MonoBehaviour
{
    [SerializeField] private UserGlobalInfo userGlobalInfo;
    [SerializeField] private UserGlobalInfoPersistence persistence;

    private Coroutine readyRoutine;
    private bool readyNotified;

    public event Action Changed;

    /// <summary>存档读取完成后才允许用默认配置补全空选择，避免覆盖玩家存档。</summary>
    public bool IsReady => userGlobalInfo && (!persistence || persistence.IsLoaded) &&
        ResourceManager.Instance && ResourceManager.Instance.IsLoadoutRegistryReady;

    /// <summary>当前所选种族在选装界面配置的大本营预制体 Key；未配置时为空。</summary>
    public string SelectedRaceMainBasePrefabKey => userGlobalInfo
        ? userGlobalInfo.SelectedRaceMainBasePrefabKey
        : string.Empty;

    private void Awake()
    {
        if (!userGlobalInfo) userGlobalInfo = UserGlobalInfo.Instance;
        if (userGlobalInfo && !persistence)
            persistence = userGlobalInfo.GetComponent<UserGlobalInfoPersistence>();

        if (!userGlobalInfo)
            Debug.LogError("[PlayerLoadoutManager] 未配置 UserGlobalInfo。", this);
    }

    private void OnEnable()
    {
        if (userGlobalInfo) userGlobalInfo.Changed += HandleChanged;
        if (persistence) persistence.Loaded += HandleLoaded;
        StartReadyRoutine();
    }

    private void OnDisable()
    {
        if (userGlobalInfo) userGlobalInfo.Changed -= HandleChanged;
        if (persistence) persistence.Loaded -= HandleLoaded;
        if (readyRoutine != null) StopCoroutine(readyRoutine);
        readyRoutine = null;
    }

    /// <summary>将工程师选择写回全局 ID，不在场景对象上保存副本。</summary>
    public bool SelectEngineer(EngineerDefinition definition)
    {
        if (!definition || string.IsNullOrWhiteSpace(definition.Id) || !IsReady) return false;
        return userGlobalInfo.SetLoadoutSelection(
            definition.Id,
            userGlobalInfo.SelectedRaceId,
            userGlobalInfo.SelectedSpellSlot1Id,
            userGlobalInfo.SelectedSpellSlot2Id);
    }

    /// <summary>将种族选择写回全局 ID，不在场景对象上保存副本。</summary>
    public bool SelectRace(RaceDefinition definition) => SelectRace(definition, string.Empty);

    /// <summary>
    /// 将种族选择及其按钮配置的大本营预制体 Key 一并写回全局数据。
    /// 大本营 Key 属于选装配置快照，而非 <see cref="RaceDefinition"/> 的静态定义数据。
    /// </summary>
    public bool SelectRace(RaceDefinition definition, string mainBasePrefabKey)
    {
        if (!definition || string.IsNullOrWhiteSpace(definition.Id) || !IsReady) return false;
        return userGlobalInfo.SetLoadoutSelection(
            userGlobalInfo.SelectedEngineerId,
            definition.Id,
            userGlobalInfo.SelectedSpellSlot1Id,
            userGlobalInfo.SelectedSpellSlot2Id,
            mainBasePrefabKey);
    }

    /// <summary>将可选法术写入指定槽位；0、1 分别对应局内第二、第三格。</summary>
    public bool SelectSpell(int slot, SpellDefinition definition)
    {
        if (!definition || string.IsNullOrWhiteSpace(definition.Id) ||
            (uint)slot >= 2 || !IsReady)
            return false;

        return userGlobalInfo.SetLoadoutSelection(
            userGlobalInfo.SelectedEngineerId,
            userGlobalInfo.SelectedRaceId,
            slot == 0 ? definition.Id : userGlobalInfo.SelectedSpellSlot1Id,
            slot == 1 ? definition.Id : userGlobalInfo.SelectedSpellSlot2Id);
    }

    /// <summary>
    /// 法术双槽选择（选择界面用）：点击未选法术时填入第一个空槽
    /// （局内第二格优先，其次第三格；两格都满则替换第三格，保留第二格）；
    /// 点击已选法术时取消对应槽位（清空）。
    /// </summary>
    public bool SelectSpellSmart(SpellDefinition definition)
    {
        if (!definition || string.IsNullOrWhiteSpace(definition.Id) || !IsReady)
            return false;

        string id = definition.Id;
        string slot1 = userGlobalInfo.SelectedSpellSlot1Id;
        string slot2 = userGlobalInfo.SelectedSpellSlot2Id;

        // 点击已选法术：取消对应槽位。
        if (id == slot1)
            return userGlobalInfo.SetLoadoutSelection(
                userGlobalInfo.SelectedEngineerId, userGlobalInfo.SelectedRaceId, string.Empty, slot2);
        if (id == slot2)
            return userGlobalInfo.SetLoadoutSelection(
                userGlobalInfo.SelectedEngineerId, userGlobalInfo.SelectedRaceId, slot1, string.Empty);

        // 未选：填入第一个空槽；都满则替换第三格（保留第二格，符合“加装第二个”的习惯）。
        string newSlot1 = slot1;
        string newSlot2 = slot2;
        if (string.IsNullOrWhiteSpace(slot1))
            newSlot1 = id;
        else if (string.IsNullOrWhiteSpace(slot2))
            newSlot2 = id;
        else
            newSlot2 = id;

        return userGlobalInfo.SetLoadoutSelection(
            userGlobalInfo.SelectedEngineerId, userGlobalInfo.SelectedRaceId, newSlot1, newSlot2);
    }

    /// <summary>解析当前工程师；注册表或 ID 无效时返回 false。</summary>
    public bool TryGetSelectedEngineer(out EngineerDefinition definition)
    {
        definition = null;
        return IsReady && ResourceManager.Instance &&
            ResourceManager.Instance.TryGetEngineer(
                userGlobalInfo.SelectedEngineerId, out definition);
    }

    /// <summary>解析当前种族；注册表或 ID 无效时返回 false。</summary>
    public bool TryGetSelectedRace(out RaceDefinition definition)
    {
        definition = null;
        return IsReady && ResourceManager.Instance &&
            ResourceManager.Instance.TryGetRace(
                userGlobalInfo.SelectedRaceId, out definition);
    }

    /// <summary>按局内槽位解析法术：0 为工程师自带法术，1、2 为玩家选择法术。</summary>
    public bool TryGetGameplaySpell(int slot, out SpellDefinition definition)
    {
        definition = null;
        if (!IsReady || !ResourceManager.Instance || (uint)slot >= 3) return false;

        string spellId;
        if (slot == 0)
        {
            if (!TryGetSelectedEngineer(out EngineerDefinition engineer)) return false;
            spellId = engineer.InnateSpellId;
        }
        else
        {
            spellId = slot == 1
                ? userGlobalInfo.SelectedSpellSlot1Id
                : userGlobalInfo.SelectedSpellSlot2Id;
        }

        return ResourceManager.Instance.TryGetSpell(spellId, out definition);
    }

    /// <summary>
    /// 在 LoadingState 期间预载本局所选工程师、种族效果与法术预制体。
    /// 该协程只在关卡切换时运行，不位于攻击或帧循环热路径。
    /// </summary>
    public IEnumerator PreloadGameplayResources()
    {
        if (!IsReady || !ResourceManager.Instance) yield break;

        if (TryGetSelectedEngineer(out EngineerDefinition engineer))
        {
            yield return ResourceManager.Instance.LoadRegisteredGameObject(engineer.PrefabKey);
            if (ResourceManager.Instance.TryGetSpell(engineer.InnateSpellId, out SpellDefinition innate))
            {
                yield return ResourceManager.Instance.LoadRegisteredGameObject(innate.CastPrefabKey);
                yield return ResourceManager.Instance.LoadRegisteredSprite(innate.IconKey);
                if (innate.ShowWarningCircle)
                    yield return ResourceManager.Instance.LoadRegisteredSprite(innate.WarningCircleKey);
            }
        }

        if (TryGetSelectedRace(out RaceDefinition race))
            yield return ResourceManager.Instance.LoadRegisteredGameObject(race.RuntimeEffectPrefabKey);

        if (!string.IsNullOrWhiteSpace(SelectedRaceMainBasePrefabKey))
        {
            yield return ResourceManager.Instance.PreloadGameObjectWithDependencies(
                SelectedRaceMainBasePrefabKey);
        }

        for (int slot = 1; slot < 3; slot++)
            if (TryGetGameplaySpell(slot, out SpellDefinition spell))
            {
                yield return ResourceManager.Instance.LoadRegisteredGameObject(spell.CastPrefabKey);
                yield return ResourceManager.Instance.LoadRegisteredSprite(spell.IconKey);
                if (spell.ShowWarningCircle)
                    yield return ResourceManager.Instance.LoadRegisteredSprite(spell.WarningCircleKey);
            }
    }

    /// <summary>预载菜单展示的图标；只应在菜单初始化时调用一次。</summary>
    public IEnumerator PreloadPresentationResources()
    {
        if (!IsReady || !ResourceManager.Instance) yield break;

        foreach (EngineerDefinition engineer in ResourceManager.Instance.EngineerDefinitions)
        {
            yield return ResourceManager.Instance.LoadRegisteredSprite(engineer.IconKey);
            yield return ResourceManager.Instance.LoadRegisteredSprite(engineer.PortraitFrameKey);
            foreach (string key in engineer.IdlePortraitFrameKeys)
                yield return ResourceManager.Instance.LoadRegisteredSprite(key);
        }
        foreach (RaceDefinition race in ResourceManager.Instance.RaceDefinitions)
            yield return ResourceManager.Instance.LoadRegisteredSprite(race.IconKey);
        foreach (SpellDefinition spell in ResourceManager.Instance.SpellDefinitions)
            yield return ResourceManager.Instance.LoadRegisteredSprite(spell.IconKey);
    }

    private void HandleChanged() => Changed?.Invoke();

    private void HandleLoaded()
    {
        StartReadyRoutine();
    }

    private void StartReadyRoutine()
    {
        if (readyNotified) return;
        if (IsReady)
        {
            NotifyReady();
            return;
        }

        if (readyRoutine == null) readyRoutine = StartCoroutine(WaitForReadyRoutine());
    }

    private IEnumerator WaitForReadyRoutine()
    {
        while (!IsReady) yield return null;
        readyRoutine = null;
        NotifyReady();
    }

    private void NotifyReady()
    {
        if (readyNotified) return;
        readyNotified = true;
        if (!EnsureDefaults()) Changed?.Invoke();
    }

    private bool EnsureDefaults()
    {
        if (!IsReady || !ResourceManager.Instance ||
            !ResourceManager.Instance.TryGetDefaultLoadout(
                out string engineerId,
                out string raceId,
                out string spellSlot1Id,
                out string spellSlot2Id))
            return false;

        return userGlobalInfo.SetLoadoutSelection(
            string.IsNullOrWhiteSpace(userGlobalInfo.SelectedEngineerId)
                ? engineerId : userGlobalInfo.SelectedEngineerId,
            string.IsNullOrWhiteSpace(userGlobalInfo.SelectedRaceId)
                ? raceId : userGlobalInfo.SelectedRaceId,
            string.IsNullOrWhiteSpace(userGlobalInfo.SelectedSpellSlot1Id)
                ? spellSlot1Id : userGlobalInfo.SelectedSpellSlot1Id,
            string.IsNullOrWhiteSpace(userGlobalInfo.SelectedSpellSlot2Id)
                ? spellSlot2Id : userGlobalInfo.SelectedSpellSlot2Id);
    }
}
