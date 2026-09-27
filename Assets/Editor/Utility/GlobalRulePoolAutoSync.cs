using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

/// <summary>
/// 全局规则池自动同步：每次资源导入（以及编辑器启动）后扫描项目内全部
/// GlobalRuleDefinition 资产，按名称排序重建 GlobalRulePool.asset 的内容。
/// 策划创建规则资产后无需手动拖入规则池——大师难度的随机抽取自动覆盖全部规则；
/// 内容无变化时不写入（避免后处理循环与无谓保存）。
/// </summary>
public sealed class GlobalRulePoolAutoSync : AssetPostprocessor
{
    private const string PoolPath = "Assets/RemoteResource/Stages/GlobalRulePool.asset";
    private const string PoolAddress = "GlobalRulePool";

    private static bool _syncing;

    [InitializeOnLoadMethod]
    private static void HookStartup()
    {
        EditorApplication.delayCall += SyncPool;
    }

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedFromAssetPaths,
        string[] movedToAssetPaths)
    {
        // 延后到本轮导入结束后执行，避免在导入过程中改写资产。
        EditorApplication.delayCall += SyncPool;
    }

    /// <summary>扫描项目内全部全局规则资产并按需重建规则池（幂等）。</summary>
    private static void SyncPool()
    {
        if (_syncing)
            return;

        _syncing = true;
        try
        {
            var rules = new List<GlobalRuleDefinition>();
            string[] guids = AssetDatabase.FindAssets("t:GlobalRuleDefinition");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GlobalRuleDefinition rule = AssetDatabase.LoadAssetAtPath<GlobalRuleDefinition>(path);
                if (rule != null)
                    rules.Add(rule);
            }

            rules.Sort((left, right) => string.CompareOrdinal(left.name, right.name));

            GlobalRulePool pool = AssetDatabase.LoadAssetAtPath<GlobalRulePool>(PoolPath);
            if (pool == null)
            {
                // 资产文件已存在但类型暂不可用（脚本未编译/导入中）时绝不重建——
                // CreateAsset 会生成新 GUID 并覆盖 .meta，导致场景里的引用失效。
                if (System.IO.File.Exists(PoolPath))
                    return;

                pool = ScriptableObject.CreateInstance<GlobalRulePool>();
                AssetDatabase.CreateAsset(pool, PoolPath);
            }

            if (RulesEqual(pool.Rules, rules))
            {
                EnsurePoolAddressable();
                return;
            }

            pool.ReplaceRules(rules);
            EditorUtility.SetDirty(pool);
            AssetDatabase.SaveAssets();
            EnsurePoolAddressable();
            Debug.Log($"[GlobalRulePoolAutoSync] 全局规则池已自动更新：{rules.Count} 条规则。");
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>
    /// 保证规则池资产已打包进 Addressables，并使用固定地址 "GlobalRulePool"——
    /// 运行时经 ResourceManager.LoadExtraResourceAsync 按该地址加载，不依赖场景序列化引用。
    /// </summary>
    private static void EnsurePoolAddressable()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
            return;

        string guid = AssetDatabase.AssetPathToGUID(PoolPath);
        if (string.IsNullOrWhiteSpace(guid))
            return;

        var entry = settings.FindAssetEntry(guid);
        if (entry == null)
        {
            var group = settings.FindGroup("LocalGroup") ?? settings.DefaultGroup;
            if (group == null)
            {
                Debug.LogError("[GlobalRulePoolAutoSync] 缺少 Addressables 组（LocalGroup/Default），无法登记规则池。");
                return;
            }

            entry = settings.CreateOrMoveEntry(guid, group);
        }

        if (entry.address != PoolAddress)
        {
            entry.address = PoolAddress;
            EditorUtility.SetDirty(settings);
        }
    }

    private static bool RulesEqual(List<GlobalRuleDefinition> current, List<GlobalRuleDefinition> target)
    {
        if (current == null || current.Count != target.Count)
            return false;

        for (int i = 0; i < current.Count; i++)
        {
            if (current[i] != target[i])
                return false;
        }

        return true;
    }
}
