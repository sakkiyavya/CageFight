using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 关卡内单个物体的数据定义
/// </summary>
[Serializable]
public class StageObjectData
{
    [Tooltip("物体实例唯一 ID，用于运行时寻址与互相引用")]
    public int instanceId;                                                // 关卡内稳定且唯一的对象编号，用于运行时查找和相互引用。

    [Tooltip("预制体逻辑契约 Key，用于在资源映射表(Registry)中查找实际 GameObject")]
    public string prefabKey;                                              // 创建该对象所使用的预制体资源键。

    [Tooltip("对象的初始空间数据")]
    public TransformData transform;                                       // 对象实例化后需要恢复的位置、旋转和缩放。

    [Tooltip("附加的组件数据列表（支持多态）")]
    [SerializeReference]
    public List<ComponentData> components = new List<ComponentData>();    // 需要应用到实例上各功能组件的多态配置数据。
}

