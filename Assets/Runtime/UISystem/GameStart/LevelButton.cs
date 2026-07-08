using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 关卡选择按钮。由 LevelButtonLayout 调用 Init() 注入配置，
/// 点击后启动对应关卡的加载流程。
/// </summary>
public class LevelButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private LevelConfig _config;

    public void Init(LevelConfig config)
    {
        _config = config;
        // 可在此更新按钮显示（关卡名、图标等）
    }

    public void OnPointerDown(PointerEventData eventData) { }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_config == null || StageLoader.Instance == null) return;
        StageLoader.Instance.StartLoad(_config);
    }
}
