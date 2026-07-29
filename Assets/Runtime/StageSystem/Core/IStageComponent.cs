using System;

/// <summary>
/// 所有挂载在 Prefab 上，且需要接收关卡配置参数覆盖的运行时组件，都需要实现此接口。
/// </summary>
public interface IStageComponent
{
    /// <summary>
    /// 获取当前组件能够接收和导出的具体 <see cref="ComponentData"/> 类型。
    /// </summary>
    Type DataType { get; }

    /// <summary>
    /// 在关卡实例化时应用保存的组件数据，覆盖预制体上的默认参数。
    /// </summary>
    /// <param name="data">从关卡配置中读取、且类型应与 <see cref="DataType"/> 一致的组件数据。</param>
    void ApplyData(ComponentData data);

    /// <summary>
    /// 将当前组件中需要持久化的参数导出为关卡配置数据。
    /// </summary>
    /// <returns>与 <see cref="DataType"/> 一致的组件数据实例。</returns>
    ComponentData ExtractData();
}

