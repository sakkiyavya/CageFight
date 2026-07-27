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
    private const int ButtonsPerPage = 7;

    [Tooltip("The seven stage buttons, in display order.")]
    [FormerlySerializedAs("levelButtons")]
    [SerializeField] private StageButton[] stageButtons = new StageButton[ButtonsPerPage];

    private readonly List<AsyncOperationHandle> _handles = new List<AsyncOperationHandle>();
    private readonly List<StageConfig> _configs = new List<StageConfig>();
    private int _currentPage;

    private int TotalPages => Mathf.Max(1, Mathf.CeilToInt((float)_configs.Count / ButtonsPerPage));

    private void Awake() => RefreshButtons();

    private void Start() => StartCoroutine(LoadConfigs());

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

    /// <summary>
    /// Turns one page. Button positions remain authored in the scene; only
    /// each button's configuration and active state are updated.
    /// </summary>
    public void TurnPage(bool isNext)
    {
        int targetPage = _currentPage + (isNext ? 1 : -1);
        if (targetPage < 0 || targetPage >= TotalPages) return;

        _currentPage = targetPage;
        RefreshButtons();
    }

    private void RefreshButtons()
    {
        if (stageButtons == null) return;

        int startIndex = _currentPage * ButtonsPerPage;
        int buttonCount = Mathf.Min(ButtonsPerPage, stageButtons.Length);

        for (int i = 0; i < buttonCount; i++)
        {
            StageButton button = stageButtons[i];
            if (button == null) continue;

            int configIndex = startIndex + i;
            bool hasConfig = configIndex < _configs.Count;
            button.Init(hasConfig ? _configs[configIndex] : null);
            button.gameObject.SetActive(hasConfig);
        }
    }

    private void OnDestroy()
    {
        foreach (var h in _handles)
            if (h.IsValid()) Addressables.Release(h);
    }

    private void OnValidate()
    {
        if (stageButtons == null || stageButtons.Length != ButtonsPerPage)
        {
            System.Array.Resize(ref stageButtons, ButtonsPerPage);
        }
    }
}
