using System.Collections;
using System.Collections.Generic;
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

    private readonly List<AsyncOperationHandle> _handles = new List<AsyncOperationHandle>();            // 已成功加载、销毁时需要释放的 Addressables 句柄。
    private readonly List<StageConfig> _configs = new List<StageConfig>();                              // 按关卡编号顺序加载的配置列表。
    private int _currentPage;                                                                           // 当前显示页的零基索引。

    private int TotalPages => Mathf.Max(1, Mathf.CeilToInt((float)_configs.Count / ButtonsPerPage));    // 根据已加载配置计算的总页数。

    #region 生命周期与回调
    /// <summary>
    /// 在异步配置加载前先刷新按钮，使尚无配置的按钮保持隐藏。
    /// </summary>
    private void Awake() => RefreshButtons();

    /// <summary>
    /// 启动按编号顺序加载关卡配置的协程。
    /// </summary>
    private void Start() => StartCoroutine(LoadConfigs());
    #endregion

    #region 特效与协程
    /// <summary>
    /// 按 Stage1、Stage2 等连续地址逐个加载关卡配置，遇到第一个不存在的地址后停止，
    /// 保存成功句柄并刷新当前页按钮。
    /// </summary>
    /// <returns>逐个等待 Addressables 加载操作完成的协程。</returns>
    private IEnumerator LoadConfigs()
    {
        for (int i = 1; ; i++)
        {
            var handle = Addressables.LoadAssetAsync<StageConfig>($"Stage{i}");
            yield return handle;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Addressables.Release(handle);
                break;
            }

            _handles.Add(handle);
            _configs.Add(handle.Result);
        }

        _currentPage = Mathf.Clamp(_currentPage, 0, TotalPages - 1);
        RefreshButtons();
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
        }
    }
    #endregion

    #region 生命周期与回调
    /// <summary>
    /// 组件销毁时释放所有仍有效的 Addressables 句柄，归还已加载关卡配置。
    /// </summary>
    private void OnDestroy()
    {
        foreach (var h in _handles)
            if (h.IsValid()) Addressables.Release(h);
    }

    /// <summary>
    /// 在编辑器配置变化时将按钮数组强制调整为每页固定数量。
    /// </summary>
    private void OnValidate()
    {
        if (stageButtons == null || stageButtons.Length != ButtonsPerPage)
        {
            System.Array.Resize(ref stageButtons, ButtonsPerPage);
        }
    }
    #endregion
}
