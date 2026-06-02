using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;


public enum ResourceState
{
    None,
    Loading,
    LoadComplete,
    Unloading,
    UnloadComplete
}

/// <summary>
/// 全局资源管理器
/// 负责所有动态资源的异步加载、缓存和释放
/// </summary>
public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }

    [Tooltip("Prefab 映射表，负责将纯文本的 prefabKey 映射到实际的 AssetReference")]
    public PrefabRegistry prefabRegistry;

    public ResourceState CurrentState { get; private set; } = ResourceState.None;

    public event Action OnLoadComplete;
    public event Action OnUnloadComplete;

    // --- 私有缓存字典 ---
    private Dictionary<string, GameObject> _gameObjectDict = new Dictionary<string, GameObject>();
    private Dictionary<string, AudioClip> _audioDict = new Dictionary<string, AudioClip>();
    private Dictionary<string, Texture2D> _textureDict = new Dictionary<string, Texture2D>();
    private Dictionary<string, AnimationClip> _animationDict = new Dictionary<string, AnimationClip>();
    private Dictionary<string, RuntimeAnimatorController> _animatorControllerDict = new Dictionary<string, RuntimeAnimatorController>();

    // 统一管理所有加载成功后的句柄，以便统一释放
    private List<AsyncOperationHandle> _handlesToRelease = new List<AsyncOperationHandle>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // --- 获取接口 (O(1)) ---
    
    public GameObject GetGameObject(string key)
    {
        return _gameObjectDict.TryGetValue(key, out var res) ? res : null;
    }

    public AudioClip GetAudio(string key)
    {
        return _audioDict.TryGetValue(key, out var res) ? res : null;
    }

    public Texture2D GetTexture(string key)
    {
        return _textureDict.TryGetValue(key, out var res) ? res : null;
    }

    public AnimationClip GetAnimation(string key)
    {
        return _animationDict.TryGetValue(key, out var res) ? res : null;
    }

    public RuntimeAnimatorController GetAnimatorController(string key)
    {
        return _animatorControllerDict.TryGetValue(key, out var res) ? res : null;
    }

    // --- 生命周期管理 ---

    /// <summary>
    /// 提前加载场景中的所有资源
    /// </summary>
    public void LoadStageResources(LevelConfig level)
    {
        if (CurrentState == ResourceState.Loading)
        {
            Debug.LogWarning("[ResourceManager] 当前正在加载中，请勿重复调用！");
            return;
        }

        if (prefabRegistry == null)
        {
            Debug.LogWarning("[ResourceManager] 尚未配置 PrefabRegistry，基于 Registry 的预制体解析将失效！");
            return;
        }

        CurrentState = ResourceState.Loading;
        StartCoroutine(CoLoadStageResources(level));
    }

    private IEnumerator CoLoadStageResources(LevelConfig level)
    {
        // 0. Catalog 热更新检查
        Debug.Log("[ResourceManager] 开始检查 Catalog 更新...");
        var checkHandle = Addressables.CheckForCatalogUpdates(false);
        yield return checkHandle;

        if (checkHandle.Status == AsyncOperationStatus.Succeeded && checkHandle.Result.Count > 0)
        {
            Debug.Log($"[ResourceManager] 发现 {checkHandle.Result.Count} 个 Catalog 更新，开始下载...");
            var updateHandle = Addressables.UpdateCatalogs(checkHandle.Result, false);
            yield return updateHandle;
            
            if (updateHandle.Status == AsyncOperationStatus.Succeeded)
            {
                Debug.Log("[ResourceManager] Catalog 更新完成！");
            }
            else
            {
                Debug.LogError("[ResourceManager] Catalog 更新失败！");
            }
            Addressables.Release(updateHandle);
        }
        Addressables.Release(checkHandle);

        if (prefabRegistry != null)
        {
            prefabRegistry.Initialize();
        }

        // 1. 通过反射扫描关卡数据，收集所有标记了 ResourceKeyAttribute 的 key 及其对应的 Type
        Dictionary<string, Type> keysWithTypes = new Dictionary<string, Type>();
        HashSet<object> visited = new HashSet<object>();
        ScanForResourceKeys(level, keysWithTypes, visited);

        List<object> keysToDownload = new List<object>();

        // 准备所有的底层 Addressables Key
        foreach (var kvp in keysWithTypes)
        {
            string key = kvp.Key;
            Type resType = kvp.Value;
            object addressableKey = key; // 默认情况下，Addressable Key 就是这个 string 名称

            // 如果是 GameObject 类型，我们优先从 PrefabRegistry 中寻找它映射的 AssetReference
            if (resType == typeof(GameObject) && prefabRegistry != null)
            {
                var reference = prefabRegistry.GetReference(key);
                if (reference != null && reference.RuntimeKeyIsValid())
                {
                    addressableKey = reference;
                }
            }

            keysToDownload.Add(addressableKey);
        }

        // 2. 批量合并下载依赖 (避免网络风暴)
        if (keysToDownload.Count > 0)
        {
            Debug.Log($"[ResourceManager] 开始批量下载 {keysToDownload.Count} 个资源的依赖...");
            // 显式转换为 IEnumerable 以消除 IList<object> 重载过时的警告
            var downloadHandle = Addressables.DownloadDependenciesAsync((IEnumerable)keysToDownload, Addressables.MergeMode.Union);
            yield return downloadHandle;

            if (downloadHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError("[ResourceManager] 批量下载资源依赖失败！网络异常。");
            }
            
            if (downloadHandle.IsValid())
            {
                Addressables.Release(downloadHandle);
            }

            // 3. 逐个将资源加载至内存并存入对应的分类字典
            foreach (var kvp in keysWithTypes)
            {
                string key = kvp.Key;
                Type resType = kvp.Value;
                object addressableKey = key;

                if (resType == typeof(GameObject) && prefabRegistry != null)
                {
                    var reference = prefabRegistry.GetReference(key);
                    if (reference != null && reference.RuntimeKeyIsValid())
                    {
                        addressableKey = reference;
                    }
                }

                if (resType == typeof(GameObject))
                {
                    var handle = Addressables.LoadAssetAsync<GameObject>(addressableKey);
                    yield return handle;
                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        _gameObjectDict[key] = handle.Result;
                        _handlesToRelease.Add(handle);
                    }
                    else Debug.LogError($"[ResourceManager] 加载 GameObject 失败！Key: {key}");
                }
                else if (resType == typeof(AudioClip))
                {
                    var handle = Addressables.LoadAssetAsync<AudioClip>(addressableKey);
                    yield return handle;
                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        _audioDict[key] = handle.Result;
                        _handlesToRelease.Add(handle);
                    }
                    else Debug.LogError($"[ResourceManager] 加载 AudioClip 失败！Key: {key}");
                }
                else if (resType == typeof(Texture2D))
                {
                    var handle = Addressables.LoadAssetAsync<Texture2D>(addressableKey);
                    yield return handle;
                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        _textureDict[key] = handle.Result;
                        _handlesToRelease.Add(handle);
                    }
                    else Debug.LogError($"[ResourceManager] 加载 Texture2D 失败！Key: {key}");
                }
                else if (resType == typeof(AnimationClip))
                {
                    var handle = Addressables.LoadAssetAsync<AnimationClip>(addressableKey);
                    yield return handle;
                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        _animationDict[key] = handle.Result;
                        _handlesToRelease.Add(handle);
                    }
                    else Debug.LogError($"[ResourceManager] 加载 AnimationClip 失败！Key: {key}");
                }
                else if (resType == typeof(RuntimeAnimatorController))
                {
                    var handle = Addressables.LoadAssetAsync<RuntimeAnimatorController>(addressableKey);
                    yield return handle;
                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        _animatorControllerDict[key] = handle.Result;
                        _handlesToRelease.Add(handle);
                    }
                    else Debug.LogError($"[ResourceManager] 加载 RuntimeAnimatorController 失败！Key: {key}");
                }
            }
        }

        CurrentState = ResourceState.LoadComplete;
        Debug.Log("[ResourceManager] 关卡预加载完成！");
        OnLoadComplete?.Invoke();
    }

    /// <summary>
    /// 反射递归扫描对象中的所有 ResourceKeyAttribute
    /// </summary>
    private void ScanForResourceKeys(object obj, Dictionary<string, Type> keysWithTypes, HashSet<object> visited)
    {
        if (obj == null) return;
        if (!visited.Add(obj)) return; // 防循环引用

        Type type = obj.GetType();
        if (type.IsPrimitive || type == typeof(string) || type.IsEnum) return;

        // 处理集合类型
        if (obj is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                ScanForResourceKeys(item, keysWithTypes, visited);
            }
            return;
        }

        var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        foreach (var field in fields)
        {
            if (field.FieldType == typeof(string))
            {
                var attr = Attribute.GetCustomAttribute(field, typeof(ResourceKeyAttribute)) as ResourceKeyAttribute;
                if (attr != null)
                {
                    string key = field.GetValue(obj) as string;
                    if (!string.IsNullOrEmpty(key) && !keysWithTypes.ContainsKey(key))
                    {
                        keysWithTypes[key] = attr.ResourceType;
                    }
                }
            }
            else if (!field.FieldType.IsPrimitive && field.FieldType != typeof(string) && !field.FieldType.IsEnum)
            {
                var val = field.GetValue(obj);
                ScanForResourceKeys(val, keysWithTypes, visited);
            }
        }
    }

    /// <summary>
    /// 提供一个按名称 (Key) 加载其它资源的基础方法，方便外部主动请求缓存
    /// </summary>
    public void LoadExtraResourceAsync<T>(string key, Action<T> onComplete = null) where T : UnityEngine.Object
    {
        StartCoroutine(CoLoadExtraResource(key, onComplete));
    }

    private IEnumerator CoLoadExtraResource<T>(string key, Action<T> onComplete) where T : UnityEngine.Object
    {
        var handle = Addressables.LoadAssetAsync<T>(key);
        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            _handlesToRelease.Add(handle);
            T result = handle.Result;

            // 根据类型存入对应的字典
            if (typeof(T) == typeof(AudioClip)) _audioDict[key] = result as AudioClip;
            else if (typeof(T) == typeof(Texture2D)) _textureDict[key] = result as Texture2D;
            else if (typeof(T) == typeof(AnimationClip)) _animationDict[key] = result as AnimationClip;
            else if (typeof(T) == typeof(RuntimeAnimatorController)) _animatorControllerDict[key] = result as RuntimeAnimatorController;

            onComplete?.Invoke(result);
        }
        else
        {
            Debug.LogError($"[ResourceManager] 按名称加载额外资源失败！Key: {key}, Type: {typeof(T)}");
            onComplete?.Invoke(null);
        }
    }

    /// <summary>
    /// 卸载当前资源管理器中已经加载的资源
    /// </summary>
    public void UnloadStageResource()
    {
        if (CurrentState == ResourceState.Unloading)
        {
            return;
        }

        CurrentState = ResourceState.Unloading;
        Debug.Log("[ResourceManager] 开始卸载关卡资源...");

        // 清理缓存字典
        _gameObjectDict.Clear();
        _audioDict.Clear();
        _textureDict.Clear();
        _animationDict.Clear();
        _animatorControllerDict.Clear();

        // 批量释放 Addressables 句柄，促使内部引用计数衰减，最终卸载内存
        foreach (var handle in _handlesToRelease)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }
        _handlesToRelease.Clear();

        CurrentState = ResourceState.UnloadComplete;
        Debug.Log("[ResourceManager] 资源卸载完成！");
        OnUnloadComplete?.Invoke();
    }
}

