using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>打开预置选装面板；法术入口可指定两个可选槽中的一个。</summary>
[DisallowMultipleComponent]
public sealed class LoadoutSelectionOpenButton : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private LoadoutSelectionPanel panel;
    [SerializeField, Range(0, 1)] private int spellSlot;
    [SerializeField] private bool nextEmptySpellSlot;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!panel)
        {
            Debug.LogWarning("[LoadoutSelectionOpenButton] 请在 Inspector 指定选装面板。", this);
            return;
        }

        if (!nextEmptySpellSlot || panel.Kind != LoadoutSelectionKind.Spell)
        {
            panel.Open(spellSlot);
            return;
        }

        PlayerLoadoutManager loadout = panel.Loadout;
        panel.Open(loadout && loadout.TryGetGameplaySpell(1, out _) ? 1 : 0);
    }
}
