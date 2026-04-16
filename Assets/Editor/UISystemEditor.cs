// using UnityEditor;
// using UnityEngine;

// /// <summary>
// /// 为 UISystemBase 及其子类提供 Gizmos 预览
// /// </summary>
// public class UISystemEditor
// {
//     [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Active)]
//     public static void DrawUISystemGizmos(UISystemBase uiSystem, GizmoType gizmoType)
//     {
//         DrawRadius(uiSystem.GetComponent<RectTransform>(), uiSystem.buttonRadius, Color.yellow);
//     }

//     [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Active)]
//     public static void DrawJoyStickGizmos(JoyStick joyStick, GizmoType gizmoType)
//     {
//         // 摇杆通常使用 background 作为半径参考
//         RectTransform refRect = joyStick.background != null ? joyStick.background : joyStick.GetComponent<RectTransform>();
//         DrawRadius(refRect, joyStick.maxRadius, Color.cyan);
//     }

//     private static void DrawRadius(RectTransform rect, float radius, Color color)
//     {
//         if (rect == null) return;

//         // 设置 Handles 颜色
//         Handles.color = color;

//         // 1. 获取 UI 局部中心点 (0,0) 对应的世界坐标（即 Pivot 的物理位置）
//         // 这与 JoyStick/BuildingButton 中 localPosition.magnitude 判定的圆心完全一致
//         Vector3 worldCenter = rect.TransformPoint(Vector3.zero);

//         // 2. 计算受全局缩放影响的半径。使用 TransformVector 处理非等比缩放或深层嵌套缩放
//         // 我们取局部坐标 (radius, 0, 0) 转换到世界空间后的长度作为圆的半径
//         Vector3 localRadiusPoint = new Vector3(radius, 0, 0);
//         float worldRadius = rect.TransformVector(localRadiusPoint).magnitude;

//         // 3. 在 Scene 视图绘制圆圈
//         // 使用 rect.forward 作为法线，确保圆面始终平行于 UI 平面
//         Handles.DrawWireDisc(worldCenter, rect.forward, worldRadius);

//         // 绘制一个极淡的填充圆供识别
//         Handles.color = new Color(color.r, color.g, color.b, 0.03f);
//         Handles.DrawSolidDisc(worldCenter, rect.forward, worldRadius);
//     }
// }
