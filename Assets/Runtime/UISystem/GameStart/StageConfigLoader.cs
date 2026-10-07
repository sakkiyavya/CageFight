using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Serialization;

/// <summary>
/// Loads stage configurations in order through Addressables and assigns the
/// configurations for the current page to the seven pre-positioned buttons.
/// </summary>
public class StageConfigLoader : MonoBehaviour
{
    private const int ButtonsPerPage = 7;                                                               // 每页固定显示的关卡按钮数量。

    [Tooltip("The seven stage buttons, in display order.")]
    [FormerlySerializedAs("levelButtons")]
    [SerializeField] private StageButton[] stageButtons = new StageButton[ButtonsPerPage];              // 按显示顺序排列的七个关卡按钮。

    [Tooltip("七个关卡的编号文本（显示顺序与 stageButtons 一致）：按整条关卡序列编号，如 1-1、1-8。")]
    [SerializeField] private TextMeshProUGUI[] stageNumberTexts = new TextMeshProUGUI[ButtonsPerPage];  // 关卡编号文本，翻页时自动更新。

    [Tooltip("关卡编号文本的章节前缀：第 8 关显示为 1-8，翻到第几页都不会变成 2-1。")]
    [SerializeField] private string stageNumberChapter = "1";                                            // 关卡编号前半部分的章节号。

    private readonly List<AsyncOperationHandle> _handles = new List<AsyncOperationHandle>();            // 已成功加载、销毁时需要释放的 Addressables 句柄。
    private readonly List<StageConfig> _configs = new List<StageConfig>();                              // 按关卡编号顺序加载的配置列表。
    private int _currentPage;                                                                           // 当前显示页的零基索引。
    private Coroutine _loading;
    private AsyncOperationHandle _pending;
    private bool _allLoaded;

    private int TotalPages => Mathf.Max(1, Mathf.CeilToInt((float)_configs.Count / ButtonsPerPage));    // 根据已加载配置计算的总页数。

    #region 翻页功能（测试用，可整体删除）
    // 目的：关卡选择面板上原本就有左右两个黄色箭头（Center 下的 L / R），但它们只是 Image，
    // 没有挂任何脚本，所以第 8、9 关（第 2 页）点不到。这里在运行时给箭头挂上
    // StagePageButton 并指定方向，无需改动场景连线即可翻页。
    // 回滚方式：删除本区块 + StagePageButton 里的同名区块 +
    //           StageSelectSelection 里标注了「翻页功能（测试用）」的几行，其余代码不受影响。
    //           （注意：只回滚本区块、但 Stage9 仍注册在 Addressables 里的话，
    //             第 8、9 关会重新变成翻不到、也点不到的状态。）
    [Header("翻页（测试用）")]
    [Tooltip("启动时自动把面板上的左右箭头接成翻页按钮；关掉则完全按原样运行。")]
    [SerializeField] private bool autoBindPageArrows = true;

    [Tooltip("左右箭头所在的父对象；留空则使用本组件的父对象（关卡选择面板 Center）。")]
    [SerializeField] private Transform pageArrowRoot;

    [Tooltip("上一页箭头的对象名（对应场景里 Center 下的 L）。")]
    [SerializeField] private string previousArrowName = "L";

    [Tooltip("下一页箭头的对象名（对应场景里 Center 下的 R）。")]
    [SerializeField] private string nextArrowName = "R";

    /// <summary>翻页完成后触发（测试用翻页功能）：供选中标记等视觉做清理。</summary>
    public static event System.Action PageTurned;

    /// <summary>当前页码，零基（测试用翻页功能）。</summary>
    public int CurrentPage => _currentPage;

    /// <summary>总页数（测试用翻页功能）。</summary>
    public int PageCount => TotalPages;

    private StagePageButton _previousArrow;   // 上一页箭头，运行时自动绑定。
    private StagePageButton _nextArrow;       // 下一页箭头，运行时自动绑定。
    private bool _pageArrowsBound;            // 是否已经绑定过，避免重复挂组件。

    /// <summary>该方向是否还有可翻的页（测试用翻页功能）。</summary>
    /// <param name="isNext"><see langword="true"/> 表示下一页方向。</param>
    /// <returns>还有可翻的页时为 <see langword="true"/>。</returns>
    public bool CanTurnPage(bool isNext)
    {
        int target = _currentPage + (isNext ? 1 : -1);
        return target >= 0 && target < TotalPages;
    }

    /// <summary>
    /// 运行时把面板上的左右箭头接成翻页按钮：箭头本身只是 Image，
    /// 这里给它挂 StagePageButton 并指定方向；找不到箭头时只警告，不影响原有选关流程。
    /// </summary>
    private void BindPageArrows()
    {
        if (!autoBindPageArrows || _pageArrowsBound) return;
        _pageArrowsBound = true;

        Transform root = pageArrowRoot != null ? pageArrowRoot : transform.parent;
        if (root == null) return;

        _previousArrow = AttachArrow(root, previousArrowName, false);
        _nextArrow = AttachArrow(root, nextArrowName, true);

        if (_previousArrow == null && _nextArrow == null)
        {
            Debug.LogWarning(
                $"[StageConfigLoader] 没找到翻页箭头（{previousArrowName} / {nextArrowName}），翻页按钮未启用。", this);
        }

        RefreshPageArrowState();
    }

    /// <summary>在 root 的直接子对象里按名字找箭头，挂上 StagePageButton 并指定翻页方向。</summary>
    /// <param name="root">箭头所在的父对象。</param>
    /// <param name="arrowName">箭头对象名。</param>
    /// <param name="isNext">该箭头是否表示下一页。</param>
    /// <returns>绑定好的翻页按钮；没找到同名子对象时为 <see langword="null"/>。</returns>
    private StagePageButton AttachArrow(Transform root, string arrowName, bool isNext)
    {
        if (string.IsNullOrEmpty(arrowName)) return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (!string.Equals(child.name.Trim(), arrowName.Trim(), System.StringComparison.OrdinalIgnoreCase))
                continue;

            StagePageButton button = child.GetComponent<StagePageButton>();
            if (button == null) button = child.gameObject.AddComponent<StagePageButton>();

            button.Bind(this, isNext);
            return button;
        }

        return null;
    }

    /// <summary>按当前页刷新两个箭头：该方向没有可翻的页了就压暗。</summary>
    private void RefreshPageArrowState()
    {
        if (_previousArrow != null) _previousArrow.SetAvailable(CanTurnPage(false));
        if (_nextArrow != null) _nextArrow.SetAvailable(CanTurnPage(true));
    }
    #endregion

    #region 生命周期与回调
    /// <summary>
    /// 在异步配置加载前先刷新按钮，使尚无配置的按钮保持隐藏；
    /// 同时把左右箭头接成翻页按钮（测试用翻页功能）。
    /// </summary>
    private void Awake()
    {
        RefreshButtons();
        BindPageArrows();
    }

    /// <summary>
    /// 启动按编号顺序加载关卡配置的协程。
    /// </summary>
    private void OnEnable()
    {
        RefreshButtons();
        if (!_allLoaded && _loading == null) _loading = StartCoroutine(LoadConfigs());
    }

    private void OnDisable()
    {
        if (_loading != null) StopCoroutine(_loading);
        _loading = null;
        // A disabled hierarchy stops coroutines, but does not release Addressables handles.
        ReleasePending();
    }

    private void ReleasePending()
    {
        if (_pending.IsValid()) Addressables.Release(_pending);
        _pending = default;
    }
    #endregion

    #region 特效与协程
    /// <summary>
    /// 按 Stage1、Stage2 等连续地址逐个加载关卡配置，先查询地址是否存在，
    /// 遇到第一个不存在的地址后停止，避免 Addressables 输出 InvalidKeyException。
    /// </summary>
    /// <returns>逐个等待 Addressables 加载操作完成的协程。</returns>
    private IEnumerator LoadConfigs()
    {
        for (int i = _configs.Count + 1; ; i++)
        {
            string key = $"Stage{i}";

            // 先查询资源位置。Key 不存在时返回空列表，不会触发 InvalidKeyException。
            var locationsHandle = Addressables.LoadResourceLocationsAsync(
                key,
                typeof(StageConfig));
            _pending = locationsHandle;

            yield return locationsHandle;

            bool queried = locationsHandle.Status == AsyncOperationStatus.Succeeded;
            bool exists = queried &&
                          locationsHandle.Result != null &&
                          locationsHandle.Result.Count > 0;

            if (!queried)
                Debug.LogError($"[StageConfigLoader] 查询关卡失败：{key}\n{locationsHandle.OperationException}");
            ReleasePending();

            // 遇到第一个断续关卡，认为前面的配置就是全部关卡。
            if (!exists)
            {
                _allLoaded = queried && _configs.Count > 0;
                if (queried && _configs.Count == 0)
                    Debug.LogError("[StageConfigLoader] 远程目录中没有 Stage1，无法显示可选关卡。请重新构建并发布 Addressables。");
                break;
            }

            var assetHandle = Addressables.LoadAssetAsync<StageConfig>(key);
            _pending = assetHandle;
            yield return assetHandle;

            if (assetHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError(
                    $"关卡配置加载失败：{key}\n{assetHandle.OperationException}");

                ReleasePending();
                break;
            }

            _handles.Add(assetHandle);
            _pending = default; // Ownership moves to _handles until this loader is destroyed.
            _configs.Add(assetHandle.Result);
            RefreshButtons();

            // 预载该关导游对话资源（头像精灵 + 音频，幂等）：首次点击关卡即可直接弹出。
            GuideDialoguePlayer.Preload(assetHandle.Result);
        }

        _currentPage = Mathf.Clamp(_currentPage, 0, TotalPages - 1);
        RefreshButtons();
        _loading = null;
    }
    #endregion

    #region 公开接口
    /// <summary>
    /// 在有效页码范围内向前或向后翻一页，并刷新各按钮绑定的关卡配置和显示状态。
    /// </summary>
    /// <param name="isNext"><see langword="true"/> 表示下一页，<see langword="false"/> 表示上一页。</param>
    public void TurnPage(bool isNext)
    {
        int targetPage = _currentPage + (isNext ? 1 : -1);                                              // 请求进入的页码。
        if (targetPage < 0 || targetPage >= TotalPages) return;

        _currentPage = targetPage;
        RefreshButtons();
        PageTurned?.Invoke();   // 翻页功能（测试用）：通知选中标记等视觉做清理。
    }
    #endregion

    #region 内部辅助
    /// <summary>
    /// 将当前页范围内的关卡配置分配给预设按钮，并隐藏没有对应配置的剩余按钮。
    /// </summary>
    private void RefreshButtons()
    {
        if (stageButtons == null) return;

        int startIndex = _currentPage * ButtonsPerPage;                                                 // 当前页第一个配置的列表索引。
        int buttonCount = Mathf.Min(ButtonsPerPage, stageButtons.Length);                               // 本次实际检查的按钮数量。

        for (int i = 0; i < buttonCount; i++)
        {
            StageButton button = stageButtons[i];                                                       // 当前需要刷新的按钮。
            if (button == null) continue;

            int configIndex = startIndex + i;                                                           // 当前按钮对应的配置索引。
            bool hasConfig = configIndex < _configs.Count;                                              // 当前按钮是否有可绑定的配置。
            button.Init(hasConfig ? _configs[configIndex] : null);
            button.gameObject.SetActive(hasConfig);

            // 关卡编号文本：按整条关卡序列编号，与翻到第几页无关——第 8 关在第 2 页，
            // 编号仍然是 1-8（取配置自己的 stageId），不会再按页算成 2-1。
            if (stageNumberTexts != null && i < stageNumberTexts.Length && stageNumberTexts[i] != null)
            {
                StageConfig config = hasConfig ? _configs[configIndex] : null;
                int number = config != null ? config.stageId : 0;
                stageNumberTexts[i].text = number > 0 ? $"{stageNumberChapter}-{number}" : string.Empty;
            }
        }

        RefreshPageArrowState();   // 翻页功能（测试用）：关卡数量或页码变化后同步箭头可用状态。
    }
    #endregion

    #region 生命周期与回调
    /// <summary>
    /// 组件销毁时释放所有仍有效的 Addressables 句柄，归还已加载关卡配置。
    /// </summary>
    private void OnDestroy()
    {
        ReleasePending();
        foreach (var h in _handles)
            if (h.IsValid()) Addressables.Release(h);
    }

    /// <summary>
    /// 在编辑器配置变化时将按钮数组与编号文本数组强制调整为每页固定数量。
    /// </summary>
    private void OnValidate()
    {
        if (stageButtons == null || stageButtons.Length != ButtonsPerPage)
        {
            System.Array.Resize(ref stageButtons, ButtonsPerPage);
        }

        if (stageNumberTexts == null || stageNumberTexts.Length != ButtonsPerPage)
        {
            System.Array.Resize(ref stageNumberTexts, ButtonsPerPage);
        }
    }
    #endregion
}
