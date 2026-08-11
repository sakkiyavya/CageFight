using TMPro;
using UnityEngine;

/// <summary>
/// 通过玩家全局信息事件刷新当前物体上的钻石数量文本。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TextMeshProUGUI))]
public sealed class DiamondInfoUI : MonoBehaviour
{
    private TextMeshProUGUI _text;
    private UserGlobalInfo _subscribedInfo;

    #region 生命周期与事件订阅
    private void Awake()
    {
        _text = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        SubscribeAndRefresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }
    #endregion

    #region 内部辅助
    private void SubscribeAndRefresh()
    {
        Unsubscribe();

        _subscribedInfo = UserGlobalInfo.Instance;
        if (_subscribedInfo == null)
        {
            Debug.LogError("[DiamondInfoUI] UserGlobalInfo.Instance 为空，请确认场景中已挂载玩家全局信息单例。", this);
            return;
        }

        _subscribedInfo.Changed += RefreshText;
        RefreshText();
    }

    private void Unsubscribe()
    {
        if (_subscribedInfo != null)
        {
            _subscribedInfo.Changed -= RefreshText;
        }

        _subscribedInfo = null;
    }

    private void RefreshText()
    {
        if (_text == null || _subscribedInfo == null)
        {
            return;
        }

        _text.text = _subscribedInfo.DiamondCount.ToString();
    }
    #endregion
}
