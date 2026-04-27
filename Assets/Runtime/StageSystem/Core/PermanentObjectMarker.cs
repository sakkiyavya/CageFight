using UnityEngine;

/// <summary>
/// 隐式标记组件，用于标记场景中的“常驻物品”（如主摄像机、灯光、UI根节点等）。
/// 在编辑器一键加载关卡时，所有未带此标记的根节点对象都将被清理销毁。
/// </summary>
public class PermanentObjectMarker : MonoBehaviour
{
}
