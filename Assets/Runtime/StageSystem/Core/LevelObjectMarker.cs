using UnityEngine;

/// <summary>
/// 隐式标记组件，仅用于编辑器下识别关卡物品。
/// 为了避免小游戏打包时报“丢失脚本”的错误，它必须放在 Runtime 目录下，
/// 但它不包含任何逻辑代码，对性能完全无影响。
/// </summary>
public class LevelObjectMarker : MonoBehaviour
{
    // 该组件在被编辑器自动添加时，会被设置为 HideFlags.HideInInspector，对策划完全隐形
}
