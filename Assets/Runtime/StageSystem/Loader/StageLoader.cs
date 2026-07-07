using UnityEngine;

/// <summary>
/// 运行时关卡加载器（单例）
/// 在 ResourceManager.OnLoadComplete 触发后，通过 ResourceManager 获取预制体资源，
/// 还原关卡内所有物体的实体、Transform 与组件数据。
/// </summary>
public class StageLoader : MonoBehaviour
{
    public static StageLoader Instance { get; private set; }

    // 等待实例化的关卡配置，在 StartLoad 时暂存
    private LevelConfig _pendingConfig;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 开始加载关卡：先触发 ResourceManager 预加载资源，
    /// 待加载完成后自动实例化所有关卡物体。
    /// </summary>
    public void StartLoad(LevelConfig config)
    {
        if (config == null)
        {
            Debug.LogError("[LevelLoader] LevelConfig 为 null，无法加载！");
            return;
        }

        if (ResourceManager.Instance == null)
        {
            Debug.LogError("[LevelLoader] ResourceManager 未就绪，无法加载关卡！");
            return;
        }

        _pendingConfig = config;

        // 注册回调，待资源加载完成后实例化
        ResourceManager.Instance.OnLoadComplete += OnResourcesLoaded;

        Debug.Log($"[LevelLoader] 开始预加载关卡 {config.levelId} 的资源...");
        ResourceManager.Instance.LoadStageResources(config);
    }

    /// <summary>
    /// ResourceManager.OnLoadComplete 触发时调用。
    /// 通过 ResourceManager 获取预制体实体，还原关卡所有物体。
    /// </summary>
    private void OnResourcesLoaded()
    {
        // 立即取消注册，避免重复触发
        ResourceManager.Instance.OnLoadComplete -= OnResourcesLoaded;

        if (_pendingConfig == null)
        {
            Debug.LogError("[LevelLoader] OnResourcesLoaded 触发时 _pendingConfig 为 null！");
            return;
        }

        LevelConfig config = _pendingConfig;
        _pendingConfig = null;

        Debug.Log($"[LevelLoader] 资源加载完成，开始实例化关卡 {config.levelId} 的物体...");

        foreach (var objData in config.objects)
        {
            // 通过 ResourceManager 获取已缓存的预制体
            GameObject prefab = ResourceManager.Instance.GetGameObject(objData.prefabKey);
            if (prefab == null)
            {
                Debug.LogWarning($"[LevelLoader] 未找到 Key 为 '{objData.prefabKey}' 的预制体，已跳过。");
                continue;
            }

            if(!GameObjectPool.Instance)
            {
                Debug.LogError("GameObjectPool未初始化");
                return;
            }
            // 实例化
            GameObject instance = GameObjectPool.Instance.Get(prefab);

            // 还原 Transform
            instance.transform.position    = objData.transform.position;
            instance.transform.eulerAngles = objData.transform.rotation;
            instance.transform.localScale  = objData.transform.scale;

            // 将数据重新注入给 ILevelComponent
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
        }

        Debug.Log($"<color=cyan>[LevelLoader] 关卡 {config.levelId} 实机加载完成！</color>");
    }
}
