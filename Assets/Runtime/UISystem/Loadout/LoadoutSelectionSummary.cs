using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>主菜单已选工程师、种族和两个可选法术的静态图标显示。</summary>
[DisallowMultipleComponent]
public sealed class LoadoutSelectionSummary : MonoBehaviour
{
    [SerializeField] private PlayerLoadoutManager loadout;
    [SerializeField] private Image engineerIcon;
    [SerializeField] private Image raceIcon;
    [SerializeField] private Image spellSlot1Icon;
    [SerializeField] private Image spellSlot2Icon;

    private Coroutine refreshRoutine;

    private void Awake()
    {
        if (!loadout)
            Debug.LogError("[LoadoutSelectionSummary] 请在 Inspector 指定 PlayerLoadoutManager。", this);
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

    /// <summary>刷新所有已选项图标。</summary>
    public void Refresh()
    {
        if (!loadout || !loadout.IsReady || !ResourceManager.Instance)
        {
            SetIcon(engineerIcon, null);
            SetIcon(raceIcon, null);
            SetIcon(spellSlot1Icon, null);
            SetIcon(spellSlot2Icon, null);
            return;
        }

        SetIcon(
            engineerIcon,
            loadout.TryGetSelectedEngineer(out EngineerDefinition engineer)
                ? ResourceManager.Instance.GetSprite(engineer.IconKey) : null);
        SetIcon(
            raceIcon,
            loadout.TryGetSelectedRace(out RaceDefinition race)
                ? ResourceManager.Instance.GetSprite(race.IconKey) : null);
        SetIcon(
            spellSlot1Icon,
            loadout.TryGetGameplaySpell(1, out SpellDefinition spell1)
                ? ResourceManager.Instance.GetSprite(spell1.IconKey) : null);
        SetIcon(
            spellSlot2Icon,
            loadout.TryGetGameplaySpell(2, out SpellDefinition spell2)
                ? ResourceManager.Instance.GetSprite(spell2.IconKey) : null);
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

    private static void SetIcon(Image image, Sprite sprite)
    {
        if (!image) return;
        image.preserveAspect = true;
        image.sprite = sprite;
        image.color = sprite ? Color.white : Color.clear;
    }
}
