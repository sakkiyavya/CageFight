using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// UserGlobalInfo 的唯一存档适配器。只经 SaveLoadManager 读写 JSON，
/// 并在首次读取完成前阻止选装系统使用或覆盖存档数据。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UserGlobalInfo))]
[DefaultExecutionOrder(-990)]
public sealed class UserGlobalInfoPersistence : MonoBehaviour
{
    [SerializeField] private string saveFileName = "user_global_info";
    [SerializeField, Min(0f)] private float saveDelay = 0.5f;

    private UserGlobalInfo userGlobalInfo;
    private Coroutine saveRoutine;
    private bool isActive;
    private bool isLoading;
    private bool loadStarted;
    private bool savePending;

    /// <summary>首次读档完成后为 true；未找到存档也视为已完成。</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>首次读档完成后触发一次。</summary>
    public event Action Loaded;

    private void Awake()
    {
        userGlobalInfo = GetComponent<UserGlobalInfo>();
    }

    private void OnEnable()
    {
        isActive = true;
        userGlobalInfo.Changed += HandleChanged;
        if (!loadStarted)
        {
            loadStarted = true;
            StartCoroutine(LoadRoutine());
        }
    }

    private void OnDisable()
    {
        isActive = false;
        if (userGlobalInfo) userGlobalInfo.Changed -= HandleChanged;
        if (saveRoutine != null) StopCoroutine(saveRoutine);
        saveRoutine = null;
    }

    /// <summary>立即请求保存当前全局数据，供退出或显式提交入口调用。</summary>
    public void SaveNow()
    {
        if (!IsLoaded || !isActive || SaveLoadManager.Instance == null) return;
        if (saveRoutine != null) StopCoroutine(saveRoutine);
        saveRoutine = StartCoroutine(SaveRoutine(0f));
    }

    private IEnumerator LoadRoutine()
    {
        isLoading = true;
        while (isActive && SaveLoadManager.Instance == null) yield return null;
        if (!isActive) yield break;

        Task<SaveReadResult> task = SaveLoadManager.Instance.LoadAsync(saveFileName);
        while (!task.IsCompleted) yield return null;
        if (!isActive) yield break;

        if (task.IsFaulted)
        {
            Debug.LogError("[UserGlobalInfoPersistence] 读取全局存档时发生异常。", this);
        }
        else
        {
            SaveReadResult result = task.Result;
            if (result.IsSuccess)
            {
                if (!userGlobalInfo.TryDeserializeFromJson(result.Json, out string error))
                    Debug.LogError($"[UserGlobalInfoPersistence] 全局存档无效：{error}", this);
            }
            else if (result.Status != SaveLoadStatus.NotFound)
            {
                Debug.LogError(
                    $"[UserGlobalInfoPersistence] 读取全局存档失败：{result.Message}",
                    this);
            }
        }

        isLoading = false;
        IsLoaded = true;
        Loaded?.Invoke();
        if (savePending) StartSaveDelayed();
    }

    private void HandleChanged()
    {
        if (!IsLoaded || isLoading)
        {
            savePending = true;
            return;
        }

        StartSaveDelayed();
    }

    private void StartSaveDelayed()
    {
        savePending = false;
        if (!isActive || saveRoutine != null) return;
        saveRoutine = StartCoroutine(SaveRoutine(saveDelay));
    }

    private IEnumerator SaveRoutine(float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        if (!isActive || SaveLoadManager.Instance == null)
        {
            saveRoutine = null;
            yield break;
        }

        Task<SaveLoadResult> task = SaveLoadManager.Instance.SaveAsync(
            saveFileName,
            userGlobalInfo.SerializeToJson());
        while (!task.IsCompleted) yield return null;
        saveRoutine = null;

        if (task.IsFaulted || !task.Result.IsSuccess)
        {
            string error = task.IsFaulted ? "发生异常" : task.Result.Message;
            Debug.LogError($"[UserGlobalInfoPersistence] 保存全局存档失败：{error}", this);
        }
    }
}
