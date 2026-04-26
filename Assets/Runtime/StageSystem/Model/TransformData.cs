using System;
using UnityEngine;


/// <summary>
/// 纯数据驱动的 Transform 数据结构，完全脱离对 UnityEngine.Transform 组件实例的依赖
/// </summary>
[Serializable]
public struct TransformData
{
    public Vector3 position;
    public Vector3 rotation;
    public Vector3 scale;

    public static TransformData Default => new TransformData
    {
        position = Vector3.zero,
        rotation = Vector3.zero,
        scale = Vector3.one
    };
}

