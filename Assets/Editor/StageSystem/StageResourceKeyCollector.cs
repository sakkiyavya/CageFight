using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

/// <summary>
/// 收集关卡物品可达序列化对象图中的 ResourceKey。
/// 支持组件子层级、ScriptableObject、数组/List 和自定义可序列化对象。
/// </summary>
internal sealed class StageResourceKeyCollector
{
    private readonly StageConfig _config;
    private readonly HashSet<string> _visitedPrefabKeys = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<int> _visitedUnityObjects = new HashSet<int>();
    private readonly HashSet<object> _visitedManagedObjects =
        new HashSet<object>(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, Type> _resourceTypesByKey =
        new Dictionary<string, Type>(StringComparer.Ordinal);
    private readonly HashSet<string> _reportedTypeConflicts =
        new HashSet<string>(StringComparer.Ordinal);

    private Dictionary<string, string> _prefabPathsByName;
    private Dictionary<string, string> _prefabPathsByAddress;

    public int TotalKeyCount =>
        _config.prefabs.Count +
        _config.audios.Count +
        _config.textures.Count +
        _config.animationClips.Count +
        _config.animatorControllers.Count +
        _config.sprites.Count;

    public StageResourceKeyCollector(StageConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// 清空自动生成的资源依赖列表。
    /// </summary>
    public static void ClearConfig(StageConfig config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        config.prefabs.Clear();
        config.audios.Clear();
        config.textures.Clear();
        config.animationClips.Clear();
        config.animatorControllers.Clear();
        config.sprites.Clear();
    }

    /// <summary>
    /// 收集一个关卡物品及其所有子物体组件可达的资源依赖。
    /// prefabKey 自身会被加入预制体列表，但不会重复扫描其资产版本，
    /// 因为当前场景实例已经包含最终的 Prefab Override 数据。
    /// </summary>
    public void CollectStageObject(GameObject stageObject, string prefabKey)
    {
        if (stageObject == null)
            return;

        if (!string.IsNullOrEmpty(prefabKey))
        {
            _visitedPrefabKeys.Add(prefabKey);
            AddKeyToConfig(prefabKey, typeof(GameObject), false);
        }

        CollectFromGameObject(stageObject);
    }

    private void CollectFromGameObject(GameObject root)
    {
        Component[] components = root.GetComponentsInChildren<Component>(true);
        foreach (Component component in components)
        {
            if (component == null || !_visitedUnityObjects.Add(component.GetInstanceID()))
                continue;

            CollectFields(component);
        }
    }

    private void CollectFields(object owner)
    {
        foreach (FieldInfo field in GetInstanceFields(owner.GetType()))
        {
            if (field.IsStatic)
                continue;

            ResourceKeyAttribute resourceKey = field.GetCustomAttribute<ResourceKeyAttribute>(true);
            if (resourceKey != null)
            {
                CollectAttributedKey(owner, field, resourceKey);
                continue;
            }

            if (!IsUnitySerializedField(field))
                continue;

            object value = field.GetValue(owner);
            CollectReferencedValue(value);
        }
    }

    private void CollectAttributedKey(
        object owner,
        FieldInfo field,
        ResourceKeyAttribute attribute)
    {
        if (field.FieldType == typeof(string))
        {
            string key = field.GetValue(owner) as string;
            if (!string.IsNullOrEmpty(key))
                AddKeyToConfig(key, attribute.ResourceType, true);
            return;
        }

        if (typeof(IEnumerable<string>).IsAssignableFrom(field.FieldType))
        {
            var keys = field.GetValue(owner) as IEnumerable<string>;
            if (keys == null)
                return;

            foreach (string key in keys)
            {
                if (!string.IsNullOrEmpty(key))
                    AddKeyToConfig(key, attribute.ResourceType, true);
            }
            return;
        }

        Debug.LogWarning(
            $"[StageResourceKeyCollector] {owner.GetType().Name}.{field.Name} 标记了 " +
            "[ResourceKey]，但字段不是 string 或 string 集合，已跳过。",
            owner as UnityEngine.Object);
    }

    private void CollectReferencedValue(object value)
    {
        if (value == null || value is string)
            return;

        if (value is UnityEngine.Object unityObject)
        {
            if (unityObject == null || !(unityObject is ScriptableObject scriptableObject))
                return;

            if (!_visitedUnityObjects.Add(scriptableObject.GetInstanceID()))
                return;

            CollectFields(scriptableObject);
            return;
        }

        if (value is IList list)
        {
            if (!_visitedManagedObjects.Add(value))
                return;

            foreach (object item in list)
                CollectReferencedValue(item);
            return;
        }

        Type valueType = value.GetType();
        if (!ShouldTraverseManagedType(valueType))
            return;

        if (!valueType.IsValueType && !_visitedManagedObjects.Add(value))
            return;

        CollectFields(value);
    }

    private void AddKeyToConfig(string key, Type resourceType, bool scanPrefabDependencies)
    {
        ReportTypeConflict(key, resourceType);

        if (resourceType == typeof(GameObject))
        {
            AddUnique(_config.prefabs, key);
            if (scanPrefabDependencies)
                CollectFromPrefabKey(key);
        }
        else if (resourceType == typeof(AudioClip))
        {
            AddUnique(_config.audios, key);
        }
        else if (resourceType == typeof(Texture2D))
        {
            AddUnique(_config.textures, key);
        }
        else if (resourceType == typeof(AnimationClip))
        {
            AddUnique(_config.animationClips, key);
        }
        else if (resourceType == typeof(RuntimeAnimatorController))
        {
            AddUnique(_config.animatorControllers, key);
        }
        else if (resourceType == typeof(Sprite))
        {
            AddUnique(_config.sprites, key);
        }
        else
        {
            Debug.LogWarning(
                $"[StageResourceKeyCollector] 资源 Key '{key}' 使用了未受 StageConfig 支持的资源类型：" +
                $"{resourceType?.FullName ?? "null"}。");
        }
    }

    private void CollectFromPrefabKey(string prefabKey)
    {
        if (!_visitedPrefabKeys.Add(prefabKey))
            return;

        string assetPath = FindAddressablePrefabPath(prefabKey);
        if (assetPath == null)
        {
            Debug.LogWarning(
                $"[StageResourceKeyCollector] 在 Addressables 中未找到名称或 address 为 '{prefabKey}' 的 Prefab，" +
                "已跳过其递归依赖扫描。");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[StageResourceKeyCollector] 无法加载 Prefab：{assetPath}");
            return;
        }

        CollectFromGameObject(prefab);
    }

    private string FindAddressablePrefabPath(string prefabKey)
    {
        EnsurePrefabPathIndex();

        // 与 ResourceManager 保持一致：逻辑名称（Registry Key）优先，
        // 找不到时才把原字符串视为 Addressables address。
        if (_prefabPathsByName.TryGetValue(prefabKey, out string assetPath))
            return assetPath;

        return _prefabPathsByAddress.TryGetValue(prefabKey, out assetPath)
            ? assetPath
            : null;
    }

    private void EnsurePrefabPathIndex()
    {
        if (_prefabPathsByName != null)
            return;

        _prefabPathsByName = new Dictionary<string, string>(StringComparer.Ordinal);
        _prefabPathsByAddress = new Dictionary<string, string>(StringComparer.Ordinal);

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
            return;

        foreach (var group in settings.groups)
        {
            if (group == null)
                continue;

            foreach (var entry in group.entries)
            {
                if (entry == null ||
                    !entry.AssetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.AssetPath);
                if (prefab == null)
                    continue;

                if (!_prefabPathsByName.ContainsKey(prefab.name))
                    _prefabPathsByName.Add(prefab.name, entry.AssetPath);

                if (!string.IsNullOrEmpty(entry.address) &&
                    !_prefabPathsByAddress.ContainsKey(entry.address))
                {
                    _prefabPathsByAddress.Add(entry.address, entry.AssetPath);
                }
            }
        }
    }

    private void ReportTypeConflict(string key, Type resourceType)
    {
        if (!_resourceTypesByKey.TryGetValue(key, out Type existingType))
        {
            _resourceTypesByKey.Add(key, resourceType);
            return;
        }

        if (existingType == resourceType || !_reportedTypeConflicts.Add(key))
            return;

        Debug.LogError(
            $"[StageResourceKeyCollector] 资源 Key '{key}' 同时被声明为 " +
            $"{existingType?.Name ?? "null"} 和 {resourceType?.Name ?? "null"}。" +
            "当前 ResourceManager 会按字符串合并资源类型，请将不同资源类型的 Key 改为全局唯一名称。");
    }

    private static IEnumerable<FieldInfo> GetInstanceFields(Type type)
    {
        for (Type current = type;
             current != null && !IsUnityBaseType(current);
             current = current.BaseType)
        {
            FieldInfo[] fields = current.GetFields(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly);

            foreach (FieldInfo field in fields)
                yield return field;
        }
    }

    private static bool IsUnityBaseType(Type type)
    {
        return type == typeof(MonoBehaviour) ||
               type == typeof(ScriptableObject) ||
               type == typeof(Component) ||
               type == typeof(UnityEngine.Object) ||
               type == typeof(object);
    }

    private static bool IsUnitySerializedField(FieldInfo field)
    {
        if (field.IsStatic || field.IsLiteral || field.IsInitOnly || field.IsNotSerialized)
            return false;

        return field.IsPublic ||
               field.GetCustomAttribute<SerializeField>() != null ||
               field.GetCustomAttribute<SerializeReference>() != null;
    }

    private static bool ShouldTraverseManagedType(Type type)
    {
        if (!type.IsSerializable ||
            type.IsPrimitive ||
            type.IsEnum ||
            type == typeof(string) ||
            type == typeof(decimal) ||
            typeof(Delegate).IsAssignableFrom(type))
        {
            return false;
        }

        string typeNamespace = type.Namespace;
        if (string.IsNullOrEmpty(typeNamespace))
            return true;

        return typeNamespace != "System" &&
               !typeNamespace.StartsWith("System.", StringComparison.Ordinal) &&
               typeNamespace != "UnityEngine" &&
               !typeNamespace.StartsWith("UnityEngine.", StringComparison.Ordinal);
    }

    private static void AddUnique(List<string> list, string key)
    {
        if (!list.Contains(key))
            list.Add(key);
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

        public new bool Equals(object x, object y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(object obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
