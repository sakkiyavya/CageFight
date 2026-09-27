using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 选关界面的全局规则按钮：点击切换本局全局规则，选中时按钮亮起、未选中变暗。
/// 走 EventSystem（IPointerDownHandler）；显示状态由 RefreshVisual 维护。
/// </summary>
public class GlobalRuleButton : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private GlobalRuleId ruleId = GlobalRuleId.Strengthen1;
    [SerializeField] private Color litColor = Color.white;                     // 选中亮起色。
    [SerializeField] private Color dimColor = new Color(0.55f, 0.55f, 0.55f, 1f);   // 未选中变暗色。

    private Image _image;

    private void Awake()
    {
        _image = GetComponent<Image>();
    }

    private void OnEnable()
    {
        RefreshVisual();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        GlobalRuleManager.Toggle(ruleId);
        RefreshVisual();
    }

    /// <summary>按当前选中状态刷新按钮亮/暗。</summary>
    public void RefreshVisual()
    {
        if (_image != null)
            _image.color = GlobalRuleManager.IsEnabled(ruleId) ? litColor : dimColor;
    }
}
