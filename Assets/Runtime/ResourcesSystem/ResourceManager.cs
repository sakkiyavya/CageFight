using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Runtime.ResourcesSystem
{
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
        /// 提前加载场景中的所有资源（当前主要提取 LevelConfig 中的预制体）
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
                Debug.LogError("[ResourceManager] 尚未配置 PrefabRegistry，无法解析 GameObject 映射！");
                return;
            }

            CurrentState = ResourceState.Loading;
            StartCoroutine(CoLoadStageResources(level));
        }

        private IEnumerator CoLoadStageResources(LevelConfig level)
        {
            prefabRegistry.Initialize();

            // 1. 收集关卡所需的所有的 prefabKey
            HashSet<string> uniquePrefabKeys = new HashSet<string>();
            List<object> keysToDownload = new List<object>();

            if (level != null && level.objects != null)
            {
                foreach (var obj in level.objects)
                {
                    if (!string.IsNullOrEmpty(obj.prefabKey) && uniquePrefabKeys.Add(obj.prefabKey))
                    {
                        var reference = prefabRegistry.GetReference(obj.prefabKey);
                        if (reference != null && reference.RuntimeKeyIsValid())
                        {
                            keysToDownload.Add(reference);
                        }
                        else
                        {
                            Debug.LogWarning($"[ResourceManager] 在 Registry 中找不到 prefabKey: {obj.prefabKey}");
                        }
                    }
                }
            }

            // 2. 批量合并下载依赖 (避免网络风暴)
            if (keysToDownload.Count > 0)
            {
                var downloadHandle = Addressables.DownloadDependenciesAsync(keysToDownload, Addressables.MergeMode.Union);
                yield return downloadHandle;

                if (downloadHandle.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogError("[ResourceManager] 批量下载资源依赖失败！网络异常。");
                }
                
                if (downloadHandle.IsValid())
                {
                    Addressables.Release(downloadHandle);
                }

                // 3. 逐个将 GameObject 加载至内存并存入字典
                foreach (var pKey in uniquePrefabKeys)
                {
                    var reference = prefabRegistry.GetReference(pKey);
                    if (reference != null && reference.RuntimeKeyIsValid())
                    {
                        var handle = Addressables.LoadAssetAsync<GameObject>(reference);
                        yield return handle;

                        if (handle.Status == AsyncOperationStatus.Succeeded)
                        {
                            _gameObjectDict[pKey] = handle.Result;
                            _handlesToRelease.Add(handle);
                        }
                        else
                        {
                            Debug.LogError($"[ResourceManager] 加载预制体 {pKey} 失败！");
                        }
                    }
                }
            }

            // 注意：其他资源（Audio, Texture等）暂时未在 LevelConfig 里声明，如果后续加入，
            // 可以在这里扩展，根据策略“Key就等于资源名称”直接调用 Addressables.LoadAssetAsync(name)

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
}
