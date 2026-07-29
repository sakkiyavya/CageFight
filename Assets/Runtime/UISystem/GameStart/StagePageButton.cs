using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Page button. isNext=true turns to the next page; false turns to the previous page.
/// </summary>
public class StagePageButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public bool isNext;                                   // 是否翻到下一页；否则翻到上一页。
    [SerializeField] private StageConfigLoader loader;    // 实际管理关卡分页和按钮刷新的加载器。

    #region 生命周期与回调
    /// <summary>
    /// 接收翻页按钮按下事件；实际翻页在抬起时执行。
    /// </summary>
    /// <param name="eventData">本次按下事件的指针数据。</param>
    public void OnPointerDown(PointerEventData eventData) { }

    /// <summary>
    /// 指针抬起时校验加载器引用，并按按钮方向请求翻页。
    /// </summary>
    /// <param name="eventData">本次抬起事件的指针数据；当前实现不读取其中的具体值。</param>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (loader == null)
        {
            Debug.LogWarning("[StagePageButton] StageConfigLoader 未配置！", this);
            return;
        }

        loader.TurnPage(isNext);
    }
    #endregion
}
