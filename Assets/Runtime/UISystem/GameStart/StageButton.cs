using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StageButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public Image icon;
    private StageConfig _config;    // 当前按钮对应的关卡配置。

    /// <summary>某关卡按钮被按下选中时触发（供共享选中视觉订阅，如标记与黄光）。</summary>
    public static event Action<StageButton> Selected;

    /// <summary>按钮自身的 RectTransform（用于共享选中视觉定位）。</summary>
    public RectTransform ButtonRect => (RectTransform)transform;

    /// <summary>关卡图标位（icon）的 RectTransform。</summary>
    public RectTransform IconRect => icon != null ? icon.rectTransform : null;

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
    /// 按下同时广播“选中”事件，供共享标记/黄光等视觉反馈使用。
    /// </summary>
    /// <param name="eventData">本次按下事件的指针数据。</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        Selected?.Invoke(this);
    }

    /// <summary>
    /// 指针抬起时不直接进入关卡：选择与开始分离，
    /// 进入关卡的请求由“GO”按钮调用 <see cref="StartStage"/> 统一发起。
    /// </summary>
    /// <param name="eventData">本次抬起事件的指针数据；当前实现不读取其中的具体值。</param>
    public void OnPointerUp(PointerEventData eventData) { }

    /// <summary>是否已绑定有效关卡配置。</summary>
    public bool HasConfig => _config != null;

    /// <summary>当前按钮绑定的关卡配置（未绑定时为 null）。</summary>
    public StageConfig Config => _config;

    /// <summary>请求进入本按钮绑定的关卡（由 GO 按钮在玩家确认后调用）。</summary>
    public void StartStage()
    {
        if (_config == null || SceneFSM.Instance == null)
            return;

        SceneFSM.Instance.BeginStageLoad(_config);
    }
    #endregion
}
