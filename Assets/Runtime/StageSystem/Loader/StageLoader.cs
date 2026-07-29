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
    private StageConfig _pendingConfig;                                                       // 等待资源加载完成后实例化的关卡配置。

    #region 生命周期与回调
    /// <summary>
    /// 建立跨场景保留的关卡加载器单例；重复实例会被销毁。
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    #endregion

    #region 游戏逻辑
    /// <summary>
    /// 保存待加载配置、订阅资源完成事件，并请求资源管理器预加载关卡所需资源。
    /// 资源加载完成后会自动调用 <see cref="OnResourcesLoaded"/> 还原关卡对象。
    /// </summary>
    /// <param name="config">需要预加载并实例化的关卡配置。</param>
    public void StartLoad(StageConfig config)
    {
        if (config == null)
        {
            Debug.LogError("[StageLoader] StageConfig 为 null，无法加载！");
            return;
        }

        if (ResourceManager.Instance == null)
        {
            Debug.LogError("[StageLoader] ResourceManager 未就绪，无法加载关卡！");
            return;
        }

        _pendingConfig = config;

        // 注册回调，待资源加载完成后实例化
        ResourceManager.Instance.OnLoadComplete += OnResourcesLoaded;

        Debug.Log($"[StageLoader] 开始预加载关卡 {config.stageId} 的资源...");
        ResourceManager.Instance.LoadStageResources(config);
    }
    #endregion

    #region 生命周期与回调
    /// <summary>
    /// 响应资源加载完成事件，取消一次性订阅，并使用已缓存的预制体还原关卡对象、
    /// Transform 和所有类型匹配的组件数据。
    /// </summary>
    private void OnResourcesLoaded()
    {
        // 立即取消注册，避免重复触发
        ResourceManager.Instance.OnLoadComplete -= OnResourcesLoaded;

        if (_pendingConfig == null)
        {
            Debug.LogError("[StageLoader] OnResourcesLoaded 触发时 _pendingConfig 为 null！");
            return;
        }

        StageConfig config = _pendingConfig;                                                  // 本次准备实例化的关卡配置。
        _pendingConfig = null;

        Debug.Log($"[StageLoader] 资源加载完成，开始实例化关卡 {config.stageId} 的物体...");

        foreach (var objData in config.objects)
        {
            // 通过 ResourceManager 获取已缓存的预制体
            GameObject prefab = ResourceManager.Instance.GetGameObject(objData.prefabKey);    // 当前关卡对象对应的已加载预制体。
            if (prefab == null)
            {
                Debug.LogWarning($"[StageLoader] 未找到 Key 为 '{objData.prefabKey}' 的预制体，已跳过。");
                continue;
            }

            if(!GameObjectPool.Instance)
            {
                Debug.LogError("GameObjectPool未初始化");
                return;
            }
            // 实例化
            GameObject instance = GameObjectPool.Instance.Get(prefab);                        // 从对象池取得的关卡实例。

            // 还原 Transform
            instance.transform.position    = objData.transform.position;
            instance.transform.eulerAngles = objData.transform.rotation;
            instance.transform.localScale  = objData.transform.scale;

            // 将数据重新注入给 IStageComponent
            var stageComponents = instance.GetComponentsInChildren<IStageComponent>(true);
            foreach (var savedData in objData.components)
            {
                foreach (var comp in stageComponents)
                {
                    if (comp.DataType == savedData.GetType())
                    {
                        comp.ApplyData(savedData);
                        break;
                    }
                }
            }
        }

        Debug.Log($"<color=cyan>[StageLoader] 关卡 {config.stageId} 实机加载完成！</color>");
    }
    #endregion
}
