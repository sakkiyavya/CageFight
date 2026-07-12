using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 将关卡配置列表按分页网格布局到子按钮上。
/// pageSize 控制每页列数(x)×行数(y)，spacing 控制间距。
/// 锚点为 Top，Y 向下排列（均为负值）。
/// </summary>
public class LevelButtonLayout : MonoBehaviour
{
    [SerializeField] private Vector2Int pageSize = new Vector2Int(3, 3);
    [SerializeField] private Vector2 spacing = new Vector2(200f, 200f);
    [SerializeField] private GameObject buttonPrefab;

    public List<LevelConfig> configs = new List<LevelConfig>();

    private int _currentPage;
    private readonly List<GameObject> _buttons = new List<GameObject>();

#if UNITY_EDITOR
    private Vector2Int _cachedPageSize;
    private Vector2    _cachedSpacing;

    private void Update()
    {
        if (pageSize != _cachedPageSize || spacing != _cachedSpacing)
        {
            _cachedPageSize = pageSize;
            _cachedSpacing  = spacing;
            LayoutButtons();
        }
    }
#endif

    private int PageCapacity => pageSize.x * pageSize.y;
    private int TotalPages   => Mathf.Max(1, Mathf.CeilToInt((float)configs.Count / PageCapacity));

    public void LayoutButtons()
    {
        Debug.Log("关卡按钮重新布局");
        // 清理旧按钮
        foreach (var btn in _buttons) Destroy(btn);
        _buttons.Clear();

        int start = _currentPage * PageCapacity;
        int end   = Mathf.Min(start + PageCapacity, configs.Count);

        for (int i = start; i < end; i++)
        {
            int localIndex = i - start;
            int col = localIndex % pageSize.x;
            int row = localIndex / pageSize.x;

            var go  = Instantiate(buttonPrefab, transform);
            var rt  = go.GetComponent<RectTransform>();
            float centerOffsetX = (pageSize.x - 1) * spacing.x * 0.5f;
            float localX = col * spacing.x - centerOffsetX;
            rt.anchoredPosition = new Vector2(localX, -row * spacing.y);

            go.GetComponent<LevelButton>().Init(configs[i]);
            go.SetActive(true);
            // go.GetComponent<Text>().text = (i + 1).ToString();
            _buttons.Add(go);
        }
    }

    /// <summary>翻页。isNext=true 下一页，false 上一页；边界时无效。</summary>
    public void TurnPage(bool isNext)
    {
        int target = _currentPage + (isNext ? 1 : -1);
        if (target < 0 || target >= TotalPages) return;
        _currentPage = target;
        LayoutButtons();
    }

    private void OnValidate()
    {
        pageSize.x = Mathf.Max(1, pageSize.x);
        pageSize.y = Mathf.Max(1, pageSize.y);
    }
}
