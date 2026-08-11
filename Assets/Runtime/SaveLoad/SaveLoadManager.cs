using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 场景中的通用 JSON 存读档单例入口。
/// 其他模块只提供逻辑文件名和 JSON，不接触实际存储路径。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-900)]
public sealed class SaveLoadManager : MonoBehaviour
{
    private const string ContextTestFileName = "save_load_test";
    private const string ContextTestJson = "{\"message\":\"SaveLoad test\"}";

    public static SaveLoadManager Instance { get; private set; }

    private int _pendingRequestCount;

    /// <summary>
    /// 是否存在正在执行或等待执行的存读档请求。
    /// </summary>
    public bool IsBusy => Volatile.Read(ref _pendingRequestCount) > 0;

    /// <summary>
    /// 当前本地测试后端的默认存档目录。
    /// </summary>
    public string DefaultLocalSaveDirectoryPath =>
        SaveLoadSystem.DefaultLocalSaveDirectoryPath;

    #region 生命周期
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "[SaveLoadManager] 场景中存在重复实例，后创建的组件将被销毁。",
                this);
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
    #endregion

    #region 对外接口
    /// <summary>
    /// 将 JSON 保存为指定逻辑文件名。
    /// fileName 不需要携带 .json 扩展名。
    /// </summary>
    public async Task<SaveLoadResult> SaveAsync(
        string fileName,
        string json,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _pendingRequestCount);
        try
        {
            return await SaveLoadSystem.SaveAsync(fileName, json, cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref _pendingRequestCount);
        }
    }

    /// <summary>
    /// 读取指定逻辑文件名对应的 JSON。
    /// 成功时从返回结果的 Json 属性取得原文。
    /// </summary>
    public async Task<SaveReadResult> LoadAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _pendingRequestCount);
        try
        {
            return await SaveLoadSystem.LoadAsync(fileName, cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref _pendingRequestCount);
        }
    }
    #endregion

    #region Inspector 测试
    [ContextMenu("存读档/保存通用测试 JSON")]
    private async void SaveFromContextMenu()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[SaveLoadManager] 请进入运行模式后再测试保存。", this);
            return;
        }

        SaveLoadResult result = await SaveAsync(ContextTestFileName, ContextTestJson);
        LogTestResult("保存", result);
    }

    [ContextMenu("存读档/读取通用测试 JSON")]
    private async void LoadFromContextMenu()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[SaveLoadManager] 请进入运行模式后再测试读取。", this);
            return;
        }

        SaveReadResult readResult = await LoadAsync(ContextTestFileName);
        LogTestResult("读取", readResult.Result);
        if (readResult.IsSuccess)
        {
            Debug.Log($"[SaveLoadManager] JSON：{readResult.Json}", this);
        }
    }

    private void LogTestResult(string operationName, SaveLoadResult result)
    {
        string message = $"[SaveLoadManager] {operationName}：{result.Message}";
        if (result.IsSuccess)
        {
            Debug.Log(message, this);
            return;
        }

        Debug.LogError($"{message}（状态：{result.Status}）", this);
    }
    #endregion
}
