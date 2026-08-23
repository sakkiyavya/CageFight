using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[CustomEditor(typeof(StageConfig))]
public class StageConfigEditor : Editor
{
    private bool _prefabsFoldout = false;
    private bool _audiosFoldout = false;
    private bool _texturesFoldout = false;
    private bool _animationClipsFoldout = false;
    private bool _animatorControllersFoldout = false;
    private bool _spritesFoldout = false;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        StageConfig config = (StageConfig)target;

        // --- 绘制标准字段（stageId、settings、objects），但跳过分类列表 ---
        DrawPropertiesExcluding(serializedObject, "icon", "prefabs", "audios", "textures", "animationClips", "animatorControllers", "sprites");
        DrawStageIconField(serializedObject.FindProperty("icon"));

        EditorGUILayout.Space(12);

        // 绘制六个分类的资源 Key 列表
        DrawListSection("预制体资源 Key (GameObject)", ref _prefabsFoldout, config.prefabs);
        DrawListSection("音频资源 Key (AudioClip)", ref _audiosFoldout, config.audios);
        DrawListSection("纹理资源 Key (Texture2D)", ref _texturesFoldout, config.textures);
        DrawListSection("动画片段资源 Key (AnimationClip)", ref _animationClipsFoldout, config.animationClips);
        DrawListSection("动画控制器资源 Key (AnimatorController)", ref _animatorControllersFoldout, config.animatorControllers);
        DrawListSection("Sprite 资源 Key (Sprite)", ref _spritesFoldout, config.sprites);

        EditorGUILayout.Space(12);

        // 统一扫描与清空按钮
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.color = new Color(0.6f, 0.9f, 1f);
        if (GUILayout.Button("🔍 从当前场景扫描所有资源 Key", GUILayout.Height(32)))
        {
            ScanResourceKeysFromScene(config);
        }
        GUI.color = Color.white;

        GUI.color = new Color(1f, 0.75f, 0.75f);
        if (GUILayout.Button("清空所有资源 Key 列表", GUILayout.Height(24)))
        {
            if (EditorUtility.DisplayDialog("确认清空", "确定要清空所有六个分类的资源 Key 清单吗？", "确定", "取消"))
            {
                Undo.RecordObject(config, "Clear All Resource Keys");
                StageResourceKeyCollector.ClearConfig(config);
                EditorUtility.SetDirty(config);
            }
        }
        GUI.color = Color.white;
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(20);
        DrawStageLoadButton(config);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawStageIconField(SerializedProperty iconProperty)
    {
        EditorGUILayout.BeginHorizontal();

        Rect previewRect = GUILayoutUtility.GetRect(64f, 64f, GUILayout.Width(64f), GUILayout.Height(64f));
        DrawSpritePreview(previewRect, iconProperty.objectReferenceValue as Sprite);

        EditorGUILayout.BeginVertical();
        EditorGUILayout.PropertyField(iconProperty, new GUIContent("Stage Icon"));
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private static void DrawSpritePreview(Rect rect, Sprite sprite)
    {
        EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f, 1f));
        if (sprite == null || sprite.texture == null) return;

        Texture texture = sprite.texture;
        Rect textureRect = sprite.textureRect;
        Rect uv = new Rect(
            textureRect.x / texture.width,
            textureRect.y / texture.height,
            textureRect.width / texture.width,
            textureRect.height / texture.height);

        GUI.color = Color.white;
        GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
        GUI.color = Color.white;
    }

    private void DrawListSection(string label, ref bool foldout, List<string> list)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        string title = $"{label}  [{list?.Count ?? 0} 个]";
        foldout = EditorGUILayout.Foldout(foldout, title, true, EditorStyles.foldoutHeader);

        if (foldout)
        {
            EditorGUILayout.Space(4);
            if (list == null || list.Count == 0)
            {
                EditorGUILayout.HelpBox("清单为空。", MessageType.Info);
            }
            else
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < list.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"{i + 1}.", GUILayout.Width(28));
                    EditorGUILayout.SelectableLabel(list[i], EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space(4);
        }
        EditorGUILayout.EndVertical();
    }

    // -----------------------------------------------------------------------
    // 场景扫描逻辑（与 StageExporter 共享相同策略）
    // -----------------------------------------------------------------------

    private void ScanResourceKeysFromScene(StageConfig config)
    {
        var markers = Object.FindObjectsOfType<StageObjectMarker>(true);
        if (markers.Length == 0)
        {
            EditorUtility.DisplayDialog("扫描提示", "当前场景中没有找到任何 StageObjectMarker，请先布置关卡物品。", "确定");
            return;
        }

        Undo.RecordObject(config, "Scan Resource Keys");

        StageResourceKeyCollector.ClearConfig(config);
        var collector = new StageResourceKeyCollector(config);

        foreach (var marker in markers)
        {
            GameObject go = marker.gameObject;
            GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
            string key = prefabAsset != null ? prefabAsset.name : go.name;

            collector.CollectStageObject(go, key);
        }

        EditorUtility.SetDirty(config);

        EditorUtility.DisplayDialog("扫描完成",
            $"共扫描到 {collector.TotalKeyCount} 个资源 Key。\n" +
            $"Prefab: {config.prefabs.Count}\n" +
            $"Audio: {config.audios.Count}\n" +
            $"Texture: {config.textures.Count}\n" +
            $"AnimationClip: {config.animationClips.Count}\n" +
            $"AnimatorController: {config.animatorControllers.Count}\n" +
            $"Sprite: {config.sprites.Count}\n\n" +
            $"请在 Inspector 中展开各列表审查清单内容是否正确。", "确定");
    }

    // -----------------------------------------------------------------------
    // 关卡加载按钮（保留原有功能）
    // -----------------------------------------------------------------------

    private void DrawStageLoadButton(StageConfig config)
    {
        if (GUILayout.Button("加载关卡", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("加载关卡预览",
                "这将会清除当前场景中所有未标记为\"常驻物品\"的对象，确定要继续吗？\n(如果有未保存的内容请先保存)", "确定", "取消"))
            {
                LoadStageToScene(config);
            }
        }
    }

    private void LoadStageToScene(StageConfig config)
    {
        var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
        int destroyedCount = 0;

        foreach (var rootObj in rootObjects)
        {
            if (rootObj.GetComponent<PermanentObjectMarker>() == null)
            {
                Undo.DestroyObjectImmediate(rootObj);
                destroyedCount++;
            }
        }

        int loadedCount = 0;
        foreach (var objData in config.objects)
        {
            string[] guids = AssetDatabase.FindAssets($"{objData.prefabKey} t:Prefab");
            GameObject prefabAsset = null;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset != null && asset.name == objData.prefabKey)
                {
                    prefabAsset = asset;
                    break;
                }
            }

            if (prefabAsset == null)
            {
                Debug.LogError($"[加载失败] 无法在工程中找到名为 '{objData.prefabKey}' 的预制体！");
                continue;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
            Undo.RegisterCreatedObjectUndo(instance, "Load Stage Object");

            instance.transform.position = objData.transform.position;
            instance.transform.eulerAngles = objData.transform.rotation;
            instance.transform.localScale = objData.transform.scale;

            var stageComponents = instance.GetComponentsInChildren<IStageComponent>(true);
            foreach (var savedComponentData in objData.components)
            {
                foreach (var comp in stageComponents)
                {
                    if (comp.DataType == savedComponentData.GetType())
                    {
                        comp.ApplyData(savedComponentData);
                        break;
                    }
                }
            }

            if (instance.GetComponent<StageObjectMarker>() == null)
            {
                var marker = instance.AddComponent<StageObjectMarker>();
                marker.hideFlags = HideFlags.HideInInspector;
            }

            loadedCount++;
        }

        Debug.Log($"<color=green><b>关卡加载完毕！</b></color> 清理了 {destroyedCount} 个对象，成功还原了 {loadedCount} 个物品。");
    }
}
