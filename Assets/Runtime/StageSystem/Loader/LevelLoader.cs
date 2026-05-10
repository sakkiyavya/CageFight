using UnityEngine;

/// <summary>
/// 运行时的关卡加载器核心系统
/// 负责在实机环境中，通过 PrefabRegistry 读取预制体资源，并还原关卡。
/// </summary>
public class LevelLoader : MonoBehaviour
{
    [Tooltip("请将生成的 PrefabRegistry 拖入此处")]
    public PrefabRegistry registry;

    /// <summary>
    /// 运行时加载并生成关卡实体的核心入口
    /// </summary>
    public void LoadLevelAtRuntime(LevelConfig config)
    {
        if (registry == null)
        {
            Debug.LogError("LevelLoader 缺少 PrefabRegistry 引用，无法加载资源！");
            return;
        }

        Debug.Log($"运行时准备加载关卡配置：ID={config.levelId}");
        
        foreach (var objData in config.objects)
        {
            // 1. 通过字典高速查找到资源实体
            GameObject prefab = registry.GetPrefab(objData.prefabKey);
            if (prefab == null) continue;

            // 2. 实机实例化
            GameObject instance = Instantiate(prefab);

            // 3. 还原 Transform
            instance.transform.position = objData.transform.position;
            instance.transform.eulerAngles = objData.transform.rotation;
            instance.transform.localScale = objData.transform.scale;

            // 4. 将提取出来的数据重新注入给组件
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
        
        Debug.Log($"<color=cyan>关卡 {config.levelId} 实机加载完成！</color>");
    }
}
