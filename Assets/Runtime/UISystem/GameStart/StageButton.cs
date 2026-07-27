using UnityEngine;
using UnityEngine.EventSystems;

public class StageButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private StageConfig _config;

    public void Init(StageConfig config)
    {
        _config = config;
    }

    public void OnPointerDown(PointerEventData eventData) { }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_config == null || SceneFSM.Instance == null) return;
        SceneFSM.Instance.BeginStageLoad(_config);
    }
}
