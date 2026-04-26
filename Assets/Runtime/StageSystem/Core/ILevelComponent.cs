using System;
using CageFight.StageSystem.Model;

/// <summary>
/// 所有挂载在 Prefab 上，且需要接收关卡配置参数覆盖的运行时组件，都需要实现此接口。
/// </summary>
public interface ILevelComponent
{
    /// <summary>
    /// 声明当前组件需要接收哪种类型的 ComponentData
    /// </summary>
    Type DataType { get; }

    /// <summary>
    /// 运行时加载时，系统会将数据注入到此处，用于覆盖组件参数
    /// </summary>
    /// <param name="data">从配置中读取的组件数据</param>
    void ApplyData(ComponentData data);
}

