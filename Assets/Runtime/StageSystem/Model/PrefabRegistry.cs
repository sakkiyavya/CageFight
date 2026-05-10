using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PrefabMapping
{
    public string key;
    public GameObject prefab;
}

/// <summary>
/// 预制体注册表 (方案 B 的核心)
/// 用于在运行时将基于纯数据的 string key 映射到实际的 GameObject 资源。
/// </summary>
[CreateAssetMenu(fileName = "PrefabRegistry", menuName = "StageSystem/Prefab Registry")]
public class PrefabRegistry : ScriptableObject
{
    public List<PrefabMapping> mappings = new List<PrefabMapping>();

    private Dictionary<string, GameObject> Dict;

    /// <summary>
    /// 运行时初始化，将 List 转换为 Dictionary 以提升查找速度 O(1)
    /// </summary>
    public void Initialize()
    {
        if (Dict != null) return;
        
        Dict = new Dictionary<string, GameObject>();
        foreach (var mapping in mappings)
        {
            if (!string.IsNullOrEmpty(mapping.key) && !Dict.ContainsKey(mapping.key))
            {
                Dict.Add(mapping.key, mapping.prefab);
            }
        }
    }

    /// <summary>
    /// 根据 key 获取对应的预制体
    /// </summary>
    public GameObject GetPrefab(string key)
    {
        if (Dict == null) Initialize();
        
        if (Dict.TryGetValue(key, out GameObject prefab))
        {
            return prefab;
        }
        Debug.LogError($"[PrefabRegistry] 找不到 Key 为 '{key}' 的预制体！请检查是否忘记重新生成注册表。");
        return null;
    }
}
