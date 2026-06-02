using System;
using UnityEngine;

/// <summary>
/// 标记字符串类型字段为资源 Key 的特性
/// 允许通过参数限定该 Key 对应的期望资源类型（如 GameObject, AudioClip 等）
/// 供 ResourceManager 运行时扫描加载，以及 Editor 面板提供类型约束的拖拽功能
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public class ResourceKeyAttribute : PropertyAttribute
{
    public Type ResourceType { get; private set; }

    public ResourceKeyAttribute(Type resourceType)
    {
        ResourceType = resourceType;
    }
}
