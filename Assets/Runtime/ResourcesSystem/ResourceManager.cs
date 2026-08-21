using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
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
    [FormerlySerializedAs("editorLevelConfig")]
    public StageConfig editorStageConfig;                                                                                                   // 编辑器中用于直接预览和加载的关卡配置。
    #endif
    public static ResourceManager Instance { get; private set; }

    [Header("各类注册表")]
    [Tooltip("Prefab 映射表，负责将纯文本的 prefabKey 映射到实际的 AssetReference")]
    [SerializeField] private PrefabRegistry prefabRegistry;                                                                                 // 预制体资源键到 Addressables 引用的注册表。

    [Tooltip("Texture 映射表")]
    [SerializeField] private TextureRegistry textureRegistry;                                                                               // 纹理资源键到 Addressables 引用的注册表。

    [Tooltip("Audio 映射表")]
    [SerializeField] private AudioRegistry audioRegistry;                                                                                   // 音频资源键到 Addressables 引用的注册表。

    [Tooltip("AnimationClip 映射表")]
    [SerializeField] private AnimationClipRegistry animationClipRegistry;                                                                   // 动画片段资源键到 Addressables 引用的注册表。

    [Tooltip("AnimatorController 映射表")]
    [SerializeField] private AnimatorControllerRegistry animatorControllerRegistry;                                                         // 动画控制器资源键到 Addressables 引用的注册表。

    [Tooltip("Sprite 映射表（支持多图切片子图）")]
    [SerializeField] private SpriteRegistry spriteRegistry;                                                                                 // 精灵资源键到 Addressables 引用的注册表。

    [Tooltip("工程师、种族与法术的稳定 ID 注册表")]
    [SerializeField] private LoadoutDefinitionRegistry loadoutDefinitionRegistry;

    [Header("局内公共资源（所有关卡自动预载）")]
    [SerializeField] private List<string> commonPrefabs = new List<string> { "Build Animation", "Cast spell", "Bullet-Arrow", "Bullet-Thunderstorm", "ConnectMasterCircle", "Huge cheese", "UPanime", "UnitVisualFollower", "EngineerHealParticles" };
    [SerializeField] private List<string> commonAudios = new List<string> { "Build", "UP", "UI Click", "Arrow-Shoot", "Hit" };
    [SerializeField] private List<string> persistentAudios = new List<string> { "UI Click", "Cage door", "Begin", "Hit", "Huge buff", "Violent", "Zip buff", "False life", "Cold dbuff", "paralysed dbuff", "BOOM.LV.3", "BOOM.LV.1", "Mechanical reinforcement ore" };
    [SerializeField] private List<string> commonAnimatorControllers = new List<string> { "Build Animation AC" };
    [SerializeField] private List<string> commonSprites = new List<string> { "Build Animation_6", "State1 AP", "State1 AP_32", "Bullet3 AP_1", "Buff1 AP_0", "Derivative-Two fool" };

    public ResourceState CurrentState { get; private set; } = ResourceState.None;                                                           // 当前关卡资源加载或卸载所处的阶段。
    public bool IsLoadoutRegistryReady => loadoutDefinitionRegistry != null;

    public event Action OnLoadComplete;                                                                                                     // 当前关卡所需资源全部进入缓存后触发。
    public event Action OnUnloadComplete;                                                                                                   // 已持有资源全部释放并清空缓存后触发。

    // --- 私有缓存字典 ---
    private Dictionary<string, GameObject> _gameObjectDict = new Dictionary<string, GameObject>();                                          // 已加载预制体按资源键建立的缓存。
    private Dictionary<string, AudioClip> _audioDict = new Dictionary<string, AudioClip>();                                                 // 已加载音频片段按资源键建立的缓存。
    private readonly Dictionary<string, AudioClip> _persistentAudioDict = new Dictionary<string, AudioClip>();
    private Dictionary<string, Texture2D> _textureDict = new Dictionary<string, Texture2D>();                                               // 已加载纹理按资源键建立的缓存。
    private Dictionary<string, AnimationClip> _animationDict = new Dictionary<string, AnimationClip>();                                     // 已加载动画片段按资源键建立的缓存。
    private Dictionary<string, RuntimeAnimatorController> _animatorControllerDict = new Dictionary<string, RuntimeAnimatorController>();    // 已加载动画控制器按资源键建立的缓存。
    private Dictionary<string, Sprite> _spriteDict = new Dictionary<string, Sprite>();                                                      // 已加载精灵按资源键建立的缓存。
    public List<string> _spriteKeys = new List<string>();                                                                                   // 当前缓存中可供调试或界面查询的精灵资源键。

    // 统一管理所有加载成功后的句柄，以便统一释放
    private List<AsyncOperationHandle> _handlesToRelease = new List<AsyncOperationHandle>();                                                // 由本管理器持有、卸载关卡时必须释放的 Addressables 句柄。
    private readonly List<AsyncOperationHandle> _persistentAudioHandles = new List<AsyncOperationHandle>();

    private static IEnumerable<string> MergeKeys(List<string> stageKeys, List<string> commonKeys)
    {
        if (stageKeys != null)
            foreach (string key in stageKeys) yield return key;
        if (commonKeys != null)
            foreach (string key in commonKeys) yield return key;
    }

    #region 生命周期与回调
    /// <summary>
    /// 建立资源管理器单例，并在编辑器中同步查找注册表，或在玩家构建中启动注册表异步加载。
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        EnsureCommonResources();

        // 加载所有 Registry：Editor 下直接从文件系统加载，Runtime 通过 Addressables 加载
#if UNITY_EDITOR
        prefabRegistry           = LoadRegistryEditor<PrefabRegistry>();
        textureRegistry          = LoadRegistryEditor<TextureRegistry>();
        audioRegistry            = LoadRegistryEditor<AudioRegistry>();
        animationClipRegistry    = LoadRegistryEditor<AnimationClipRegistry>();
        animatorControllerRegistry = LoadRegistryEditor<AnimatorControllerRegistry>();
        spriteRegistry           = LoadRegistryEditor<SpriteRegistry>();
        loadoutDefinitionRegistry = LoadRegistryEditor<LoadoutDefinitionRegistry>();
        audioRegistry?.Initialize();
        StartCoroutine(PreloadPersistentAudios());

#else
        StartCoroutine(LoadAllRegistriesRuntime());
#endif
    }

    private void EnsureCommonResources()
    {
        if (commonPrefabs == null) commonPrefabs = new List<string>();
        if (commonAudios == null) commonAudios = new List<string>();
        if (commonAnimatorControllers == null) commonAnimatorControllers = new List<string>();
        if (commonSprites == null) commonSprites = new List<string>();

        if (!commonPrefabs.Contains("Build Animation")) commonPrefabs.Add("Build Animation");
        if (!commonPrefabs.Contains("Cast spell")) commonPrefabs.Add("Cast spell");
        if (!commonPrefabs.Contains("Bullet-Arrow")) commonPrefabs.Add("Bullet-Arrow");
        if (!commonPrefabs.Contains("Bullet-Thunderstorm")) commonPrefabs.Add("Bullet-Thunderstorm");
        if (!commonPrefabs.Contains("ConnectMasterCircle")) commonPrefabs.Add("ConnectMasterCircle");
        if (!commonPrefabs.Contains("Huge cheese")) commonPrefabs.Add("Huge cheese");
        if (!commonPrefabs.Contains("UPanime")) commonPrefabs.Add("UPanime");
        if (!commonPrefabs.Contains("UnitVisualFollower")) commonPrefabs.Add("UnitVisualFollower");
        if (!commonPrefabs.Contains("EngineerHealParticles")) commonPrefabs.Add("EngineerHealParticles");
        if (!commonAudios.Contains("Build")) commonAudios.Add("Build");
        if (!commonAudios.Contains("UP")) commonAudios.Add("UP");
        if (!commonAudios.Contains("UI Click")) commonAudios.Add("UI Click");
        if (!commonAudios.Contains("Arrow-Shoot")) commonAudios.Add("Arrow-Shoot");
        if (!commonAudios.Contains("Hit")) commonAudios.Add("Hit");
        if (persistentAudios == null) persistentAudios = new List<string>();
        if (!persistentAudios.Contains("UI Click")) persistentAudios.Add("UI Click");
        if (!persistentAudios.Contains("Cage door")) persistentAudios.Add("Cage door");
        if (!persistentAudios.Contains("Begin")) persistentAudios.Add("Begin");
        if (!persistentAudios.Contains("Hit")) persistentAudios.Add("Hit");
        if (!persistentAudios.Contains("Huge buff")) persistentAudios.Add("Huge buff");
        if (!persistentAudios.Contains("Violent")) persistentAudios.Add("Violent");
        if (!persistentAudios.Contains("Zip buff")) persistentAudios.Add("Zip buff");
        if (!persistentAudios.Contains("False life")) persistentAudios.Add("False life");
        if (!persistentAudios.Contains("Cold dbuff")) persistentAudios.Add("Cold dbuff");
        if (!persistentAudios.Contains("paralysed dbuff")) persistentAudios.Add("paralysed dbuff");
        if (!persistentAudios.Contains("BOOM.LV.3")) persistentAudios.Add("BOOM.LV.3");
        if (!persistentAudios.Contains("BOOM.LV.1")) persistentAudios.Add("BOOM.LV.1");
        if (!persistentAudios.Contains("Mechanical reinforcement ore")) persistentAudios.Add("Mechanical reinforcement ore");
        if (!commonAnimatorControllers.Contains("Build Animation AC")) commonAnimatorControllers.Add("Build Animation AC");
        if (!commonSprites.Contains("Build Animation_6")) commonSprites.Add("Build Animation_6");
        if (!commonSprites.Contains("State1 AP")) commonSprites.Add("State1 AP");
        if (!commonSprites.Contains("State1 AP_32")) commonSprites.Add("State1 AP_32");
        if (!commonSprites.Contains("Bullet3 AP_1")) commonSprites.Add("Bullet3 AP_1");
        if (!commonSprites.Contains("Buff1 AP_0")) commonSprites.Add("Buff1 AP_0");
        if (!commonSprites.Contains("Derivative-Two fool")) commonSprites.Add("Derivative-Two fool");
    }

    /// <summary>
    /// 当前资源管理器销毁时清除静态单例引用。
    /// </summary>
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
    #endregion

    #region 注册表加载与初始化
#if UNITY_EDITOR
    /// <summary>
    /// 编辑器下：从 AssetDatabase 搜索并加载指定类型的 Registry SO。
    /// 文件名和类名相同（如 PrefabRegistry.asset）。
    /// </summary>
    /// <typeparam name="T">需要查找的注册表 ScriptableObject 类型。</typeparam>
    /// <returns>找到的第一个注册表资产；没有匹配资产或加载失败时返回 <see langword="null"/>。</returns>
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
    /// 在玩家构建中按类型名依次加载全部资源注册表，并在全部加载完成后初始化各注册表的键索引。
    /// </summary>
    /// <returns>等待所有注册表 Addressables 请求依次完成的协程。</returns>
    private IEnumerator LoadAllRegistriesRuntime()
    {
        yield return LoadRegistryRuntime<PrefabRegistry>(r           => prefabRegistry = r);
        yield return LoadRegistryRuntime<TextureRegistry>(r          => textureRegistry = r);
        yield return LoadRegistryRuntime<AudioRegistry>(r            => audioRegistry = r);
        yield return LoadRegistryRuntime<AnimationClipRegistry>(r    => animationClipRegistry = r);
        yield return LoadRegistryRuntime<AnimatorControllerRegistry>(r => animatorControllerRegistry = r);
        yield return LoadRegistryRuntime<SpriteRegistry>(r           => spriteRegistry = r);
        yield return LoadRegistryRuntime<LoadoutDefinitionRegistry>(r => loadoutDefinitionRegistry = r);

        // 初始化已加载的 Registry
        prefabRegistry?.Initialize();
        textureRegistry?.Initialize();
        audioRegistry?.Initialize();
        animationClipRegistry?.Initialize();
        animatorControllerRegistry?.Initialize();
        spriteRegistry?.Initialize();
        loadoutDefinitionRegistry?.Initialize();

        yield return PreloadPersistentAudios();

        Debug.Log("[ResourceManager] 所有 Registry 加载完成。");
    }

    /// <summary>
    /// 使用注册表类型名作为 Addressables 键加载单个注册表，保存成功句柄并通过回调返回结果。
    /// </summary>
    /// <typeparam name="T">需要加载的注册表 ScriptableObject 类型。</typeparam>
    /// <param name="onDone">加载完成后接收注册表实例或空值的回调。</param>
    /// <returns>等待单个注册表加载完成的协程。</returns>
    private IEnumerator LoadRegistryRuntime<T>(Action<T> onDone) where T : ScriptableObject
    {
        string key = typeof(T).Name;                                                                                                        // 注册表使用的 Addressables 键。
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
    #endregion


    
    #region 缓存资源查询
    /// <summary>
    /// 从当前关卡的预制体缓存中按资源键查询对象。
    /// </summary>
    /// <param name="key">预制体资源键。</param>
    /// <returns>已加载的预制体；缓存中不存在时返回 <see langword="null"/>。</returns>
    public GameObject GetGameObject(string key)
    {
        return _gameObjectDict.TryGetValue(key, out var res) ? res : null;
    }

    /// <summary>
    /// 从当前关卡的音频缓存中按资源键查询片段。
    /// </summary>
    /// <param name="key">音频资源键。</param>
    /// <returns>已加载的音频片段；缓存中不存在时返回 <see langword="null"/>。</returns>
    public AudioClip GetAudio(string key)
    {
        if (_audioDict.TryGetValue(key, out var res)) return res;
        return _persistentAudioDict.TryGetValue(key, out res) ? res : null;
    }

    private IEnumerator PreloadPersistentAudios()
    {
        if (persistentAudios == null) yield break;
        while (audioRegistry == null) yield return null;
        audioRegistry.Initialize();

        foreach (string key in persistentAudios)
        {
            if (string.IsNullOrEmpty(key) || _persistentAudioDict.ContainsKey(key)) continue;
            object address = audioRegistry.GetReference(key);
            if (address == null) continue;
            var handle = Addressables.LoadAssetAsync<AudioClip>(address);
            yield return handle;
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _persistentAudioDict[key] = handle.Result;
                _persistentAudioHandles.Add(handle);
            }
            else Debug.LogError($"[ResourceManager] 持久音频加载失败：{key}");
        }
    }

    /// <summary>
    /// 从当前关卡的纹理缓存中按资源键查询纹理。
    /// </summary>
    /// <param name="key">纹理资源键。</param>
    /// <returns>已加载的纹理；缓存中不存在时返回 <see langword="null"/>。</returns>
    public Texture2D GetTexture(string key)
    {
        return _textureDict.TryGetValue(key, out var res) ? res : null;
    }

    /// <summary>
    /// 从当前关卡的动画缓存中按资源键查询动画片段。
    /// </summary>
    /// <param name="key">动画片段资源键。</param>
    /// <returns>已加载的动画片段；缓存中不存在时返回 <see langword="null"/>。</returns>
    public AnimationClip GetAnimation(string key)
    {
        return _animationDict.TryGetValue(key, out var res) ? res : null;
    }

    /// <summary>
    /// 从当前关卡的动画控制器缓存中按资源键查询控制器。
    /// </summary>
    /// <param name="key">动画控制器资源键。</param>
    /// <returns>已加载的动画控制器；缓存中不存在时返回 <see langword="null"/>。</returns>
    public RuntimeAnimatorController GetAnimatorController(string key)
    {
        return _animatorControllerDict.TryGetValue(key, out var res) ? res : null;
    }

    /// <summary>
    /// 从当前关卡的精灵缓存中按资源键查询精灵。
    /// </summary>
    /// <param name="key">精灵资源键。</param>
    /// <returns>已加载的精灵；缓存中不存在时返回 <see langword="null"/>。</returns>
    public Sprite GetSprite(string key)
    {
        // Debug.Log("ResourceManager.GetSprit:" + key + "  " + _spriteDict.ContainsKey(key));
        return _spriteDict.TryGetValue(key, out var res) ? res : null;
    }

    #region 选装定义查询（负责人决议 2026-08-22：保留并正式接管为框架 API）
    /// <summary>
    /// 选装（工程师/种族/法术）定义查询入口。数据归属：全部选装定义来自 LoadoutDefinitionRegistry
    /// （编辑器构建生成、ResourceManager 初始化期解析缓存）。加载时机：仅菜单/出战准备期查询，
    /// 禁止在战斗热路径查询。失败策略：ID 未注册时返回 false/空，调用方须给出可定位警告。
    /// 调用边界：仅选装 UI、出战生成（PlayerLoadoutSpawner）与法术条可依赖本组 API。
    /// </summary>
    /// <summary>按稳定 ID 解析工程师定义；定义资源只从 LoadoutDefinitionRegistry 取得。</summary>
    public bool TryGetEngineer(string id, out EngineerDefinition definition)
    {
        definition = null;
        return loadoutDefinitionRegistry &&
            loadoutDefinitionRegistry.TryGetEngineer(id, out definition);
    }

    /// <summary>按稳定 ID 解析种族定义；定义资源只从 LoadoutDefinitionRegistry 取得。</summary>
    public bool TryGetRace(string id, out RaceDefinition definition)
    {
        definition = null;
        return loadoutDefinitionRegistry &&
            loadoutDefinitionRegistry.TryGetRace(id, out definition);
    }

    /// <summary>按稳定 ID 解析法术定义；定义资源只从 LoadoutDefinitionRegistry 取得。</summary>
    public bool TryGetSpell(string id, out SpellDefinition definition)
    {
        definition = null;
        return loadoutDefinitionRegistry &&
            loadoutDefinitionRegistry.TryGetSpell(id, out definition);
    }

    /// <summary>取得注册表定义的初始出战配置；缺少注册表时返回 false。</summary>
    public bool TryGetDefaultLoadout(
        out string engineerId,
        out string raceId,
        out string spellSlot1Id,
        out string spellSlot2Id)
    {
        engineerId = raceId = spellSlot1Id = spellSlot2Id = string.Empty;
        return loadoutDefinitionRegistry && loadoutDefinitionRegistry.TryGetDefaultLoadout(
            out engineerId,
            out raceId,
            out spellSlot1Id,
            out spellSlot2Id);
    }

    public IReadOnlyList<EngineerDefinition> EngineerDefinitions =>
        loadoutDefinitionRegistry ? loadoutDefinitionRegistry.EngineerDefinitions :
        Array.Empty<EngineerDefinition>();

    public IReadOnlyList<RaceDefinition> RaceDefinitions =>
        loadoutDefinitionRegistry ? loadoutDefinitionRegistry.RaceDefinitions :
        Array.Empty<RaceDefinition>();

    public IReadOnlyList<SpellDefinition> SpellDefinitions =>
        loadoutDefinitionRegistry ? loadoutDefinitionRegistry.SpellDefinitions :
        Array.Empty<SpellDefinition>();
    #endregion

    /// <summary>
    /// 通过 PrefabRegistry 预载一个预制体到当前缓存。该协程仅用于菜单和关卡加载期，
    /// 不应从攻击或逐帧逻辑调用。
    /// </summary>
    public IEnumerator LoadRegisteredGameObject(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || _gameObjectDict.ContainsKey(key)) yield break;
        if (!prefabRegistry)
        {
            Debug.LogError("[ResourceManager] PrefabRegistry 未就绪，无法预载选装资源。", this);
            yield break;
        }

#if UNITY_EDITOR
        GameObject editorPrefab = prefabRegistry.GetPrefab(key);
        if (editorPrefab)
        {
            _gameObjectDict[key] = editorPrefab;
            yield break;
        }
#endif

        AssetReferenceGameObject reference = prefabRegistry.GetReference(key);
        if (reference == null || !reference.RuntimeKeyIsValid())
        {
            Debug.LogError($"[ResourceManager] 未注册预制体 Key：{key}", this);
            yield break;
        }

        var handle = Addressables.LoadAssetAsync<GameObject>(reference);
        yield return handle;
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            _gameObjectDict[key] = handle.Result;
            _handlesToRelease.Add(handle);
        }
        else
        {
            Debug.LogError($"[ResourceManager] 预载预制体失败：{key}", this);
        }
    }

    /// <summary>
    /// 通过 SpriteRegistry 预载一个图标到当前缓存。该协程仅用于菜单和关卡加载期。
    /// </summary>
    public IEnumerator LoadRegisteredSprite(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || _spriteDict.ContainsKey(key)) yield break;
        if (!spriteRegistry)
        {
            Debug.LogError("[ResourceManager] SpriteRegistry 未就绪，无法预载选装图标。", this);
            yield break;
        }

#if UNITY_EDITOR
        Sprite editorSprite = spriteRegistry.GetAsset(key);
        if (editorSprite)
        {
            _spriteDict[key] = editorSprite;
            if (!_spriteKeys.Contains(key)) _spriteKeys.Add(key);
            yield break;
        }
#endif

        AssetReferenceT<Sprite> reference = spriteRegistry.GetReference(key);
        if (reference == null || !reference.RuntimeKeyIsValid())
        {
            Debug.LogError($"[ResourceManager] 未注册 Sprite Key：{key}", this);
            yield break;
        }

        var handle = Addressables.LoadAssetAsync<Sprite>(reference);
        yield return handle;
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            _spriteDict[key] = handle.Result;
            if (!_spriteKeys.Contains(key)) _spriteKeys.Add(key);
            _handlesToRelease.Add(handle);
        }
        else
        {
            Debug.LogError($"[ResourceManager] 预载图标失败：{key}", this);
        }
    }
    #endregion

    // --- 生命周期管理 ---

    #region 关卡资源加载
    /// <summary>
    /// 清理上一关资源后启动当前关卡所需资源的异步预加载。
    /// 已处于加载状态或全部注册表均不可用时拒绝重复请求。
    /// </summary>
    /// <param name="stage">需要扫描并加载资源的关卡配置。</param>
    /// <returns>是否成功启动加载协程。</returns>
    public bool LoadStageResources(StageConfig stage)
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
        StartCoroutine(CoLoadStageResources(stage));
        return true;
    }

    /// <summary>
    /// 检查并更新 Addressables Catalog，扫描关卡配置中的资源键和目标类型，
    /// 按对应注册表解析并加载资源，写入类型缓存后发布加载完成事件。
    /// </summary>
    /// <param name="stage">提供全部关卡对象及资源键的关卡配置。</param>
    /// <returns>等待 Catalog 更新和所有资源加载完成的协程。</returns>
    private IEnumerator CoLoadStageResources(StageConfig stage)
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

        // 1. 根据 StageConfig 的 5 个分类列表，收集所有资源 Key 并通过对应的 Registry 解析真实句柄
        Dictionary<string, Type> keysWithTypes = new Dictionary<string, Type>();                                                            // 每个逻辑资源键期望加载成的 Unity 资源类型。
        Dictionary<string, object> keysWithAddressableKeys = new Dictionary<string, object>();                                              // 逻辑资源键对应的 Addressables 运行时键或安全引用。

        // 处理 Prefabs (GameObject)
        foreach (string key in MergeKeys(stage.prefabs, commonPrefabs))
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

        // 处理 Audios (AudioClip)
        foreach (string key in MergeKeys(stage.audios, commonAudios))
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

        // 处理 Textures (Texture2D)
        foreach (string key in MergeKeys(stage.textures, null))
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

        // 处理 AnimationClips (AnimationClip)
        foreach (string key in MergeKeys(stage.animationClips, null))
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

        // 处理 AnimatorControllers (RuntimeAnimatorController)
        foreach (string key in MergeKeys(stage.animatorControllers, commonAnimatorControllers))
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

        // 处理 Sprites (Sprite)
        foreach (string key in MergeKeys(stage.sprites, commonSprites))
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

        List<object> keysToDownload = new List<object>();                                                                                   // 合并下载依赖时提交给 Addressables 的运行时键集合。
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
#if UNITY_EDITOR
                    // 编辑器直引快路径：按注册表的直接 sprite 引用解析，与 m_SubObjectName 无关，
                    // 保证编辑器内测试不受注册表重载时序影响（运行时构建仍走 Addressables 子对象名）。
                    Sprite editorSprite = spriteRegistry != null ? spriteRegistry.GetAsset(key) : null;
                    if (editorSprite)
                    {
                        _spriteDict[key] = editorSprite;
                        _spriteKeys.Add(key);
                        continue;
                    }
#endif
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
    #endregion

    #region 额外资源加载
    /// <summary>
    /// 启动加载额外资源的协程，并在成功后写入相应类型缓存。
    /// 资源键优先按对应注册表解析；未注册时按原始 Addressables 键加载。
    /// </summary>
    /// <typeparam name="T">需要加载的 Unity 资源类型。</typeparam>
    /// <param name="key">资源注册表键或 Addressables 键；成功后以该键写入缓存。</param>
    /// <param name="onComplete">加载结束后接收资源实例或空值的可选回调。</param>
    public void LoadExtraResourceAsync<T>(string key, Action<T> onComplete = null) where T : UnityEngine.Object
    {
        StartCoroutine(CoLoadExtraResource(key, onComplete));
    }

    /// <summary>
    /// 异步加载单个额外资源，保存有效句柄，并根据泛型类型写入对应缓存后调用完成回调。
    /// 资源键优先按对应注册表解析为 AssetReference；未注册时按原始 Addressables 键加载（兼容既有调用）。
    /// </summary>
    /// <typeparam name="T">需要加载的 Unity 资源类型。</typeparam>
    /// <param name="key">资源注册表键或 Addressables 键；成功后以该键写入对应类型缓存。</param>
    /// <param name="onComplete">加载结束后接收资源实例或空值的回调。</param>
    /// <returns>等待 Addressables 加载完成的协程。</returns>
    private IEnumerator CoLoadExtraResource<T>(string key, Action<T> onComplete) where T : UnityEngine.Object
    {
#if UNITY_EDITOR
        // 编辑器：注册表直接引用可用时立即缓存并完成。
        // 多精灵图集必须按子对象精确解析；Addressables 引用未携带子对象名时只能取到
        // 图集主对象（第一张精灵），编辑器直引用路径可保证每个键命中正确的精灵。
        T direct = ResolveDirectAsset<T>(key);
        if (direct != null)
        {
            CacheLoadedAsset(key, direct);
            onComplete?.Invoke(direct);
            yield break;
        }
#endif

        var handle = Addressables.LoadAssetAsync<T>(ResolveRegisteredKey<T>(key));
        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            _handlesToRelease.Add(handle);
            CacheLoadedAsset(key, handle.Result);
            onComplete?.Invoke(handle.Result);
        }
        else
        {
            Debug.LogError($"[ResourceManager] 加载额外资源失败！Key: {key}, Type: {typeof(T)}");
            onComplete?.Invoke(null);
        }
    }

    /// <summary>按泛型类型把加载结果写入对应缓存字典。</summary>
    private void CacheLoadedAsset<T>(string key, T result) where T : UnityEngine.Object
    {
        if (typeof(T) == typeof(GameObject)) _gameObjectDict[key] = result as GameObject;
        else if (typeof(T) == typeof(Sprite))
        {
            _spriteDict[key] = result as Sprite;
            if (!_spriteKeys.Contains(key)) _spriteKeys.Add(key);
        }
        else if (typeof(T) == typeof(AudioClip)) _audioDict[key] = result as AudioClip;
        else if (typeof(T) == typeof(Texture2D)) _textureDict[key] = result as Texture2D;
        else if (typeof(T) == typeof(AnimationClip)) _animationDict[key] = result as AnimationClip;
        else if (typeof(T) == typeof(RuntimeAnimatorController)) _animatorControllerDict[key] = result as RuntimeAnimatorController;
    }

#if UNITY_EDITOR
    /// <summary>编辑器专用：按类型从对应注册表取得资源的直接引用（无则返回 null）。</summary>
    private T ResolveDirectAsset<T>(string key) where T : UnityEngine.Object
    {
        if (typeof(T) == typeof(GameObject) && prefabRegistry != null)
            return prefabRegistry.GetPrefab(key) as T;
        if (typeof(T) == typeof(Sprite) && spriteRegistry != null)
            return spriteRegistry.GetAsset(key) as T;
        if (typeof(T) == typeof(AudioClip) && audioRegistry != null)
            return audioRegistry.GetAsset(key) as T;
        if (typeof(T) == typeof(Texture2D) && textureRegistry != null)
            return textureRegistry.GetAsset(key) as T;
        if (typeof(T) == typeof(AnimationClip) && animationClipRegistry != null)
            return animationClipRegistry.GetAsset(key) as T;
        if (typeof(T) == typeof(RuntimeAnimatorController) && animatorControllerRegistry != null)
            return animatorControllerRegistry.GetAsset(key) as T;
        return null;
    }
#endif

    /// <summary>
    /// 按泛型类型从对应注册表把资源键解析为 AssetReference（经 RuntimeKeyIsValid 校验）；
    /// 未注册时回退为原始 Addressables 键。
    /// </summary>
    private object ResolveRegisteredKey<T>(string key)
    {
        if (typeof(T) == typeof(GameObject) && prefabRegistry != null)
        {
            AssetReferenceGameObject reference = prefabRegistry.GetReference(key);
            if (reference != null && reference.RuntimeKeyIsValid()) return reference;
        }
        else if (typeof(T) == typeof(Sprite) && spriteRegistry != null)
        {
            AssetReferenceT<Sprite> reference = spriteRegistry.GetReference(key);
            if (reference != null && reference.RuntimeKeyIsValid()) return reference;
        }
        else if (typeof(T) == typeof(AudioClip) && audioRegistry != null)
        {
            AssetReferenceT<AudioClip> reference = audioRegistry.GetReference(key);
            if (reference != null && reference.RuntimeKeyIsValid()) return reference;
        }
        else if (typeof(T) == typeof(Texture2D) && textureRegistry != null)
        {
            AssetReferenceT<Texture2D> reference = textureRegistry.GetReference(key);
            if (reference != null && reference.RuntimeKeyIsValid()) return reference;
        }
        else if (typeof(T) == typeof(AnimationClip) && animationClipRegistry != null)
        {
            AssetReferenceT<AnimationClip> reference = animationClipRegistry.GetReference(key);
            if (reference != null && reference.RuntimeKeyIsValid()) return reference;
        }
        else if (typeof(T) == typeof(RuntimeAnimatorController) && animatorControllerRegistry != null)
        {
            AssetReferenceT<RuntimeAnimatorController> reference = animatorControllerRegistry.GetReference(key);
            if (reference != null && reference.RuntimeKeyIsValid()) return reference;
        }

        return key;
    }
    #endregion

    #region 资源卸载
    /// <summary>
    /// 清空当前关卡的全部类型缓存，释放资源和注册表句柄，并发布卸载完成事件。
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
    #endregion
}

