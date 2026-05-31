using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 关卡加载测试交互按钮脚本 (Addressable 临时自包含加载器版)
/// 挂载在 UGUI 的 Button 对象上。点击后，自己内部使用 Addressables 异步流下载并解析资源，
/// 还原关卡，不依赖尚未重构好的 LevelLoader。
/// </summary>
[RequireComponent(typeof(Button))]
public class StageLoadTestButton : MonoBehaviour
{
    [Header("核心配置")]
    [Tooltip("目标关卡配置，请拖入您需要测试的 LevelConfig 资产")]
    [SerializeField] private LevelConfig targetLevelConfig;

    [Tooltip("预制体注册表，用于获取 AssetReferenceGameObject 弱引用")]
    [SerializeField] private PrefabRegistry prefabRegistry;

    [Header("运行时生成的实体根节点 (可选，便于清理归类)")]
    [SerializeField] private Transform entitiesParent;

    // UGUI 按钮组件缓存
    private Button _button;
    private Text _buttonText;
    private string _originalText = "加载关卡";

    // 缓存已加载的句柄，用于离开或重新加载时彻底释放显存
    private readonly Dictionary<string, AsyncOperationHandle<GameObject>> _loadedHandles = new Dictionary<string, AsyncOperationHandle<GameObject>>();

    // 缓存已成功加载出来的 GameObject 预制体原始模板
    private readonly Dictionary<string, GameObject> _cachedPrefabs = new Dictionary<string, GameObject>();

    // 记录在场景中动态生成的所有物体实例，用于一键销毁
    private readonly List<GameObject> _spawnedEntities = new List<GameObject>();

    // 正在运行的加载协程
    private Coroutine _loadingCoroutine;

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (_button != null)
        {
            // 自动检索按钮的文字组件
            _buttonText = _button.GetComponentInChildren<Text>();
            if (_buttonText != null)
            {
                _originalText = _buttonText.text;
            }
        }
    }

    private void OnEnable()
    {
        if (_button != null)
        {
            _button.onClick.AddListener(OnLoadButtonClick);
        }
    }

    private void OnDisable()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnLoadButtonClick);
        }
    }

    private void OnDestroy()
    {
        // 游戏物体被销毁时，强制释放所有 Addressable 句柄，斩断内存堆积
        ReleaseAllResources();
    }

    /// <summary>
    /// 按钮点击响应事件
    /// </summary>
    private void OnLoadButtonClick()
    {
        if (targetLevelConfig == null)
        {
            Debug.LogError("[StageLoadTestButton] 无法执行加载：目标关卡配置 (LevelConfig) 为空，请先在 Inspector 中分配测试关卡！");
            return;
        }

        if (prefabRegistry == null)
        {
            Debug.LogError("[StageLoadTestButton] 无法执行加载：预制体注册表 (PrefabRegistry) 为空，请拖入配置！");
            return;
        }

        // 如果当前有正在加载的协程，强行停掉
        if (_loadingCoroutine != null)
        {
            StopCoroutine(_loadingCoroutine);
            _loadingCoroutine = null;
        }

        // 启动异步并发下载、加载与生成协程
        _loadingCoroutine = StartCoroutine(CoLoadAndSpawnLevel());
    }

    /// <summary>
    /// 核心异步并发加载与实体生成协程
    /// </summary>
    private IEnumerator CoLoadAndSpawnLevel()
    {
        // 1. 按钮锁定与状态更新
        _button.interactable = false;
        UpdateText("开始清空旧场景...");

        // 2. 级联清理旧的实体和之前加载占用的 Addressable 句柄
        ReleaseAllResources();
        yield return new WaitForSeconds(0.1f); // 稍微等待一帧，确保垃圾回收干净

        // 3. 扫描关卡配置，提取所有不重复的 prefabKey
        HashSet<string> uniqueKeys = new HashSet<string>();
        foreach (var obj in targetLevelConfig.objects)
        {
            if (obj != null && !string.IsNullOrEmpty(obj.prefabKey))
            {
                uniqueKeys.Add(obj.prefabKey);
            }
        }

        if (uniqueKeys.Count == 0)
        {
            Debug.LogWarning($"[StageLoadTestButton] 关卡 {targetLevelConfig.levelId} 中不包含任何关卡物品，加载完成。");
            ResetButtonState();
            yield break;
        }

        int totalCount = uniqueKeys.Count;
        var loadTasks = new List<LoadTask>();

        UpdateText("开始网络预下载...");
        Debug.Log($"[StageLoadTestButton] 开始使用 Addressables 异步预热关卡 {targetLevelConfig.levelId} 的 {totalCount} 个资源...");

        // 4. 发起全并发 Addressables 异步下载与加载
        foreach (string key in uniqueKeys)
        {
            // 通过刚才重构的 GetReference 接口获取对应的弱引用句柄
            AssetReferenceGameObject assetRef = prefabRegistry.GetReference(key);
            AsyncOperationHandle<GameObject> handle;

            if (assetRef != null && assetRef.RuntimeKeyIsValid())
            {
                // 采用 GUID 弱引用安全加载
                handle = Addressables.LoadAssetAsync<GameObject>(assetRef);
            }
            else
            {
                // 兜底退化方案：直寻址模式 (以 key 作为 Address 直接动态加载)
                handle = Addressables.LoadAssetAsync<GameObject>(key);
            }

            loadTasks.Add(new LoadTask(key, handle));
            _loadedHandles.Add(key, handle);
        }

        // 5. 进度追踪与状态同步
        bool allDone = false;
        while (!allDone)
        {
            allDone = true;
            float progressSum = 0f;

            foreach (var task in loadTasks)
            {
                if (!task.Handle.IsDone)
                {
                    allDone = false;
                }
                progressSum += task.Handle.PercentComplete;
            }

            float currentProgress = progressSum / totalCount;
            UpdateText($"加载中... ({Mathf.RoundToInt(currentProgress * 100)}%)");
            yield return null;
        }

        // 6. 校验并缓存加载成果
        bool hasError = false;
        foreach (var task in loadTasks)
        {
            if (task.Handle.Status == AsyncOperationStatus.Succeeded)
            {
                _cachedPrefabs.Add(task.Key, task.Handle.Result);
            }
            else
            {
                hasError = true;
                Debug.LogError($"[StageLoadTestButton] Addressable 资源异步加载失败！Key: '{task.Key}'，异常: {task.Handle.OperationException}");
            }
        }

        if (hasError)
        {
            Debug.LogError("[StageLoadTestButton] 关卡加载中断：存在无法成功下载/解析的 Addressable 资源！");
            UpdateText("加载失败！");
            yield return new WaitForSeconds(1.5f);
            ResetButtonState();
            yield break;
        }

        UpdateText("开始生成游戏世界...");
        yield return null; // 歇一帧，防止单帧卡死

        // 7. 实体生成与多态参数数据注入覆盖
        int spawnedCount = 0;
        foreach (var objData in targetLevelConfig.objects)
        {
            if (objData == null) continue;

            if (_cachedPrefabs.TryGetValue(objData.prefabKey, out GameObject prefab))
            {
                // 实例化
                GameObject instance = Instantiate(prefab, entitiesParent);
                _spawnedEntities.Add(instance);

                // 还原空间 Transform
                instance.transform.position = objData.transform.position;
                instance.transform.eulerAngles = objData.transform.rotation;
                instance.transform.localScale = objData.transform.scale;

                // 多态组件数据还原（对 ILevelComponent 接口成员注入 ApplyData）
                var levelComponents = instance.GetComponentsInChildren<ILevelComponent>(true);
                foreach (var savedData in objData.components)
                {
                    foreach (var comp in levelComponents)
                    {
                        if (comp.DataType == savedData.GetType())
                        {
                            comp.ApplyData(savedData);
                            break;
                        }
                    }
                }

                spawnedCount++;
            }
        }

        Debug.Log($"<color=lime><b>[StageLoadTestButton] 关卡 {targetLevelConfig.levelId} 完美加载完毕！成功生成了 {spawnedCount} 个实体。</b></color>");
        
        UpdateText("加载成功！");
        yield return new WaitForSeconds(1.0f);
        
        ResetButtonState();
        _loadingCoroutine = null;
    }

    /// <summary>
    /// 一键清空场景实体并释放所有 Addressables 加载句柄，实现生命周期彻底闭环
    /// </summary>
    private void ReleaseAllResources()
    {
        // 1. 彻底销毁场景内上一次由本组件动态生成并记录的所有实体实例
        foreach (var entity in _spawnedEntities)
        {
            if (entity != null)
            {
                Destroy(entity);
            }
        }
        _spawnedEntities.Clear();

        // 2. 主动在场景中搜寻并清除所有挂载了 LevelObjectMarker 的非常驻关卡物品 (无论是否由本脚本生成)
        var sceneMarkers = FindObjectsOfType<LevelObjectMarker>(true);
        int destroyedMarkerCount = 0;
        foreach (var marker in sceneMarkers)
        {
            if (marker != null && marker.gameObject != null)
            {
                Destroy(marker.gameObject);
                destroyedMarkerCount++;
            }
        }
        if (destroyedMarkerCount > 0)
        {
            Debug.Log($"[StageLoadTestButton] 主动检测并清除了场景中 {destroyedMarkerCount} 个包含 LevelObjectMarker 的非常驻旧关卡物体。");
        }

        // 3. 遍历句柄，释放所有正在占用显存/内存的资源引用
        foreach (var kvp in _loadedHandles)
        {
            if (kvp.Value.IsValid())
            {
                Addressables.Release(kvp.Value);
            }
        }
        _loadedHandles.Clear();
        _cachedPrefabs.Clear();

        Debug.Log("[StageLoadTestButton] 场景旧实体已完全销毁，Addressables 句柄已级联安全释放。");
    }

    /// <summary>
    /// 恢复按钮交互状态
    /// </summary>
    private void ResetButtonState()
    {
        _button.interactable = true;
        if (_buttonText != null)
        {
            _buttonText.text = _originalText;
        }
    }

    /// <summary>
    /// 动态修改按钮的提示文字
    /// </summary>
    private void UpdateText(string content)
    {
        if (_buttonText != null)
        {
            _buttonText.text = content;
        }
    }

    // 辅助追踪异步任务状态的结构体
    private struct LoadTask
    {
        public string Key;
        public AsyncOperationHandle<GameObject> Handle;

        public LoadTask(string key, AsyncOperationHandle<GameObject> handle)
        {
            Key = key;
            Handle = handle;
        }
    }
}
