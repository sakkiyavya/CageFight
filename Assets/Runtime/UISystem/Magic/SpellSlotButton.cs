using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>法术栏单格的 EventSystem 输入桥接；槽位和法术栏均在 Inspector 配置。</summary>
[DisallowMultipleComponent]
public sealed class SpellSlotButton : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private GameplaySpellBar spellBar;
    [SerializeField, Range(0, 2)] private int slotIndex;

    private bool aiming;

    public void OnPointerDown(PointerEventData eventData)
    {
        aiming = spellBar && spellBar.BeginAim(slotIndex, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (aiming) spellBar.UpdateAim(slotIndex, eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (aiming)
        {
            spellBar.UpdateAim(slotIndex, eventData.position);
            spellBar.ReleaseAim(slotIndex);
        }
        else if (spellBar)
        {
            spellBar.Cast(slotIndex);
        }

        aiming = false;
    }

    private void OnDisable()
    {
        if (aiming && spellBar) spellBar.CancelAim();
        aiming = false;
    }
}
