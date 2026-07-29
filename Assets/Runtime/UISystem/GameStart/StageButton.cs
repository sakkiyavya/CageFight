using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StageButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public Image icon;
    private StageConfig _config;    // 当前按钮对应的关卡配置。

    #region 公开接口
    /// <summary>
    /// 绑定当前按钮代表的关卡配置；传入空值时按钮不会发起加载。
    /// </summary>
    /// <param name="config">当前分页位置对应的关卡配置。</param>
    public void Init(StageConfig config)
    {
        _config = config;
        if(icon && _config != null && _config.icon != null)
        {
            icon.sprite = config.icon;
            icon.color = Color.white;
        }else
            icon.color = new Color(0,0,0,0);
    }
    #endregion

    #region 生命周期与回调
    /// <summary>
    /// 接收关卡按钮按下事件；实际进入关卡的请求在抬起时执行。
    /// </summary>
    /// <param name="eventData">本次按下事件的指针数据。</param>
    public void OnPointerDown(PointerEventData eventData) { }

    /// <summary>
    /// 指针抬起时，在配置和状态机均有效的情况下请求加载当前关卡。
    /// </summary>
    /// <param name="eventData">本次抬起事件的指针数据；当前实现不读取其中的具体值。</param>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (_config == null || SceneFSM.Instance == null) return;
        SceneFSM.Instance.BeginStageLoad(_config);
    }
    #endregion
}
