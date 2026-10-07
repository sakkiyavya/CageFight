using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Page button. isNext=true turns to the next page; false turns to the previous page.
/// </summary>
public class StagePageButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public bool isNext;                                   // 是否翻到下一页；否则翻到上一页。
    [SerializeField] private StageConfigLoader loader;    // 实际管理关卡分页和按钮刷新的加载器。

    #region 翻页功能（测试用，可整体删除）
    // 本区块属于「关卡翻页（测试用）」功能的一部分。回滚方式：
    //   删除本区块 + StageConfigLoader 里的同名区块 + StageSelectSelection 里标注了
    //   「翻页功能（测试用）」的几行即可，其余代码不受影响。
    private const float UnavailableAlpha = 0.3f;   // 该方向没有可翻的页时，箭头的透明度。
    private Image _arrowImage;                     // 箭头自身的 Image（用于显示可翻页状态）。
    private Color _arrowColor;                     // 箭头未被压暗时的原始颜色。
    private bool _arrowColorCached;                // 原始颜色是否已经缓存过。

    /// <summary>
    /// 运行时绑定：由 StageConfigLoader 启动时自动调用，替代在场景里手工拖 loader 引用
    /// （箭头在场景里只是一张 Image，没有挂任何脚本）。
    /// </summary>
    /// <param name="owner">负责分页的关卡加载器。</param>
    /// <param name="next"><see langword="true"/> 表示下一页，<see langword="false"/> 表示上一页。</param>
    public void Bind(StageConfigLoader owner, bool next)
    {
        loader = owner;
        isNext = next;
    }

    /// <summary>
    /// 按「这个方向还能不能翻页」压暗或还原箭头，让测试时一眼看出还有没有下一页。
    /// </summary>
    /// <param name="available">该方向是否还有可翻的页。</param>
    public void SetAvailable(bool available)
    {
        if (_arrowImage == null) _arrowImage = GetComponent<Image>();
        if (_arrowImage == null) return;

        if (!_arrowColorCached)
        {
            _arrowColor = _arrowImage.color;
            _arrowColorCached = true;
        }

        Color color = _arrowColor;
        if (!available) color.a *= UnavailableAlpha;
        _arrowImage.color = color;
    }
    #endregion

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
