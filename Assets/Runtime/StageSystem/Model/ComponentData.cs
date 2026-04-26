using System;


/// <summary>
/// 组件数据基类，所有具体的组件数据（如 SpawnerData, PatrolData 等）需继承此类。
/// 在 LevelObjectData 中配合 [SerializeReference] 标签使用，以支持多态序列化。
/// </summary>
[Serializable]
public abstract class ComponentData
{
}

