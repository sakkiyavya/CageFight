using UnityEngine;

/// <summary>
/// 商店面板分页控制器：
/// 点击 1/2/3/4 页签切换 A/B/C/D 页面；
/// 每页商品超过 4 个（存在多页片）时自动显示该页的 L Reverse / R Reverse 左右翻页箭头，
/// 否则自动隐藏；翻页在该页的页片之间循环切换（每片 4 个商品位）。
/// 商品数量暂由 Inspector 配置（productsPerPage，按 A/B/C/D 顺序），
/// 商店数据接入后由数据侧写入真实数量即可。
/// </summary>
[DisallowMultipleComponent]
public sealed class ShopPageController : MonoBehaviour
{
    [SerializeField, Tooltip("A/B/C/D 四个页面根对象（与页签顺序对应）。")]
    private GameObject[] pages = new GameObject[0];
    [SerializeField, Tooltip("每页商品数量（A/B/C/D 顺序；每片显示 4 个，超过 4 时显示翻页箭头）。")]
    private int[] productsPerPage = new int[] { 0, 0, 0, 0 };
    [SerializeField, Tooltip("每页的左翻页箭头（与页面顺序对应，无箭头的页面留空）。")]
    private GameObject[] leftReverseButtons;
    [SerializeField, Tooltip("每页的右翻页箭头（与页面顺序对应，无箭头的页面留空）。")]
    private GameObject[] rightReverseButtons;

    private const int SheetCapacity = 4;          // 每页片商品位数量。
    private int _currentPage = -1;                // 当前打开的页面下标（0=A，1=B，2=C，3=D）。
    private readonly int[] _sheetIndex = new int[4];   // 每页当前所在的页片下标。

    private void OnEnable()
    {
        // 商店打开时定位默认页面：优先使用场景中已激活的页面（A），否则取第一页。
        if (_currentPage < 0)
        {
            _currentPage = 0;
            if (pages != null)
            {
                for (int i = 0; i < pages.Length; i++)
                {
                    if (pages[i] != null && pages[i].activeSelf)
                    {
                        _currentPage = i;
                        break;
                    }
                }
            }
        }

        RefreshArrows();
    }

    /// <summary>页签点击：打开指定页面（0=A，1=B，2=C，3=D），并刷新翻页箭头。</summary>
    /// <param name="pageIndex">目标页面下标。</param>
    public void OpenPage(int pageIndex)
    {
        if (pages == null || pageIndex < 0 || pageIndex >= pages.Length)
            return;

        _currentPage = pageIndex;

        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] != null)
                pages[i].SetActive(i == pageIndex);
        }

        if (_sheetIndex[pageIndex] >= SheetCount(pageIndex))
            _sheetIndex[pageIndex] = 0;

        RefreshArrows();
    }

    /// <summary>左翻页：切换到上一页片（循环）。</summary>
    public void FlipLeft()
    {
        Flip(-1);
    }

    /// <summary>右翻页：切换到下一页片（循环）。</summary>
    public void FlipRight()
    {
        Flip(1);
    }

    /// <summary>在当前页面内切换页片；单页片（商品不超过 4 个）时不响应。</summary>
    /// <param name="direction">翻页方向：-1 向左，1 向右。</param>
    private void Flip(int direction)
    {
        if (_currentPage < 0)
            return;

        int sheets = SheetCount(_currentPage);
        if (sheets <= 1)
            return;

        _sheetIndex[_currentPage] =
            ((_sheetIndex[_currentPage] + direction) % sheets + sheets) % sheets;

        // TODO: 商店商品数据接入后，按当前页片刷新 4 个商品位的内容。
    }

    /// <summary>计算指定页面的页片数量（每片 4 个商品位，至少 1 片）。</summary>
    /// <param name="pageIndex">页面下标。</param>
    /// <returns>该页的页片数量。</returns>
    private int SheetCount(int pageIndex)
    {
        if (productsPerPage == null || pageIndex < 0 || pageIndex >= productsPerPage.Length)
            return 1;

        int count = Mathf.Max(0, productsPerPage[pageIndex]);
        return Mathf.Max(1, (count + SheetCapacity - 1) / SheetCapacity);
    }

    /// <summary>按当前页面刷新左右翻页箭头：商品超过 4 个（多页片）才显示，否则隐藏。</summary>
    private void RefreshArrows()
    {
        bool show = _currentPage >= 0 && SheetCount(_currentPage) > 1;
        SetActiveAt(leftReverseButtons, show);
        SetActiveAt(rightReverseButtons, show);
    }

    /// <summary>设置指定数组中当前页面下标的对象激活状态（越界或空引用跳过）。</summary>
    /// <param name="targets">对象数组（与页面顺序对应）。</param>
    /// <param name="active">目标激活状态。</param>
    private void SetActiveAt(GameObject[] targets, bool active)
    {
        if (targets == null || _currentPage < 0 || _currentPage >= targets.Length)
            return;

        if (targets[_currentPage] != null)
            targets[_currentPage].SetActive(active);
    }
}