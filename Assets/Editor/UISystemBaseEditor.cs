using UnityEditor;
using UnityEngine;

/// <summary>
/// 为 UISystemBase 及其子类提供自定义 Inspector 面板
/// </summary>
[CustomEditor(typeof(UISystemBase), true)]
[CanEditMultipleObjects]
public class UISystemBaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 绘制默认的 Inspector 内容
        DrawDefaultInspector();

        UISystemBase uiSystem = (UISystemBase)target;
        RectTransform currentRect = uiSystem.GetComponent<RectTransform>();

        GUILayout.Space(10);
        EditorGUILayout.LabelField("数值录制工具", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        // 按钮：记录当前状态到起始数值
        if (GUILayout.Button("记录开始变换", GUILayout.Height(30)))
        {
            RecordStateToValues(uiSystem, currentRect, true);
        }

        // 按钮：记录当前状态到结束数值
        if (GUILayout.Button("记录结束变换", GUILayout.Height(30)))
        {
            RecordStateToValues(uiSystem, currentRect, false);
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 将当前 RectTransform 的状态记录到组件的 Vector3 数值字段中
    /// </summary>
    private void RecordStateToValues(UISystemBase uiSystem, RectTransform source, bool isStart)
    {
        if (source == null) return;

        // 记录撤销
        Undo.RecordObject(uiSystem, isStart ? "Record Start Values" : "Record End Values");

        if (isStart)
        {
            uiSystem.startPos = source.anchoredPosition;
            uiSystem.startSize = source.sizeDelta;
            uiSystem.startRot = source.localEulerAngles;
            uiSystem.startScale = source.localScale;
        }
        else
        {
            uiSystem.endPos = source.anchoredPosition;
            uiSystem.endSize = source.sizeDelta;
            uiSystem.endRot = source.localEulerAngles;
            uiSystem.endScale = source.localScale;
        }

        // 标记脏数据
        EditorUtility.SetDirty(uiSystem);
        
        Debug.Log($"已成功录制当前状态到 {(isStart ? "起始" : "结束")} 数值中。");
    }
}
