using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif


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
    #if UNITY_EDITOR
    public LevelConfig editorLevelConfig;
    #endif
    public static ResourceManager Instance { get; private set; }

    [Header("各类注册表")]
    [Tooltip("Prefab 映射表，负责将纯文本的 prefabKey 映射到实际的 AssetReference")]
    [SerializeField] private PrefabRegistry prefabRegistry;

    [Tooltip("Texture 映射表")]
    [SerializeField] private TextureRegistry textureRegistry;

    [Tooltip("Audio 映射表")]
    [SerializeField] private AudioRegistry audioRegistry;

    [Tooltip("AnimationClip 映射表")]
    [SerializeField] private AnimationClipRegistry animationClipRegistry;

    [Tooltip("AnimatorController 映射表")]
    [SerializeField] private AnimatorControllerRegistry animatorControllerRegistry;

    [Tooltip("Sprite 映射表（支持多图切片子图）")]
    [SerializeField] private SpriteRegistry spriteRegistry;

    public ResourceState CurrentState { get; private set; } = ResourceState.None;

    public event Action OnLoadComplete;
    public event Action OnUnloadComplete;

    // --- 私有缓存字典 ---
    private Dictionary<string, GameObject> _gameObjectDict = new Dictionary<string, GameObject>();
    private Dictionary<string, AudioClip> _audioDict = new Dictionary<string, AudioClip>();
    private Dictionary<string, Texture2D> _textureDict = new Dictionary<string, Texture2D>();
    private Dictionary<string, AnimationClip> _animationDict = new Dictionary<string, AnimationClip>();
    private Dictionary<string, RuntimeAnimatorController> _animatorControllerDict = new Dictionary<string, RuntimeAnimatorController>();
    private Dictionary<string, Sprite> _spriteDict = new Dictionary<string, Sprite>();
    public List<string> _spriteKeys = new List<string>();

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

        // 加载所有 Registry：Editor 下直接从文件系统加载，Runtime 通过 Addressables 加载
#if UNITY_EDITOR
        prefabRegistry           = LoadRegistryEditor<PrefabRegistry>();
        textureRegistry          = LoadRegistryEditor<TextureRegistry>();
        audioRegistry            = LoadRegistryEditor<AudioRegistry>();
        animationClipRegistry    = LoadRegistryEditor<AnimationClipRegistry>();
        animatorControllerRegistry = LoadRegistryEditor<AnimatorControllerRegistry>();
        spriteRegistry           = LoadRegistryEditor<SpriteRegistry>();

#else
        StartCoroutine(LoadAllRegistriesRuntime());
#endif
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 编辑器下：从 AssetDatabase 搜索并加载指定类型的 Registry SO。
    /// 文件名和类名相同（如 PrefabRegistry.asset）。
    /// </summary>
    private static T LoadRegistryEditor<T>() where T : ScriptableObject
    {
        string typeName = typeof(T).Name;
        string[] guids = AssetDatabase.FindAssets($"{typeName} t:{typeName}");
        if (guids.Length == 0)
        {
            Debug.LogWarning($"[ResourceManager] Editor: 未找到 {typeName}！请确认资产已存在且文件名与类名一致。");
            return null;
        }
        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            Debug.LogWarning($"[ResourceManager] Editor: 加载 {typeName} 失败，路径：{path}");
        return asset;
    }
#else
    /// <summary>
    /// 运行时：通过 Addressables 异步加载所有 Registry。
    /// Key = 类名（如 "PrefabRegistry"）。
    /// </summary>
    /// <summary>
    /// 运行时：通过 Addressables 异步加载所有 Registry。
    /// Key = 类名（如 "PrefabRegistry"）。
    /// </summary>
    private IEnumerator LoadAllRegistriesRuntime()
    {
        yield return LoadRegistryRuntime<PrefabRegistry>(r           => prefabRegistry = r);
        yield return LoadRegistryRuntime<TextureRegistry>(r          => textureRegistry = r);
        yield return LoadRegistryRuntime<AudioRegistry>(r            => audioRegistry = r);
        yield return LoadRegistryRuntime<AnimationClipRegistry>(r    => animationClipRegistry = r);
        yield return LoadRegistryRuntime<AnimatorControllerRegistry>(r => animatorControllerRegistry = r);
        yield return LoadRegistryRuntime<SpriteRegistry>(r           => spriteRegistry = r);

        // 初始化已加载的 Registry
        prefabRegistry?.Initialize();
        textureRegistry?.Initialize();
        audioRegistry?.Initialize();
        animationClipRegistry?.Initialize();
        animatorControllerRegistry?.Initialize();
        spriteRegistry?.Initialize();

        Debug.Log("[ResourceManager] 所有 Registry 加载完成。");
    }

    private IEnumerator LoadRegistryRuntime<T>(Action<T> onDone) where T : ScriptableObject
    {
        string key = typeof(T).Name;
        var handle = Addressables.LoadAssetAsync<T>(key);
        yield return handle;
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            _handlesToRelease.Add(handle);
            onDone?.Invoke(handle.Result);
        }
        else
        {
            Debug.LogError($"[ResourceManager] Runtime: 加载 {key} 失败！请确认 Addressables 中已配置该 Key。");
            onDone?.Invoke(null);
        }

    }
#endif


    
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

    public Sprite GetSprite(string key)
    {
        // Debug.Log("ResourceManager.GetSprit:" + key + "  " + _spriteDict.ContainsKey(key));
        return _spriteDict.TryGetValue(key, out var res) ? res : null;
    }

    // --- 生命周期管理 ---

    /// <summary>
    /// 提前加载场景中的所有资源
    /// </summary>
    public bool LoadStageResources(LevelConfig level)
    {

        if (CurrentState == ResourceState.Loading)
        {
            Debug.LogWarning("[ResourceManager] 当前正在加载中，请勿重复调用！");
            return false;
        }

        if (prefabRegistry == null && textureRegistry == null && audioRegistry == null && 
            animationClipRegistry == null && animatorControllerRegistry == null)
        {
            Debug.LogError("[ResourceManager] 所有 Registry 均未配置！无法预加载任何资源。");
            return false;
        }

        UnloadStageResource();
        CurrentState = ResourceState.Loading;
        StartCoroutine(CoLoadStageResources(level));
        return true;
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

        // 初始化所有已配置的注册表
        if (prefabRegistry != null) prefabRegistry.Initialize();
        if (textureRegistry != null) textureRegistry.Initialize();
        if (audioRegistry != null) audioRegistry.Initialize();
        if (animationClipRegistry != null) animationClipRegistry.Initialize();
        if (animatorControllerRegistry != null) animatorControllerRegistry.Initialize();
        if (spriteRegistry != null) spriteRegistry.Initialize();

        // 1. 根据 LevelConfig 的 5 个分类列表，收集所有资源 Key 并通过对应的 Registry 解析真实句柄
        Dictionary<string, Type> keysWithTypes = new Dictionary<string, Type>();
        Dictionary<string, object> keysWithAddressableKeys = new Dictionary<string, object>();

        // 处理 Prefabs (GameObject)
        if (level.prefabs != null)
        {
            foreach (string key in level.prefabs)
            {
                if (string.IsNullOrEmpty(key)) continue;
                keysWithTypes[key] = typeof(GameObject);

                object addressableKey = key;
                if (prefabRegistry != null && prefabRegistry.GetReference(key) is { } prefabRef && prefabRef.RuntimeKeyIsValid())
                {
                    addressableKey = prefabRef;
                }
                keysWithAddressableKeys[key] = addressableKey;
            }
        }

        // 处理 Audios (AudioClip)
        if (level.audios != null)
        {
            foreach (string key in level.audios)
            {
                if (string.IsNullOrEmpty(key)) continue;
                keysWithTypes[key] = typeof(AudioClip);

                object addressableKey = key;
                if (audioRegistry != null && audioRegistry.GetReference(key) is { } audioRef && audioRef.RuntimeKeyIsValid())
                {
                    addressableKey = audioRef;
                }
                keysWithAddressableKeys[key] = addressableKey;
            }
        }

        // 处理 Textures (Texture2D)
        if (level.textures != null)
        {
            foreach (string key in level.textures)
            {
                if (string.IsNullOrEmpty(key)) continue;
                keysWithTypes[key] = typeof(Texture2D);

                object addressableKey = key;
                if (textureRegistry != null && textureRegistry.GetReference(key) is { } textureRef && textureRef.RuntimeKeyIsValid())
                {
                    addressableKey = textureRef;
                }
                keysWithAddressableKeys[key] = addressableKey;
            }
        }

        // 处理 AnimationClips (AnimationClip)
        if (level.animationClips != null)
        {
            foreach (string key in level.animationClips)
            {
                if (string.IsNullOrEmpty(key)) continue;
                keysWithTypes[key] = typeof(AnimationClip);

                object addressableKey = key;
                if (animationClipRegistry != null && animationClipRegistry.GetReference(key) is { } animClipRef && animClipRef.RuntimeKeyIsValid())
                {
                    addressableKey = animClipRef;
                }
                keysWithAddressableKeys[key] = addressableKey;
            }
        }

        // 处理 AnimatorControllers (RuntimeAnimatorController)
        if (level.animatorControllers != null)
        {
            foreach (string key in level.animatorControllers)
            {
                if (string.IsNullOrEmpty(key)) continue;
                keysWithTypes[key] = typeof(RuntimeAnimatorController);

                object addressableKey = key;
                if (animatorControllerRegistry != null && animatorControllerRegistry.GetReference(key) is { } animCtrlRef && animCtrlRef.RuntimeKeyIsValid())
                {
                    addressableKey = animCtrlRef;
                }
                keysWithAddressableKeys[key] = addressableKey;
            }
        }

        // 处理 Sprites (Sprite)
        if (level.sprites != null)
        {
            foreach (string key in level.sprites)
            {
                if (string.IsNullOrEmpty(key)) continue;
                keysWithTypes[key] = typeof(Sprite);

                object addressableKey = key;
                if (spriteRegistry != null && spriteRegistry.GetReference(key) is { } spriteRef && spriteRef.RuntimeKeyIsValid())
                {
                    addressableKey = spriteRef;
                }
                keysWithAddressableKeys[key] = addressableKey;
            }
        }

        List<object> keysToDownload = new List<object>();
        foreach (var kvp in keysWithTypes)
        {
            string key = kvp.Key;
            if (keysWithAddressableKeys.TryGetValue(key, out var addrKey))
            {
                keysToDownload.Add(addrKey);
            }
        }

        // 2. 批量合并下载依赖 (避免网络风暴)
        if (keysToDownload.Count > 0)
        {
            Debug.Log($"[ResourceManager] 开始批量下载 {keysToDownload.Count} 个资源的依赖...");
            // 显式转换为 IEnumerable 以消除 IList<object> 重载过时的警告
            var downloadHandle = Addressables.DownloadDependenciesAsync((System.Collections.IEnumerable)keysToDownload, Addressables.MergeMode.Union);
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
                object addressableKey = keysWithAddressableKeys[key];

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
                else if (resType == typeof(Sprite))
                {
                    var handle = Addressables.LoadAssetAsync<Sprite>(addressableKey);
                    yield return handle;
                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        _spriteDict[key] = handle.Result;
                        _spriteKeys.Add(key);
                        _handlesToRelease.Add(handle);
                    }
                    else Debug.LogError($"[ResourceManager] 加载 Sprite 失败！Key: {key}");
                }
            }
        }

        CurrentState = ResourceState.LoadComplete;
        Debug.Log("[ResourceManager] 关卡预加载完成！");
        OnLoadComplete?.Invoke();
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
        _spriteDict.Clear();

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

