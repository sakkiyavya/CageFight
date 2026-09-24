using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 图鉴面板控制器（Book Canvas）：
/// 页签切换派系、每页 12 格、L/R 翻页、未解锁格子显示 Unlock 覆盖、
/// 点击已解锁格子弹出 Card 并填充数据。解锁规则见 BookProgress。
/// </summary>
[DisallowMultipleComponent]
public sealed class BookController : MonoBehaviour
{
    [SerializeField] private BookCatalog catalog;
    [SerializeField] private GameObject[] slotObjects = new GameObject[0];
    [SerializeField] private Button[] tabButtons = new Button[0];
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;
    [SerializeField] private Transform detailCardRoot;
    [SerializeField] private string[] categoryIds = { "mouse", "cat", "chicken", "goat", "magic", "en" };
    [SerializeField] private bool debugShowAll;
    [SerializeField, Tooltip("已解锁但尚未拥有的条目头像颜色（灰白）")]
    private Color unownedColor = new Color(0.62f, 0.62f, 0.62f, 1f);

    private const int PageSize = 12;

    private int _categoryIndex;
    private int _pageIndex;
    private int _refreshToken;
    private BookEntry _selectedEntry;
    private readonly List<BookEntry> _currentEntries = new List<BookEntry>();
    private readonly List<Image> _slotImages = new List<Image>();
    private readonly List<GameObject> _slotUnlockOverlays = new List<GameObject>();
    private CardView _cardView;
    private bool _cardCached;

    private void Awake()
    {
        Debug.Log($"[Book诊断] Awake active={gameObject.activeSelf} scale={transform.localScale} catalog={(catalog != null ? catalog.name : "NULL")} slots={slotObjects.Length} tabs={tabButtons.Length} detailRoot={(detailCardRoot != null ? detailCardRoot.name : "NULL")}");
        BookProgress.RegisterCatalog(catalog);
        CacheSlots();

        for (int i = 0; i < tabButtons.Length; i++)
        {
            int index = i;
            Button button = tabButtons[i];
            if (button != null)
                button.onClick.AddListener(() => OpenCategory(index));
        }

        if (leftButton != null) leftButton.onClick.AddListener(FlipLeft);
        if (rightButton != null) rightButton.onClick.AddListener(FlipRight);
    }

    private void OnEnable()
    {
        // 防御：编辑器里若把面板根缩成 0（收纳面板的常见操作），打开时强制恢复 1，
        // 否则整块 UI 会被压成一个点、看起来一片空白。
        transform.localScale = Vector3.one;
        BookProgress.RegisterCatalog(catalog);
        _categoryIndex = 0;
        _pageIndex = 0;
        CloseDetail();
        Refresh();
        Debug.Log($"[Book诊断] OnEnable scale={transform.localScale} catalog={(catalog != null ? catalog.name : "NULL")} 条目总数={(catalog != null ? catalog.Entries.Count : 0)} mouse分类数={(catalog != null ? catalog.GetEntriesByCategory("mouse").Count : 0)}");
    }

    /// <summary>页签点击：切换派系（下标对应 categoryIds）。</summary>
    public void OpenCategory(int index)
    {
        if (categoryIds == null || index < 0 || index >= categoryIds.Length)
            return;

        _categoryIndex = index;
        _pageIndex = 0;
        CloseDetail();
        Refresh();
    }

    /// <summary>左翻页。</summary>
    public void FlipLeft()
    {
        if (_pageIndex <= 0)
            return;

        _pageIndex--;
        CloseDetail();
        Refresh();
    }

    /// <summary>右翻页。</summary>
    public void FlipRight()
    {
        if (_pageIndex >= PageCount() - 1)
            return;

        _pageIndex++;
        CloseDetail();
        Refresh();
    }

    /// <summary>格子点击：已解锁则弹出/切换 Card。</summary>
    public void OnSlotClicked(int slotIndex)
    {
        Debug.Log($"[Book诊断] OnSlotClicked 卡位{slotIndex} 本类条目={_currentEntries.Count} 页={_pageIndex}");
        int entryIndex = _pageIndex * PageSize + slotIndex;
        if (entryIndex < 0 || entryIndex >= _currentEntries.Count)
            return;

        BookEntry entry = _currentEntries[entryIndex];
        if (entry == null || (!debugShowAll && !BookProgress.IsUnlocked(entry)))
            return;

        if (_selectedEntry == entry)
        {
            CloseDetail();
            return;
        }

        ShowDetail(entry);
    }

    private void CacheSlots()
    {
        _slotImages.Clear();
        _slotUnlockOverlays.Clear();

        for (int i = 0; i < slotObjects.Length; i++)
        {
            GameObject slot = slotObjects[i];
            if (slot == null)
            {
                _slotImages.Add(null);
                _slotUnlockOverlays.Add(null);
                continue;
            }

            Image img = slot.GetComponent<Image>();
            if (img == null)
                img = slot.GetComponentInChildren<Image>(true);
            _slotImages.Add(img);

            Transform overlay = slot.transform.Find("Unlock");
            if (overlay != null)
            {
                _slotUnlockOverlays.Add(overlay.gameObject);

                // 锁覆盖与格子同尺寸、居中：锁是格子的子物体，尺寸按格子的 sizeDelta 对齐，
                // 父级缩放对两者一致，视觉上正好整格覆盖。
                RectTransform slotRect = slot.GetComponent<RectTransform>();
                RectTransform overlayRect = overlay.GetComponent<RectTransform>();
                if (slotRect != null && overlayRect != null)
                {
                    overlayRect.sizeDelta = slotRect.sizeDelta;
                    overlayRect.anchoredPosition = Vector2.zero;
                }
            }
            else
            {
                _slotUnlockOverlays.Add(null);
            }

            Button button = slot.GetComponent<Button>();
            if (button != null)
            {
                int slotIndex = i;
                button.onClick.AddListener(() => OnSlotClicked(slotIndex));
            }
        }
    }

    private void Refresh()
    {
        _refreshToken++;

        _currentEntries.Clear();
        if (catalog != null && categoryIds != null && _categoryIndex >= 0 && _categoryIndex < categoryIds.Length)
            _currentEntries.AddRange(catalog.GetEntriesByCategory(categoryIds[_categoryIndex]));

        // 同一派系内按星级升序排列（1 星在前）；数据侧编辑顺序不影响最终展示。
        _currentEntries.Sort((a, b) => a.star.CompareTo(b.star));

        int pageStart = _pageIndex * PageSize;
        Debug.Log($"[Book诊断] Refresh 分类={(_categoryIndex < categoryIds.Length ? categoryIds[_categoryIndex] : "?")} 页={_pageIndex} 本类条目={_currentEntries.Count} slotImages={_slotImages.Count} overlays={_slotUnlockOverlays.Count}");
        for (int i = 0; i < PageSize; i++)
        {
            if (i >= _slotImages.Count)
                break;

            int entryIndex = pageStart + i;
            bool hasEntry = entryIndex < _currentEntries.Count;
            BookEntry entry = hasEntry ? _currentEntries[entryIndex] : null;
            bool unlocked = hasEntry && entry != null && (debugShowAll || BookProgress.IsUnlocked(entry));

            if (_slotUnlockOverlays[i] != null)
                _slotUnlockOverlays[i].SetActive(hasEntry && !unlocked);

            if (_slotImages[i] != null)
            {
                _slotImages[i].enabled = unlocked;
                if (unlocked)
                {
                    // 新规则：已解锁但尚未拥有该兵种 → 头像显示为灰白色；获得后恢复原色。
                    _slotImages[i].color = BookProgress.IsOwned(entry) ? Color.white : unownedColor;
                    ApplySlotIcon(i, entry);
                }
            }
        }
    }

    private void ApplySlotIcon(int slotIndex, BookEntry entry)
    {
        if (entry == null)
            return;

        // 优先按资源键经框架加载（构建环境唯一路径）；编辑器下键为空时用拖拽预览图。
        if (!string.IsNullOrEmpty(entry.avatarSpriteKey) && ResourceManager.Instance != null)
        {
            int token = _refreshToken;
            Debug.Log($"[Book诊断] ApplySlotIcon 卡位{slotIndex} key={entry.avatarSpriteKey} 异步加载");
            ResourceManager.Instance.LoadExtraResourceAsync<Sprite>(entry.avatarSpriteKey, sprite =>
            {
                Debug.Log($"[Book诊断] 头像回调 卡位{slotIndex} sprite={(sprite != null ? sprite.name : "NULL")} tokenOK={token == _refreshToken}");
                if (sprite == null || token != _refreshToken)
                    return;

                if (slotIndex >= 0 && slotIndex < _slotImages.Count && _slotImages[slotIndex] != null)
                    _slotImages[slotIndex].sprite = sprite;
            });
            return;
        }

        Sprite fallback = entry.AvatarEditorFallback;
        Debug.Log($"[Book诊断] ApplySlotIcon 卡位{slotIndex} 编辑器回落 sprite={(fallback != null ? fallback.name : "NULL")}");
        if (fallback != null && slotIndex >= 0 && slotIndex < _slotImages.Count && _slotImages[slotIndex] != null)
            _slotImages[slotIndex].sprite = fallback;
    }

    private void ShowDetail(BookEntry entry)
    {
        Debug.Log($"[Book诊断] ShowDetail entry={entry.id} detailRoot={(detailCardRoot != null ? detailCardRoot.name : "NULL")}");
        _selectedEntry = entry;

        bool cardWasActive = detailCardRoot != null && detailCardRoot.gameObject.activeSelf;

        if (detailCardRoot != null)
            detailCardRoot.gameObject.SetActive(true);

        if (!_cardCached)
        {
            _cardCached = true;
            if (detailCardRoot != null)
                _cardView = detailCardRoot.GetComponentInChildren<CardView>(true);
        }

        if (_cardView != null)
        {
            if (cardWasActive)
                _cardView.PlaySwitch(entry);   // 换卡：翻页式切换动画。
            else
                _cardView.PlayEnter(entry);    // 首次弹出：弹性翻卡入场 + 内部错峰弹入。
        }
    }

    private void CloseDetail()
    {
        _selectedEntry = null;

        if (_cardView != null && detailCardRoot != null && detailCardRoot.gameObject.activeSelf)
        {
            Transform root = detailCardRoot;
            _cardView.PlayExit(() =>
            {
                // 退场动画结束后才真正关闭；期间若又点了新卡（_selectedEntry 已变）则保留激活。
                if (root != null && _selectedEntry == null)
                    root.gameObject.SetActive(false);
            });
        }
        else if (detailCardRoot != null)
        {
            detailCardRoot.gameObject.SetActive(false);
        }
    }

    private int PageCount()
    {
        return Mathf.Max(1, (_currentEntries.Count + PageSize - 1) / PageSize);
    }
}