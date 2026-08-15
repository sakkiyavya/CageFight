using UnityEngine;
using UnityEngine.EventSystems;

public sealed class LoadoutConfigurationPageButton : MonoBehaviour, IPointerClickHandler
{
    public enum Page { Engineer, Race, Spell, Close, PreviousCategory, NextCategory, PreviousOptions, NextOptions }

    [SerializeField] private LoadoutConfigurationCanvas canvas;
    [SerializeField] private Page page;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!canvas) return;
        switch (page)
        {
            case Page.Engineer: canvas.OpenEngineer(); break;
            case Page.Race: canvas.OpenRace(); break;
            case Page.Spell: canvas.OpenSpell(); break;
            case Page.PreviousCategory: canvas.PreviousCategory(); break;
            case Page.NextCategory: canvas.NextCategory(); break;
            case Page.PreviousOptions: canvas.PreviousOptions(); break;
            case Page.NextOptions: canvas.NextOptions(); break;
            default: canvas.Close(); break;
        }
    }
}
