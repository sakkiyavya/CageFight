using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class PrefabRegistryBuilder
{
    // 将注册表存放在统一的 Resource 目录下，这样它就能被打包，或者被 Addressables 轻松引用
    private const string REGISTRY_PATH = "Assets/Resource/PrefabRegistry.asset";

    [MenuItem("关卡构建/一键生成预制体注册表")]
    public static void BuildRegistry()
    {
        // 1. 寻找或创建 Registry SO
        PrefabRegistry registry = AssetDatabase.LoadAssetAtPath<PrefabRegistry>(REGISTRY_PATH);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<PrefabRegistry>();
            
            string dir = System.IO.Path.GetDirectoryName(REGISTRY_PATH);
            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }
            
            AssetDatabase.CreateAsset(registry, REGISTRY_PATH);
        }

        registry.mappings.Clear();

        // 2. 收集工程里所有关卡配置，找出里面用到的所有 prefabKey
        HashSet<string> usedKeys = new HashSet<string>();
        string[] configGuids = AssetDatabase.FindAssets("t:LevelConfig");
        foreach (var guid in configGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            LevelConfig config = AssetDatabase.LoadAssetAtPath<LevelConfig>(path);
            if (config != null && config.objects != null)
            {
                foreach (var obj in config.objects)
                {
                    if (!string.IsNullOrEmpty(obj.prefabKey))
                    {
                        usedKeys.Add(obj.prefabKey);
                    }
                }
            }
        }

        // 3. 遍历工程寻找对应的 Prefab 进行绑定
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int addedCount = 0;

        foreach (var guid in prefabGuids)
        {
            // 如果所有需要的 key 都已经找到了，提前结束搜索提升速度
            if (usedKeys.Count == 0) break;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            
            if (prefab != null)
            {
                // 我们系统目前的契约：prefabKey 就是 prefab 的 name
                if (usedKeys.Contains(prefab.name))
                {
                    registry.mappings.Add(new PrefabMapping { key = prefab.name, prefab = prefab });
                    usedKeys.Remove(prefab.name); // 绑定成功，从待寻找列表中移除
                    addedCount++;
                }
            }
        }

        // 4. 错误报告：是否有配置里写了，但是工程里已经被删掉的 Prefab
        if (usedKeys.Count > 0)
        {
            string missing = string.Join(", ", usedKeys);
            Debug.LogError($"<color=red>[预警]</color> 以下 prefabKey 在工程中未找到对应预制体模型: {missing}");
        }

        // 5. 保存资产
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("绑定成功", $"预制体注册表生成完毕！\n\n共成功绑定了 {addedCount} 个在关卡中使用到的预制体。\n存放路径: {REGISTRY_PATH}", "确定");
    }
}
