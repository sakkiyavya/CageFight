using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

[CustomPropertyDrawer(typeof(ResourceKeyAttribute))]
public class ResourceKeyAttributeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            EditorGUI.EndProperty();
            return;
        }

        ResourceKeyAttribute attr = attribute as ResourceKeyAttribute;
        Type resourceType = attr != null ? attr.ResourceType : typeof(UnityEngine.Object);

        if (!typeof(UnityEngine.Object).IsAssignableFrom(resourceType))
        {
            property.stringValue = EditorGUI.TextField(position, label, property.stringValue);
            EditorGUI.EndProperty();
            return;
        }

        UnityEngine.Object currentObject = FindObjectByName(property.stringValue, resourceType);

        EditorGUI.BeginChangeCheck();
        UnityEngine.Object selectedObject = EditorGUI.ObjectField(position, label, currentObject, resourceType, false);
        if (EditorGUI.EndChangeCheck())
        {
            if (selectedObject == null)
            {
                property.stringValue = string.Empty;
            }
            else if (IsValidResourceObject(selectedObject) || IsRegisteredKey(selectedObject))
            {
                property.stringValue = selectedObject.name;
            }
            else
            {
                Debug.LogWarning(
                    $"[ResourceKey] 拖入被拒绝：{selectedObject.name}（{selectedObject.GetType().Name}）。\n" +
                    "必须从 Project 面板拖入资产；贴图/图集需要展开小箭头后拖入具体的 Sprite 子项（资源键 = 子项名）。" +
                    "该资产还必须已打包进 Addressables（先执行 关卡构建/资源构建/一键生成全部资源注册表）。",
                    selectedObject);
            }
        }

        EditorGUI.EndProperty();
    }

    /// <summary>
    /// 兜底校验：对象名已经是对应注册表里的资源键（注册表生成器已保证相关资产打包进 Addressables）。
    /// 即使 Addressables 设置查询因多设置/延迟导入等原因失效，也接受拖入，避免"已注册却拖不进"。
    /// </summary>
    private bool IsRegisteredKey(UnityEngine.Object obj)
    {
        if (obj == null || string.IsNullOrEmpty(obj.name))
            return false;

        ResourceKeyAttribute attr = attribute as ResourceKeyAttribute;
        Type resourceType = attr != null ? attr.ResourceType : null;
        string registryTypeName = null;
        if (resourceType == typeof(Sprite)) registryTypeName = "SpriteRegistry";
        else if (resourceType == typeof(AudioClip)) registryTypeName = "AudioRegistry";
        else if (resourceType == typeof(GameObject)) registryTypeName = "PrefabRegistry";
        else if (resourceType == typeof(Texture2D)) registryTypeName = "TextureRegistry";
        else if (resourceType == typeof(AnimationClip)) registryTypeName = "AnimationClipRegistry";
        else if (resourceType == typeof(RuntimeAnimatorController)) registryTypeName = "AnimatorControllerRegistry";

        if (string.IsNullOrEmpty(registryTypeName))
            return false;

        string[] guids = AssetDatabase.FindAssets(registryTypeName + " t:" + registryTypeName);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path))
                continue;

            try
            {
                string text = File.ReadAllText(path);
                if (text.Contains("- key: " + obj.name + "\n") ||
                    text.Contains("- key: " + obj.name + "\r"))
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // 注册表读取失败：忽略，继续按 Addressables 校验结果处理。
            }
        }

        return false;
    }

    private static UnityEngine.Object FindObjectByName(string objectName, Type resourceType)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        // 优先按注册表键解析：图集子精灵等子资产按名无法被 AssetDatabase.FindAssets 命中，
        // 旧逻辑查不到时字段会显示为空（看似“值被顶掉”，实际键仍保存在资产里）。
        UnityEngine.Object registered = FindRegisteredAsset(objectName, resourceType);
        if (registered != null)
        {
            return registered;
        }

        string[] guids = AssetDatabase.FindAssets(objectName);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(path, resourceType);
            if (asset != null && asset.name == objectName)
            {
                return asset;
            }
        }

        foreach (UnityEngine.Object obj in Resources.FindObjectsOfTypeAll(resourceType))
        {
            if (obj.name == objectName && EditorUtility.IsPersistent(obj))
            {
                return obj;
            }
        }

        return null;
    }

    /// <summary>按资源类型从对应注册表按键解析对象（注册表编辑器强引用字段）。</summary>
    private static UnityEngine.Object FindRegisteredAsset(string key, Type resourceType)
    {
        if (resourceType == typeof(Sprite))
            return ResolveFromRegistry<SpriteRegistry, Sprite>(key, registry => registry.GetAsset(key));
        if (resourceType == typeof(AudioClip))
            return ResolveFromRegistry<AudioRegistry, AudioClip>(key, registry => registry.GetAsset(key));
        if (resourceType == typeof(GameObject))
            return ResolveFromRegistry<PrefabRegistry, GameObject>(key, registry => registry.GetPrefab(key));
        if (resourceType == typeof(Texture2D))
            return ResolveFromRegistry<TextureRegistry, Texture2D>(key, registry => registry.GetAsset(key));
        if (resourceType == typeof(AnimationClip))
            return ResolveFromRegistry<AnimationClipRegistry, AnimationClip>(key, registry => registry.GetAsset(key));
        if (resourceType == typeof(RuntimeAnimatorController))
            return ResolveFromRegistry<AnimatorControllerRegistry, RuntimeAnimatorController>(key, registry => registry.GetAsset(key));

        return null;
    }

    private static TAsset ResolveFromRegistry<TRegistry, TAsset>(string key, Func<TRegistry, TAsset> resolve)
        where TRegistry : ScriptableObject
        where TAsset : UnityEngine.Object
    {
        string typeName = typeof(TRegistry).Name;
        string[] guids = AssetDatabase.FindAssets(typeName + " t:" + typeName);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path))
                continue;

            TRegistry registry = AssetDatabase.LoadAssetAtPath<TRegistry>(path);
            if (registry == null)
                continue;

            try
            {
                return resolve(registry);
            }
            catch (Exception)
            {
                return null;
            }
        }

        return null;
    }

    private static bool IsValidResourceObject(UnityEngine.Object obj)
    {
        if (obj is GameObject gameObject && !PrefabUtility.IsPartOfPrefabAsset(gameObject))
        {
            EditorUtility.DisplayDialog("资源选择无效", $"对象 \"{obj.name}\" 不是 Prefab 资源，不能作为资源 Key。", "确定");
            return false;
        }

        string path = AssetDatabase.GetAssetPath(obj);
        string guid = AssetDatabase.AssetPathToGUID(path);
        if (string.IsNullOrEmpty(guid))
        {
            EditorUtility.DisplayDialog("资源选择无效", $"对象 \"{obj.name}\" 不是项目资源，不能作为资源 Key。", "确定");
            return false;
        }

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            EditorUtility.DisplayDialog("资源选择无效", "当前项目还没有 Addressables Settings，请先初始化 Addressables。", "确定");
            return false;
        }

        if (settings.FindAssetEntry(guid) == null)
        {
            EditorUtility.DisplayDialog("资源选择无效", $"对象 \"{obj.name}\" 没有打包进 Addressables，不能作为资源 Key。", "确定");
            return false;
        }

        return true;
    }
}
