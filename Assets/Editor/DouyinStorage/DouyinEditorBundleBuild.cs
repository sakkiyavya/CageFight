using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>Build native bundles for Windows Editor playback, separate from published WebGL content.</summary>
public static class DouyinEditorBundleBuild
{
    private const string Menu = "Tools/抖音对象存储/编辑器资源包测试/";
    private const string Profile = "Editor-Windows-local";

    [MenuItem(Menu + "1. 切换到 Windows（等待导入完成）")]
    public static void SwitchToWindows()
    {
        RequireIdle();
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            throw new InvalidOperationException("请通过 Unity Hub 为当前编辑器安装 Windows Build Support。");
        SelectWindowsTarget();
        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            throw new InvalidOperationException("切换 Windows 平台失败，请检查 Console。");
        Debug.Log("[EditorBundles] 等待资源导入及脚本编译完成后，执行菜单第 2 步。平台切换可能触发脚本重载，因此构建单独执行。");
    }

    [MenuItem(Menu + "2. 构建 Windows 包并启用 Existing Build")]
    public static void BuildWindows()
    {
        RequireIdle();
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            throw new InvalidOperationException("请先执行第 1 步切换到 Windows，并等待导入完成。");
        SelectWindowsTarget();
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) throw new InvalidOperationException("缺少 Addressables 设置。");
        // Exact type comparison excludes the cloud-upload subclass.
        int buildIndex = settings.DataBuilders.FindIndex(b => b != null && b.GetType() == typeof(BuildScriptPackedMode));
        int playIndex = settings.DataBuilders.FindIndex(b => b != null && b.GetType() == typeof(BuildScriptPackedPlayMode));
        if (buildIndex < 0 || playIndex < 0)
            throw new InvalidOperationException("Data Builders 中缺少 Default Build Script 或 Use Existing Build。");

        var profiles = settings.profileSettings;
        string profile = profiles.GetProfileId(Profile);
        if (string.IsNullOrEmpty(profile)) profile = profiles.AddProfile(Profile, settings.activeProfileId);
        string root = Path.GetFullPath("ServerData/Editor-local/StandaloneWindows64").Replace('\\', '/');
        profiles.SetValue(profile, "Remote.BuildPath", root);
        profiles.SetValue(profile, "Remote.LoadPath", root);
        profiles.SetValue(profile, "Local.BuildPath", "[UnityEngine.AddressableAssets.Addressables.BuildPath]/[BuildTarget]");
        profiles.SetValue(profile, "Local.LoadPath", "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]");
        string previousProfile = settings.activeProfileId;
        int previousBuilder = settings.ActivePlayerDataBuilderIndex;
        bool success = false;
        try
        {
            settings.activeProfileId = profile;
            // Refuse custom HTTP paths so Editor testing cannot accidentally use WebGL cloud bundles.
            foreach (var group in settings.groups)
            {
                if (group == null) continue;
                var schema = group.GetSchema<UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema>();
                if (schema == null || !schema.IncludeInBuild) continue;
                if (schema.LoadPath.GetValue(settings).StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("资源组仍引用云端地址，请改用 Profile 路径变量：" + group.Name);
            }
            if (settings.BuildRemoteCatalog && settings.RemoteCatalogLoadPath.GetValue(settings) != root)
                throw new InvalidOperationException("Remote Catalog 必须使用 Remote.LoadPath 变量。");
            settings.ActivePlayerDataBuilderIndex = buildIndex;
            AddressableAssetSettings.BuildPlayerContent(out var result);
            if (result == null || !string.IsNullOrEmpty(result.Error))
                throw new InvalidOperationException(result == null ? "构建未返回结果。" : result.Error);
            settings.ActivePlayModeDataBuilderIndex = playIndex;
            success = true;
            Debug.Log("[EditorBundles] Windows 资源包构建成功，已启用 Use Existing Build。现在可按 Play 测试音频和资源包加载。远程分组从本机读取，本次不验证云端网络。发布前执行第 3 步，并重新构建 WebGL Addressables。目录：" + root);
        }
        finally
        {
            if (!success) settings.activeProfileId = previousProfile;
            settings.ActivePlayerDataBuilderIndex = previousBuilder;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }

    [MenuItem(Menu + "3. 返回抖音 WebGL（之后重新构建并上传）")]
    public static void ReturnToWebGL()
    {
        RequireIdle();
        // Persist the publishing profile before a platform switch can reload the domain.
        DouyinStorageBuild.Configure();
        EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.WebGL;
        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            throw new InvalidOperationException("切换 WebGL 失败，请检查 Console。");
        Debug.Log("[EditorBundles] 返回 WebGL 后，请等待导入完成，再运行原来的构建并上传脚本，然后导出小游戏。");
    }

    private static void SelectWindowsTarget()
    {
        // SBP 1.21.25 also checks the Build Settings selection, not just the active target.
        EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Standalone;
        EditorUserBuildSettings.selectedStandaloneTarget = BuildTarget.StandaloneWindows64;
    }

    private static void RequireIdle()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("请先停止 Play，并等待 Unity 完成导入及编译。");
    }
}
