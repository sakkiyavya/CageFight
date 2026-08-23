using System;
using UnityEngine;

/// <summary>
/// 标记 string 或 string 集合字段为资源 Key 的特性
/// 允许通过参数限定该 Key 对应的期望资源类型（如 GameObject, AudioClip 等）
/// 供 ResourceManager 运行时扫描加载，以及 Editor 面板提供类型约束的拖拽功能
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public class ResourceKeyAttribute : PropertyAttribute
{
    public Type ResourceType { get; private set; }

    #region 公开接口
    /// <summary>
    /// 创建资源键标记，并记录该字段期望引用的资源类型。
    /// </summary>
    /// <param name="resourceType">资源键对应的资源类型，例如 <see cref="GameObject"/> 或 <see cref="AudioClip"/>。</param>
    public ResourceKeyAttribute(Type resourceType)
    {
        ResourceType = resourceType;
    }
    #endregion
}
