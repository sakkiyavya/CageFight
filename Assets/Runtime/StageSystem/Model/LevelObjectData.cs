using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 关卡内单个物体的数据定义
/// </summary>
[Serializable]
public class LevelObjectData
{
    [Tooltip("物体实例唯一 ID，用于运行时寻址与互相引用")]
    public int instanceId;

    [Tooltip("预制体逻辑契约 Key，用于在资源映射表(Registry)中查找实际 GameObject")]
    public string prefabKey;

    [Tooltip("对象的初始空间数据")]
    public TransformData transform;

    [Tooltip("附加的组件数据列表（支持多态）")]
    [SerializeReference]
    public List<ComponentData> components = new List<ComponentData>();
}

