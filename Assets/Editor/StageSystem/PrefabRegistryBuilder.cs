using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;
using System.IO;

public class PrefabRegistryBuilder
{
    // 将注册表存放在统一的 Resource 目录下，这样它就能被打包，或者被 Addressables 轻松引用
    private const string REGISTRY_PATH = "Assets/RemoteResource/PrefabRegistry.asset";
    
    // 关卡重资源分配的专属更新组名称
    private const string ADDRESSABLE_GROUP_NAME = "LevelObjects";

    [MenuItem("关卡构建/一键生成预制体注册表 (Addressable版)")]
    public static void BuildRegistry()
    {
        // 1. 寻找或创建 Registry SO
        PrefabRegistry registry = AssetDatabase.LoadAssetAtPath<PrefabRegistry>(REGISTRY_PATH);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<PrefabRegistry>();
            
            string dir = Path.GetDirectoryName(REGISTRY_PATH);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            
            AssetDatabase.CreateAsset(registry, REGISTRY_PATH);
        }

        registry.mappings.Clear();

        // 2. 收集工程里所有关卡配置，找出里面用到的所有 prefabKey
        HashSet<string> usedKeys = new HashSet<string>();
        string[] configGuids = AssetDatabase.FindAssets("t:LevelConfig");
        foreach (var guid in configGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            LevelConfig config = AssetDatabase.LoadAssetAtPath<LevelConfig>(path);
            if (config != null && config.objects != null)
            {
                foreach (var obj in config.objects)
                {
                    if (!string.IsNullOrEmpty(obj.prefabKey))
                    {
                        usedKeys.Add(obj.prefabKey);
                    }
                }
            }
        }

        if (usedKeys.Count == 0)
        {
            Debug.LogWarning("[PrefabRegistryBuilder] 没有任何关卡配置文件，或者所有关卡配置中都没有使用预制体！");
            EditorUtility.DisplayDialog("绑定提示", "工程中没有发现任何被使用的 prefabKey 关卡物品，注册表已清空。", "确定");
            return;
        }

        // 3. 准备 Addressables 设置，获取或创建专有的 LevelObjects 资源组
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[PrefabRegistryBuilder] 未在工程中找到 Addressables Settings！请先在 Windows -> Asset Management -> Addressables -> Groups 面板点击 'Create Addressables Settings' 进行初始化！");
            EditorUtility.DisplayDialog("生成失败", "未在工程中检测到 Addressables 配置，请先初始化 Addressables 后重试。", "确定");
            return;
        }

        AddressableAssetGroup targetGroup = settings.FindGroup(ADDRESSABLE_GROUP_NAME);
        if (targetGroup == null)
        {
            // 自动创建对应的远程重资源资源组
            targetGroup = settings.CreateGroup(ADDRESSABLE_GROUP_NAME, false, false, true, null);
            Debug.Log($"[PrefabRegistryBuilder] 自动创建了全新的 Addressable 组：{ADDRESSABLE_GROUP_NAME}");
        }

        // 4. 遍历工程寻找对应的 Prefab 进行自动标记与映射绑定
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int addedCount = 0;
        HashSet<string> keysToSearch = new HashSet<string>(usedKeys);

        foreach (var guid in prefabGuids)
        {
            if (keysToSearch.Count == 0) break;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            
            if (prefab != null)
            {
                // 我们系统目前的契约：prefabKey 就是 prefab 的 name
                if (keysToSearch.Contains(prefab.name))
                {
                    // 核心自动化标记：如果该预制体没有被标记为 Addressable，一键自动标记并移动到 LevelObjects 更新组下
                    var entry = settings.CreateOrMoveEntry(guid, targetGroup);
                    if (entry != null)
                    {
                        // 规范命名其 Addressable 地址为预制体名称，完美兼容直寻址模式
                        entry.SetAddress(prefab.name);
                    }

                    // 创建对应的 AssetReferenceGameObject 安全弱引用
                    var reference = new AssetReferenceGameObject(guid);

                    // 实例化映射并加入 mappings 列表
                    registry.mappings.Add(new PrefabMapping 
                    { 
                        key = prefab.name, 
                        prefabReference = reference,
#if UNITY_EDITOR
                        prefab = prefab // 仅在编辑器宏包裹下赋值物理强引用，方便编辑器同步预览
#endif
                    });

                    keysToSearch.Remove(prefab.name); // 绑定成功，从待寻找列表中移除
                    addedCount++;
                }
            }
        }

        // 5. 错误报告：是否有配置里写了，但是工程里已经被删掉的 Prefab
        if (keysToSearch.Count > 0)
        {
            string missing = string.Join(", ", keysToSearch);
            Debug.LogError($"<color=red>[预警]</color> 以下 prefabKey 在工程中未找到对应物理预制体资源: {missing}。请检查是否误删，或者名称拼写不一致。");
        }

        // 6. 保存资产并刷新
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 自动将 Settings 的改动脏化保存，防止在关闭 Unity 时 Addressables 修改丢失
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true);

        EditorUtility.DisplayDialog("绑定成功", 
            $"预制体 Addressable 注册表已生成并自动部署！\n\n" +
            $"共成功标记并关联了 {addedCount} 个关卡预制体到 '{ADDRESSABLE_GROUP_NAME}' 资源组下。\n" +
            $"存放路径: {REGISTRY_PATH}", "确定");
    }
}
