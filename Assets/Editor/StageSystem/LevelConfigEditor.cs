using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[CustomEditor(typeof(LevelConfig))]
public class LevelConfigEditor : Editor
{
    private bool _prefabsFoldout = false;
    private bool _audiosFoldout = false;
    private bool _texturesFoldout = false;
    private bool _animationClipsFoldout = false;
    private bool _animatorControllersFoldout = false;

    public override void OnInspectorGUI()
    {
        LevelConfig config = (LevelConfig)target;

        // --- 绘制标准字段（levelId、settings、objects），但跳过分类列表 ---
        DrawPropertiesExcluding(serializedObject, "prefabs", "audios", "textures", "animationClips", "animatorControllers");

        EditorGUILayout.Space(12);

        // 绘制五个分类的资源 Key 列表
        DrawListSection("预制体资源 Key (GameObject)", ref _prefabsFoldout, config.prefabs);
        DrawListSection("音频资源 Key (AudioClip)", ref _audiosFoldout, config.audios);
        DrawListSection("纹理资源 Key (Texture2D)", ref _texturesFoldout, config.textures);
        DrawListSection("动画片段资源 Key (AnimationClip)", ref _animationClipsFoldout, config.animationClips);
        DrawListSection("动画控制器资源 Key (AnimatorController)", ref _animatorControllersFoldout, config.animatorControllers);

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
            if (EditorUtility.DisplayDialog("确认清空", "确定要清空所有五个分类的资源 Key 清单吗？", "确定", "取消"))
            {
                Undo.RecordObject(config, "Clear All Resource Keys");
                config.prefabs.Clear();
                config.audios.Clear();
                config.textures.Clear();
                config.animationClips.Clear();
                config.animatorControllers.Clear();
                EditorUtility.SetDirty(config);
            }
        }
        GUI.color = Color.white;
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(20);
        DrawLevelLoadButton(config);
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
    // 场景扫描逻辑（与 LevelExporter 共享相同策略）
    // -----------------------------------------------------------------------

    private void ScanResourceKeysFromScene(LevelConfig config)
    {
        var markers = Object.FindObjectsOfType<LevelObjectMarker>(true);
        if (markers.Length == 0)
        {
            EditorUtility.DisplayDialog("扫描提示", "当前场景中没有找到任何 LevelObjectMarker，请先布置关卡物品。", "确定");
            return;
        }

        HashSet<string> visitedKeys = new HashSet<string>();
        HashSet<string> visitedPrefabs = new HashSet<string>();

        Undo.RecordObject(config, "Scan Resource Keys");
        
        config.prefabs.Clear();
        config.audios.Clear();
        config.textures.Clear();
        config.animationClips.Clear();
        config.animatorControllers.Clear();

        foreach (var marker in markers)
        {
            GameObject go = marker.gameObject;
            GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
            string key = prefabAsset != null ? prefabAsset.name : go.name;

            if (visitedKeys.Add(key))
            {
                AddKeyToConfig(key, typeof(GameObject), config);
            }
            visitedPrefabs.Add(key);

            CollectResourceKeys(go, visitedKeys, visitedPrefabs, config);
        }

        EditorUtility.SetDirty(config);

        int totalKeys = config.prefabs.Count + config.audios.Count + config.textures.Count + config.animationClips.Count + config.animatorControllers.Count;
        EditorUtility.DisplayDialog("扫描完成",
            $"共扫描到 {totalKeys} 个资源 Key。\n" +
            $"Prefab: {config.prefabs.Count}\n" +
            $"Audio: {config.audios.Count}\n" +
            $"Texture: {config.textures.Count}\n" +
            $"AnimationClip: {config.animationClips.Count}\n" +
            $"AnimatorController: {config.animatorControllers.Count}\n\n" +
            $"请在 Inspector 中展开各列表审查清单内容是否正确。", "确定");
    }

    private static void AddKeyToConfig(string key, Type type, LevelConfig config)
    {
        if (type == typeof(GameObject))
        {
            if (!config.prefabs.Contains(key)) config.prefabs.Add(key);
        }
        else if (type == typeof(AudioClip))
        {
            if (!config.audios.Contains(key)) config.audios.Add(key);
        }
        else if (type == typeof(Texture2D))
        {
            if (!config.textures.Contains(key)) config.textures.Add(key);
        }
        else if (type == typeof(AnimationClip))
        {
            if (!config.animationClips.Contains(key)) config.animationClips.Add(key);
        }
        else if (type == typeof(RuntimeAnimatorController))
        {
            if (!config.animatorControllers.Contains(key)) config.animatorControllers.Add(key);
        }
    }

    private static void CollectResourceKeys(
        GameObject go,
        HashSet<string> visitedKeys,
        HashSet<string> visitedPrefabs,
        LevelConfig config)
    {
        var components = go.GetComponents<Component>();
        foreach (var comp in components)
        {
            if (comp == null) continue;

            var type = comp.GetType();
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var field in fields)
            {
                if (field.FieldType != typeof(string)) continue;

                var attr = field.GetCustomAttribute<ResourceKeyAttribute>();
                if (attr == null) continue;

                string resKey = field.GetValue(comp) as string;
                if (string.IsNullOrEmpty(resKey)) continue;

                if (visitedKeys.Add(resKey))
                {
                    AddKeyToConfig(resKey, attr.ResourceType, config);
                }

                if (attr.ResourceType == typeof(GameObject))
                {
                    RecursiveCollectFromPrefabAsset(resKey, visitedKeys, visitedPrefabs, config);
                }
            }
        }
    }

    private static void RecursiveCollectFromPrefabAsset(
        string prefabKey,
        HashSet<string> visitedKeys,
        HashSet<string> visitedPrefabs,
        LevelConfig config)
    {
        if (!visitedPrefabs.Add(prefabKey)) return;

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) return;

        string assetPath = null;
        foreach (var group in settings.groups)
        {
            if (group == null) continue;
            foreach (var entry in group.entries)
            {
                if (entry == null) continue;
                if (entry.address == prefabKey && entry.AssetPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    assetPath = entry.AssetPath;
                    break;
                }
            }
            if (assetPath != null) break;
        }

        if (assetPath == null)
        {
            Debug.LogWarning($"[ResourceKey扫描] 在 Addressables 中未找到 address 为 '{prefabKey}' 的 Prefab，跳过递归。");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null) return;

        var components = prefab.GetComponents<Component>();
        foreach (var comp in components)
        {
            if (comp == null) continue;

            var type = comp.GetType();
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var field in fields)
            {
                if (field.FieldType != typeof(string)) continue;

                var attr = field.GetCustomAttribute<ResourceKeyAttribute>();
                if (attr == null) continue;

                string resKey = field.GetValue(comp) as string;
                if (string.IsNullOrEmpty(resKey)) continue;

                if (visitedKeys.Add(resKey))
                {
                    AddKeyToConfig(resKey, attr.ResourceType, config);
                }

                if (attr.ResourceType == typeof(GameObject))
                    RecursiveCollectFromPrefabAsset(resKey, visitedKeys, visitedPrefabs, config);
            }
        }
    }

    // -----------------------------------------------------------------------
    // 关卡加载按钮（保留原有功能）
    // -----------------------------------------------------------------------

    private void DrawLevelLoadButton(LevelConfig config)
    {
        if (GUILayout.Button("加载关卡", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("加载关卡预览",
                "这将会清除当前场景中所有未标记为\"常驻物品\"的对象，确定要继续吗？\n(如果有未保存的内容请先保存)", "确定", "取消"))
            {
                LoadLevelToScene(config);
            }
        }
    }

    private void LoadLevelToScene(LevelConfig config)
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
            Undo.RegisterCreatedObjectUndo(instance, "Load Level Object");

            instance.transform.position = objData.transform.position;
            instance.transform.eulerAngles = objData.transform.rotation;
            instance.transform.localScale = objData.transform.scale;

            var levelComponents = instance.GetComponentsInChildren<ILevelComponent>(true);
            foreach (var savedComponentData in objData.components)
            {
                foreach (var comp in levelComponents)
                {
                    if (comp.DataType == savedComponentData.GetType())
                    {
                        comp.ApplyData(savedComponentData);
                        break;
                    }
                }
            }

            if (instance.GetComponent<LevelObjectMarker>() == null)
            {
                var marker = instance.AddComponent<LevelObjectMarker>();
                marker.hideFlags = HideFlags.HideInInspector;
            }

            loadedCount++;
        }

        Debug.Log($"<color=green><b>关卡加载完毕！</b></color> 清理了 {destroyedCount} 个对象，成功还原了 {loadedCount} 个物品。");
    }
}
