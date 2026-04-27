using UnityEngine;

/// <summary>
/// 运行时的关卡加载器核心系统
/// 负责在实机环境中，通过 PrefabRegistry 读取预制体资源，并还原关卡。
/// </summary>
public class LevelLoader : MonoBehaviour
{
    // 待实现：这里未来需要引入 PrefabRegistry 进行资源寻址。
    // public PrefabRegistry registry;

    /// <summary>
    /// 运行时加载并生成关卡实体的核心入口
    /// </summary>
    public void LoadLevelAtRuntime(LevelConfig config)
    {
        Debug.Log($"运行时准备加载关卡配置：ID={config.levelId}");
        
        foreach (var objData in config.objects)
        {
            // 【占位】
            // 1. GameObject prefab = registry.GetPrefab(objData.prefabKey);
            // 2. GameObject instance = Instantiate(prefab);
            // 3. 还原 Transform ...
            // 4. 遍历 instance 上的 ILevelComponent，调用 ApplyData()...
        }
    }
}
