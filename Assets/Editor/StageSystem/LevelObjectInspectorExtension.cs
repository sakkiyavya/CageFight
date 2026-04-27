using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class LevelObjectInspectorExtension
{
    static LevelObjectInspectorExtension()
    {
        Editor.finishedDefaultHeaderGUI += OnPostHeaderGUI;
    }

    private static void OnPostHeaderGUI(Editor editor)
    {
        // 确保只处理 GameObject
        if (!(editor.target is GameObject)) return;

        var targets = editor.targets;
        
        bool hasPerm = false;
        bool missingPerm = false;
        
        bool hasLevel = false;
        bool missingLevel = false;
        bool allPrefabs = true;

        // 遍历所有选中的对象，统计状态
        foreach (var t in targets)
        {
            var go = t as GameObject;
            if (go == null) continue;

            if (go.GetComponent<PermanentObjectMarker>() != null) hasPerm = true;
            else missingPerm = true;

            if (PrefabUtility.IsPartOfPrefabInstance(go))
            {
                if (go.GetComponent<LevelObjectMarker>() != null) hasLevel = true;
                else missingLevel = true;
            }
            else
            {
                allPrefabs = false;
            }
        }

        EditorGUILayout.BeginVertical("helpbox");
        
        // 1. 常驻物品开关
        // 当多选对象中有的勾选了，有的没勾选时，显示混合状态 (dash)
        EditorGUI.showMixedValue = hasPerm && missingPerm;
        EditorGUI.BeginChangeCheck();
        bool permToggle = EditorGUILayout.ToggleLeft(" 设为常驻物品 (加载关卡时不销毁)", hasPerm, EditorStyles.boldLabel);
        if (EditorGUI.EndChangeCheck())
        {
            foreach (var t in targets)
            {
                var go = t as GameObject;
                if (go == null) continue;
                
                if (permToggle)
                {
                    if (go.GetComponent<PermanentObjectMarker>() == null)
                    {
                        var marker = go.AddComponent<PermanentObjectMarker>();
                        marker.hideFlags = HideFlags.HideInInspector;
                    }
                }
                else
                {
                    var marker = go.GetComponent<PermanentObjectMarker>();
                    if (marker != null) Undo.DestroyObjectImmediate(marker);
                }
                EditorUtility.SetDirty(go);
            }
        }

        // 2. 关卡物品开关 (强制要求全选的都是预制体)
        if (allPrefabs)
        {
            EditorGUI.showMixedValue = hasLevel && missingLevel;
            EditorGUI.BeginChangeCheck();
            bool levelToggle = EditorGUILayout.ToggleLeft(" 设为关卡物品", hasLevel, EditorStyles.boldLabel);
            
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var t in targets)
                {
                    var go = t as GameObject;
                    if (go == null) continue;
                    
                    if (levelToggle)
                    {
                        if (go.GetComponent<LevelObjectMarker>() == null)
                        {
                            var marker = go.AddComponent<LevelObjectMarker>();
                            marker.hideFlags = HideFlags.HideInInspector;
                        }
                    }
                    else
                    {
                        var marker = go.GetComponent<LevelObjectMarker>();
                        if (marker != null) Undo.DestroyObjectImmediate(marker);
                    }
                    EditorUtility.SetDirty(go);
                }
            }
        }
        else
        {
            EditorGUI.showMixedValue = false;
            GUI.enabled = false;
            EditorGUILayout.ToggleLeft(targets.Length > 1 ? " 设为关卡物品 (存在非预制体)" : " 设为关卡物品 (仅限预制体)", false, EditorStyles.boldLabel);
            GUI.enabled = true;
        }

        // 恢复状态，避免影响其他 Inspector 的绘制
        EditorGUI.showMixedValue = false; 
        EditorGUILayout.EndVertical();
    }
}
