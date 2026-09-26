using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class StageObjectInspectorExtension
{
    static StageObjectInspectorExtension()
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
        
        bool hasStage = false;
        bool missingStage = false;
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
                if (go.GetComponent<StageObjectMarker>() != null) hasStage = true;
                else missingStage = true;
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
            EditorGUI.showMixedValue = hasStage && missingStage;
            EditorGUI.BeginChangeCheck();
            bool stageToggle = EditorGUILayout.ToggleLeft(" 设为关卡物品", hasStage, EditorStyles.boldLabel);
            
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var t in targets)
                {
                    var go = t as GameObject;
                    if (go == null) continue;
                    
                    if (stageToggle)
                    {
                        if (go.GetComponent<StageObjectMarker>() == null)
                        {
                            var marker = go.AddComponent<StageObjectMarker>();
                            marker.hideFlags = HideFlags.HideInInspector;
                        }
                    }
                    else
                    {
                        var marker = go.GetComponent<StageObjectMarker>();
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

        DrawFriendlyMainBaseToggle(targets);

        // 恢复状态，避免影响其他 Inspector 的绘制
        EditorGUI.showMixedValue = false; 
        EditorGUILayout.EndVertical();
    }

    private static void DrawFriendlyMainBaseToggle(Object[] targets)
    {
        bool hasMarker = false;
        bool missingMarker = false;
        bool allScenePrefabs = true;
        foreach (Object target in targets)
        {
            var go = target as GameObject;
            if (go == null) continue;
            hasMarker |= go.GetComponent<FriendlyMainBaseMarker>() != null;
            missingMarker |= go.GetComponent<FriendlyMainBaseMarker>() == null;
            allScenePrefabs &= go.scene.IsValid() && !EditorUtility.IsPersistent(go)
                && PrefabStageUtility.GetPrefabStage(go) == null
                && PrefabUtility.IsOutermostPrefabInstanceRoot(go);
        }

        EditorGUI.showMixedValue = hasMarker && missingMarker;
        // 已有标记即使因解包而失效，也允许取消。
        using (new EditorGUI.DisabledScope(!allScenePrefabs && !hasMarker))
        {
            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.ToggleLeft(
                " 设为己方大本营 (仅限场景最外层预制体根对象)", hasMarker, EditorStyles.boldLabel);
            if (EditorGUI.EndChangeCheck() && (!enabled || allScenePrefabs))
            {
                foreach (Object target in targets)
                {
                    var go = target as GameObject;
                    if (go == null) continue;
                    var marker = go.GetComponent<FriendlyMainBaseMarker>();
                    if (enabled && marker == null)
                    {
                        marker = Undo.AddComponent<FriendlyMainBaseMarker>(go);
                        Undo.RecordObject(marker, "Mark Friendly Main Base");
                        marker.hideFlags = HideFlags.HideInInspector;
                        EditorUtility.SetDirty(marker);
                    }
                    else if (!enabled && marker != null)
                    {
                        Undo.DestroyObjectImmediate(marker);
                    }
                    if (go.scene.IsValid()) EditorSceneManager.MarkSceneDirty(go.scene);
                }
            }
        }
    }
}
