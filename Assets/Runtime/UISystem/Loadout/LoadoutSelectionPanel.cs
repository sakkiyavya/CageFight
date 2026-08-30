using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 静态选装面板。面板和候选按钮均由场景预先配置，运行时只解析稳定 ID 并更新显示。
/// </summary>
[DisallowMultipleComponent]
public sealed class LoadoutSelectionPanel : UISystemBase
{
    [SerializeField] private PlayerLoadoutManager loadout;
    [SerializeField] private LoadoutSelectionKind kind;
    [SerializeField, Range(0, 1)] private int spellSlot;
    [SerializeField] private LoadoutSelectionOption[] options;
    [SerializeField, Min(1), Tooltip("每页最多显示的候选数量（不超过场景预置的选项槽数）；候选超过该值时启用左右翻页")]
    private int pageSize = 6;
    [SerializeField] private bool closeAfterSelect = true;
    [SerializeField] private MonoBehaviour engineerDetailCard;
    [SerializeField, Range(.05f, 1f)] private float unselectedAlpha = .35f;

    private Coroutine refreshRoutine;
    private int pageIndex;
    private Sprite checkSprite;

    /// <summary>当前面板负责的选装类别。</summary>
    public LoadoutSelectionKind Kind => kind;
    public PlayerLoadoutManager Loadout => loadout;

    /// <summary>当类别为法术时，对应两个可选法术槽中的哪一个。</summary>
    public int SpellSlot => spellSlot;

    public void SetSpellSlot(int value)
    {
        if (kind != LoadoutSelectionKind.Spell) return;
        spellSlot = Mathf.Clamp(value, 0, 1);
        Refresh();
    }

    public void SetCheckSprite(Sprite value)
    {
        checkSprite = value;
        Refresh();
    }

    public void PreviewEngineer(string definitionId)
    {
        if (kind != LoadoutSelectionKind.Engineer || !ResourceManager.Instance ||
            !(engineerDetailCard is IEngineerDetailCard card) ||
            !ResourceManager.Instance.TryGetEngineer(definitionId, out EngineerDefinition engineer)) return;
        card.Show(engineer);
    }

    public void PreviousPage()
    {
        pageIndex = Mathf.Max(0, pageIndex - 1);
        Refresh();
    }

    public void NextPage()
    {
        pageIndex = Mathf.Min(pageIndex + 1, PageCount() - 1);
        Refresh();
    }

    protected override void Awake()
    {
        base.Awake();
        if (!loadout)
            Debug.LogError("[LoadoutSelectionPanel] 请在 Inspector 指定 PlayerLoadoutManager。", this);
    }

    private void OnEnable()
    {
        if (!loadout) return;
        loadout.Changed += Refresh;
        StartRefreshRoutine();
    }

    private void OnDisable()
    {
        if (loadout) loadout.Changed -= Refresh;
        if (refreshRoutine != null) StopCoroutine(refreshRoutine);
        refreshRoutine = null;
    }

    /// <summary>
    /// 打开预置面板。法术面板可在打开时指定第 0 或第 1 个可选法术槽。
    /// </summary>
    public void Open(int requestedSpellSlot = -1)
    {
        if (kind == LoadoutSelectionKind.Spell && (uint)requestedSpellSlot < 2)
            spellSlot = requestedSpellSlot;

        if (!UIStack.Instance)
        {
            Debug.LogWarning("[LoadoutSelectionPanel] UIStack 未就绪。", this);
            return;
        }

        if (UIStack.Instance.Peek() != this) UIStack.Instance.Push(this);
        UIMotionEffect(true);
        StartRefreshRoutine();
    }

    /// <summary>由预置候选按钮调用，将该稳定 ID 写入当前选择槽。</summary>
    public void Select(string definitionId) => Select(definitionId, string.Empty);

    /// <summary>
    /// 由预置候选按钮调用，将稳定 ID 与其种族大本营预制体 Key 写入当前选择槽。
    /// 非种族选项会忽略 <paramref name="raceMainBasePrefabKey"/>。
    /// </summary>
    public void Select(string definitionId, string raceMainBasePrefabKey)
    {
        if (!loadout || !loadout.IsReady || string.IsNullOrWhiteSpace(definitionId)) return;

        bool changed = kind switch
        {
            LoadoutSelectionKind.Engineer =>
                ResourceManager.Instance &&
                ResourceManager.Instance.TryGetEngineer(definitionId, out EngineerDefinition engineer) &&
                loadout.SelectEngineer(engineer),
            LoadoutSelectionKind.Race =>
                ResourceManager.Instance &&
                ResourceManager.Instance.TryGetRace(definitionId, out RaceDefinition race) &&
                loadout.SelectRace(race, raceMainBasePrefabKey),
            LoadoutSelectionKind.Spell =>
                ResourceManager.Instance &&
                ResourceManager.Instance.TryGetSpell(definitionId, out SpellDefinition spell) &&
                loadout.SelectSpellSmart(spell),
            _ => false
        };

        if (!changed) return;
        Refresh();

        if (closeAfterSelect && UIStack.Instance && UIStack.Instance.Peek() == this)
            UIStack.Instance.Pop();
    }

    /// <summary>刷新候选图标、可用状态与已选中标记。</summary>
    public void Refresh()
    {
        if (options == null) return;

        if (!loadout || !loadout.IsReady || !ResourceManager.Instance)
        {
            foreach (LoadoutSelectionOption option in options)
                option?.SetPresentation(null, null, false, false, checkSprite, unselectedAlpha);
            return;
        }

        AssignCurrentPageIds();
        string selectedId = GetSelectedId();
        foreach (LoadoutSelectionOption option in options)
        {
            if (!option) continue;

            bool hasDefinition = !string.IsNullOrWhiteSpace(option.DefinitionId);
            option.gameObject.SetActive(hasDefinition);
            if (!hasDefinition) continue;

            bool available = TryGetIconKey(option.DefinitionId, out string iconKey);
            Sprite icon = available ? ResourceManager.Instance.GetSprite(iconKey) : null;
            Sprite frame = TryGetPortraitFrame(option.DefinitionId);
            // 法术类别：两个槽位各自的已选法术都显示勾选（两个“被选择项”）。
            bool isSelected = kind == LoadoutSelectionKind.Spell
                ? IsSpellSelected(option.DefinitionId)
                : option.DefinitionId == selectedId;
            option.SetPresentation(icon, frame, available && isSelected, available,
                checkSprite, unselectedAlpha);
        }
    }

    /// <summary>
    /// 法术是否已被任一可选槽位选中（局内第二/第三格都算）。
    /// </summary>
    private bool IsSpellSelected(string definitionId)
    {
        if (string.IsNullOrWhiteSpace(definitionId) || !loadout || !loadout.IsReady)
            return false;

        if (loadout.TryGetGameplaySpell(1, out SpellDefinition slot1) &&
            slot1.Id == definitionId)
            return true;

        if (loadout.TryGetGameplaySpell(2, out SpellDefinition slot2) &&
            slot2.Id == definitionId)
            return true;

        return false;
    }

    private void AssignCurrentPageIds()
    {
        List<string> ids = GetDefinitionIds();
        int capacity = PageCapacity();
        pageIndex = Mathf.Clamp(pageIndex, 0, Mathf.Max(0, Mathf.CeilToInt(ids.Count / (float)capacity) - 1));
        int first = pageIndex * capacity;
        for (int i = 0; i < options.Length; i++)
        {
            LoadoutSelectionOption option = options[i];
            if (!option) continue;
            // 超出每页容量的槽位清空（隐藏），避免同一候选在本页与下一页重复显示。
            bool inPage = i < capacity;
            int index = first + i;
            option.SetDefinitionId(inPage && index < ids.Count ? ids[index] : string.Empty);
        }
    }

    /// <summary>每页容量 = min(pageSize, 场景预置选项槽数)。</summary>
    private int PageCapacity()
    {
        int slots = options == null ? 0 : options.Length;
        return Mathf.Max(1, Mathf.Min(pageSize, slots));
    }

    private int PageCount()
    {
        int capacity = PageCapacity();
        return Mathf.Max(1, Mathf.CeilToInt(GetDefinitionIds().Count / (float)capacity));
    }

    private List<string> GetDefinitionIds()
    {
        var ids = new List<string>();
        if (!ResourceManager.Instance) return ids;
        switch (kind)
        {
            case LoadoutSelectionKind.Engineer:
                foreach (EngineerDefinition item in ResourceManager.Instance.EngineerDefinitions)
                    if (item && !string.IsNullOrWhiteSpace(item.Id)) ids.Add(item.Id);
                break;
            case LoadoutSelectionKind.Race:
                foreach (RaceDefinition item in ResourceManager.Instance.RaceDefinitions)
                    if (item && !string.IsNullOrWhiteSpace(item.Id)) ids.Add(item.Id);
                break;
            case LoadoutSelectionKind.Spell:
                // 工程师自带法术（各工程师 innateSpellId）为该单位专属、唯一的能力，
                // 不出现在法术选择面板候选列表中。
                var innateSpellIds = new List<string>();
                foreach (EngineerDefinition engineerDef in ResourceManager.Instance.EngineerDefinitions)
                    if (engineerDef && !string.IsNullOrWhiteSpace(engineerDef.InnateSpellId))
                        innateSpellIds.Add(engineerDef.InnateSpellId);

                foreach (SpellDefinition item in ResourceManager.Instance.SpellDefinitions)
                    if (item && !string.IsNullOrWhiteSpace(item.Id) && !innateSpellIds.Contains(item.Id))
                        ids.Add(item.Id);
                break;
        }
        return ids;
    }

    private void StartRefreshRoutine()
    {
        if (!isActiveAndEnabled || refreshRoutine != null) return;
        refreshRoutine = StartCoroutine(PreloadAndRefreshRoutine());
    }

    private IEnumerator PreloadAndRefreshRoutine()
    {
        while (loadout && !loadout.IsReady) yield return null;
        if (loadout) yield return loadout.PreloadPresentationResources();
        refreshRoutine = null;
        Refresh();
    }

    private string GetSelectedId()
    {
        switch (kind)
        {
            case LoadoutSelectionKind.Engineer:
                return loadout.TryGetSelectedEngineer(out EngineerDefinition engineer) ? engineer.Id : string.Empty;
            case LoadoutSelectionKind.Race:
                return loadout.TryGetSelectedRace(out RaceDefinition race) ? race.Id : string.Empty;
            case LoadoutSelectionKind.Spell:
                return loadout.TryGetGameplaySpell(spellSlot + 1, out SpellDefinition spell) ? spell.Id : string.Empty;
            default:
                return string.Empty;
        }
    }

    private bool TryGetIconKey(string definitionId, out string iconKey)
    {
        iconKey = string.Empty;
        switch (kind)
        {
            case LoadoutSelectionKind.Engineer:
                if (ResourceManager.Instance.TryGetEngineer(definitionId, out EngineerDefinition engineer))
                {
                    iconKey = engineer.IconKey;
                    return true;
                }
                break;
            case LoadoutSelectionKind.Race:
                if (ResourceManager.Instance.TryGetRace(definitionId, out RaceDefinition race))
                {
                    iconKey = race.IconKey;
                    return true;
                }
                break;
            case LoadoutSelectionKind.Spell:
                if (ResourceManager.Instance.TryGetSpell(definitionId, out SpellDefinition spell))
                {
                    iconKey = spell.IconKey;
                    return true;
                }
                break;
        }

        return false;
    }

    private Sprite TryGetPortraitFrame(string definitionId)
    {
        if (kind != LoadoutSelectionKind.Engineer || !ResourceManager.Instance ||
            !ResourceManager.Instance.TryGetEngineer(definitionId, out EngineerDefinition engineer)) return null;
        if (string.IsNullOrWhiteSpace(engineer.PortraitFrameKey)) return null;
        return ResourceManager.Instance.GetSprite(engineer.PortraitFrameKey);
    }
}

/// <summary>未来工程师数据卡实现此接口后，即可接收选择页的长按预览。</summary>
public interface IEngineerDetailCard
{
    void Show(EngineerDefinition engineer);
}

/// <summary>静态选装面板可编辑的类别。</summary>
public enum LoadoutSelectionKind
{
    Engineer,
    Race,
    Spell
}
