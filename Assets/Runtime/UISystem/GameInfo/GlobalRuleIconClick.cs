using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 全局规则图标的点击处理：
/// 挂在规则图标上（Setup 传入字幕内容）——点击弹出该规则的效果字幕，字幕打开时再点关闭；
/// 挂在全屏透明关闭层上（Setup 传入 null）——点击任意区域关闭字幕。
/// </summary>
public class GlobalRuleIconClick : MonoBehaviour, IPointerDownHandler
{
    private GlobalRuleIndicator _indicator;
    private string _description;

    /// <summary>绑定所属指示器与字幕内容；描述为空表示该对象只负责“点击关闭”。</summary>
    public void Setup(GlobalRuleIndicator indicator, string description)
    {
        _indicator = indicator;
        _description = description;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_indicator == null)
            return;

        if (string.IsNullOrEmpty(_description))
        {
            _indicator.ClosePopup();
            return;
        }

        if (_indicator.IsPopupOpen)
            _indicator.ClosePopup();   // 字幕已打开：再点一次图标 = 关闭。
        else
            _indicator.ShowPopup(_description);
    }
}
