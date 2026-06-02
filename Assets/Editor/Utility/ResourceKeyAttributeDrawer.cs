using System;
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
            else if (IsValidResourceObject(selectedObject))
            {
                property.stringValue = selectedObject.name;
            }
        }

        EditorGUI.EndProperty();
    }

    private static UnityEngine.Object FindObjectByName(string objectName, Type resourceType)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return null;
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
