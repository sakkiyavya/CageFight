using UnityEngine;

/// <summary>
/// 关卡对象实例化工具：资源加载完成后，根据 StageConfig 还原关卡实体。
/// </summary>
public static class StageObjectInstantiator
{
    public static bool InstantiateStage(StageConfig config)
    {
        if (config == null)
        {
            Debug.LogError("[StageObjectInstantiator] StageConfig 为空，无法实例化关卡！");
            return false;
        }

        if (ResourceManager.Instance == null)
        {
            Debug.LogError("[StageObjectInstantiator] ResourceManager 未初始化！");
            return false;
        }

        if (GameObjectPool.Instance == null)
        {
            Debug.LogError("[StageObjectInstantiator] GameObjectPool 未初始化！");
            return false;
        }

        if (config.objects == null)
        {
            Debug.LogWarning($"[StageObjectInstantiator] 关卡 {config.stageId} 没有可实例化的对象。");
            return true;
        }

        int spawnedCount = 0;
        foreach (var objData in config.objects)
        {
            if (objData == null || string.IsNullOrEmpty(objData.prefabKey))
                continue;

            GameObject prefab = ResourceManager.Instance.GetGameObject(objData.prefabKey);
            if (prefab == null)
            {
                Debug.LogWarning($"[StageObjectInstantiator] 找不到预制体 Key：{objData.prefabKey}，跳过该对象。");
                continue;
            }

            GameObject instance = GameObjectPool.Instance.Get(prefab);
            if (instance == null)
            {
                Debug.LogError($"[StageObjectInstantiator] 对象池无法生成预制体：{objData.prefabKey}");
                return false;
            }

            instance.transform.position = objData.transform.position;
            instance.transform.eulerAngles = objData.transform.rotation;
            instance.transform.localScale = objData.transform.scale;

            var stageComponents = instance.GetComponentsInChildren<IStageComponent>(true);
            if (objData.components != null)
            {
                foreach (var savedData in objData.components)
                {
                    if (savedData == null)
                        continue;

                    foreach (var component in stageComponents)
                    {
                        if (component.DataType == savedData.GetType())
                        {
                            component.ApplyData(savedData);
                            break;
                        }
                    }
                }
            }

            spawnedCount++;
        }

        Debug.Log($"[StageObjectInstantiator] 关卡 {config.stageId} 实例化完成，共生成 {spawnedCount} 个对象。");
        return true;
    }
}
