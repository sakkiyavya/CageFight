using UnityEngine;
using UnityEngine.EventSystems;

public class LevelButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private LevelConfig _config;

    public void Init(LevelConfig config)
    {
        _config = config;
    }

    public void OnPointerDown(PointerEventData eventData) { }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_config == null || SceneFSM.Instance == null) return;
        SceneFSM.Instance.BeginLevelLoad(_config);
    }
}
