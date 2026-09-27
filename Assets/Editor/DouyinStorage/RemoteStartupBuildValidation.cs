using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Reject player builds that accidentally put the menu and its dependencies back into the player.</summary>
public sealed class RemoteStartupBuildValidation : IPreprocessBuildWithReport
{
    internal const string ReportPath = "Logs/RemoteStartupValidation.txt";
    public int callbackOrder => -1000;
    public void OnPreprocessBuild(BuildReport report) => Validate();

    [MenuItem("Tools/抖音对象存储/校验轻量首包")]
    public static void Validate()
    {
        var errors = CollectErrors();
        string message = errors.Count == 0
            ? "[RemoteStartup] 首包配置校验通过。"
            : $"[RemoteStartup] 首包配置校验失败，共 {errors.Count} 项：\n" +
              string.Join("\n", errors.Select((error, i) => $"{i + 1}. {error}"));
        message += "\n\n标签问题可用 Tools/抖音对象存储/修复启动资源标签 一次修复。" +
                   "\n修改标签后，请先重新构建并发布远程 Addressables，再用 TTSDK 构建 WebGL。";
        RemoteStartupValidationWindow.LastReport = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n" + message;
        try
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath, RemoteStartupValidationWindow.LastReport);
        }
        catch (Exception e)
        {
            Debug.LogWarning("无法保存首包校验报告：" + e.Message);
        }
        if (errors.Count > 0)
        {
            // The SDK may clear Console after its modal dialog. Keep diagnostics in a separate window and file.
            if (!Application.isBatchMode)
                EditorApplication.delayCall += RemoteStartupValidationWindow.ShowReport;
            throw new BuildFailedException(message + "\n完整报告：" + Path.GetFullPath(ReportPath));
        }
        Debug.Log(message);
    }

    internal static List<string> CollectErrors()
    {
        var errors = new List<string>();
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
        if (scenes.Length != 1 || scenes[0].path != "Assets/Scenes/Startup.unity")
            errors.Add("首包必须仅包含 Assets/Scenes/Startup.unity；菜单场景应由 Addressables 远程加载。");
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
            errors.Add("缺少 Addressables 设置。");
        else
        {
            var menu = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID("Assets/Scenes/ToolTest 1.unity"));
            if (menu == null || menu.address != RemoteStartup.MenuAddress || menu.labels.Contains(RemoteStartup.PreloadLabel))
                errors.Add("菜单场景地址必须是 StartupMenuScene，且不能带 StartupPreload 资源标签。");
            var buildPath = settings.profileSettings.GetProfileDataByName("Remote.BuildPath");
            var loadPath = settings.profileSettings.GetProfileDataByName("Remote.LoadPath");
            if (buildPath == null || loadPath == null)
                errors.Add("Addressables Profile 缺少 Remote.BuildPath 或 Remote.LoadPath。");
            foreach (var group in settings.groups.Where(g => g != null).OrderBy(g => g.Name, StringComparer.Ordinal))
            {
                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null || !schema.IncludeInBuild) continue;
                if (buildPath != null && loadPath != null &&
                    (schema.BuildPath.Id != buildPath.Id || schema.LoadPath.Id != loadPath.Id))
                    errors.Add("首包不应再包含本地 Addressables 包，请检查组：" + group.Name);
                if (RequiresPreload(group.Name))
                    foreach (var entry in group.entries.Where(e => e != menu).OrderBy(e => e.AssetPath, StringComparer.Ordinal))
                        if (!entry.labels.Contains(RemoteStartup.PreloadLabel))
                            errors.Add("启动资源缺少 StartupPreload 标签：[" + group.Name + "] " + entry.AssetPath);
            }
            foreach (string localPath in new[] { RemoteStartup.StartupCoverPath, RemoteStartup.StageAnimationPath })
                if (settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(localPath)) != null)
                    errors.Add("加载画面应直接内置在启动场景，不应另外打入 Addressables：" + localPath);
        }
        var dependencies = AssetDatabase.GetDependencies("Assets/Scenes/Startup.unity", true);
        foreach (string path in dependencies)
        {
            if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path == "Assets/Scenes/Startup.unity" ||
                path == RemoteStartup.StageAnimationPath || path == RemoteStartup.StartupCoverPath) continue;
            errors.Add("启动场景出现额外资源依赖：" + path);
        }
        return errors;
    }

    private static bool RequiresPreload(string groupName) =>
        groupName == "Remote First package" || groupName == "LocalGroup" ||
        groupName == "UI Audio" || groupName == "StageConfig";

    [MenuItem("Tools/抖音对象存储/修复启动资源标签")]
    public static void RepairLabels()
    {
        if (BuildPipeline.isBuildingPlayer || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new BuildFailedException("请等待构建、编译和资源导入完成后再修复标签。");
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) throw new BuildFailedException("缺少 Addressables 设置。");
        var menu = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID("Assets/Scenes/ToolTest 1.unity"));
        int count = 0;
        Undo.RecordObject(settings, "修复启动资源标签");
        if (menu != null && menu.labels.Contains(RemoteStartup.PreloadLabel))
        {
            Undo.RecordObject(menu.parentGroup, "修复启动资源标签");
            if (menu.SetLabel(RemoteStartup.PreloadLabel, false)) count++;
        }
        foreach (var group in settings.groups.Where(g => g != null && RequiresPreload(g.Name)))
        {
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null || !schema.IncludeInBuild) continue;
            Undo.RecordObject(group, "修复启动资源标签");
            foreach (var entry in group.entries.Where(e => e != menu))
                if (entry.SetLabel(RemoteStartup.PreloadLabel, true, true)) count++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[RemoteStartup] 已修复 {count} 项标签。请重新构建并发布远程 Addressables，再构建 WebGL。");
        Validate();
        if (!Application.isBatchMode) RemoteStartupValidationWindow.ShowReport();
    }
}

/// <summary>Diagnostics survive Console clearing and editor restarts.</summary>
public sealed class RemoteStartupValidationWindow : EditorWindow
{
    internal static string LastReport;
    private Vector2 scroll;

    [MenuItem("Tools/抖音对象存储/查看首包校验报告")]
    public static void ShowReport()
    {
        GetWindow<RemoteStartupValidationWindow>("首包校验报告").Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("报告独立于 Console 保存。修复标签后需要重新构建并发布远程 Addressables。", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("重新校验"))
                EditorApplication.delayCall += RemoteStartupBuildValidation.Validate;
            using (new EditorGUI.DisabledScope(BuildPipeline.isBuildingPlayer || EditorApplication.isCompiling || EditorApplication.isUpdating))
                if (GUILayout.Button("一键修复标签"))
                    EditorApplication.delayCall += RemoteStartupBuildValidation.RepairLabels;
        }
        if (LastReport == null)
            LastReport = File.Exists(RemoteStartupBuildValidation.ReportPath)
                ? File.ReadAllText(RemoteStartupBuildValidation.ReportPath) : "暂无报告，请点击重新校验。";
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(LastReport, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }
}
