using System;
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
    public int callbackOrder => -1000;
    public void OnPreprocessBuild(BuildReport report) => Validate();

    [MenuItem("Tools/抖音对象存储/校验轻量首包")]
    public static void Validate()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
        if (scenes.Length != 1 || scenes[0].path != "Assets/Scenes/Startup.unity")
            throw new BuildFailedException("首包必须仅包含 Assets/Scenes/Startup.unity；菜单场景应由 Addressables 远程加载。");
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) throw new BuildFailedException("缺少 Addressables 设置。");
        var menu = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID("Assets/Scenes/ToolTest 1.unity"));
        if (menu == null || menu.address != RemoteStartup.MenuAddress || menu.labels.Contains(RemoteStartup.PreloadLabel))
            throw new BuildFailedException("菜单场景地址必须是 StartupMenuScene，且不能带 StartupPreload 资源标签。");
        foreach (var group in settings.groups.Where(g => g != null))
        {
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null || !schema.IncludeInBuild) continue;
            if (schema.BuildPath.Id != settings.profileSettings.GetProfileDataByName("Remote.BuildPath").Id ||
                schema.LoadPath.Id != settings.profileSettings.GetProfileDataByName("Remote.LoadPath").Id)
                throw new BuildFailedException("首包不应再包含本地 Addressables 包，请检查组：" + group.Name);
            if (group.Name == "Remote First package" || group.Name == "LocalGroup" ||
                group.Name == "UI Audio" || group.Name == "StageConfig")
                foreach (var entry in group.entries.Where(e => e != menu))
                    if (!entry.labels.Contains(RemoteStartup.PreloadLabel))
                        throw new BuildFailedException("启动资源缺少 StartupPreload 标签：" + entry.AssetPath);
        }
        var animation = AssetDatabase.AssetPathToGUID("Assets/Resource/LocalResource/Animation/Load Anime AP.png");
        if (settings.FindAssetEntry(animation) != null)
            throw new BuildFailedException("加载动画应直接内置在启动场景，不应另外打入 Addressables。");
        var dependencies = AssetDatabase.GetDependencies("Assets/Scenes/Startup.unity", true);
        foreach (string path in dependencies)
        {
            if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path == "Assets/Scenes/Startup.unity" ||
                path == "Assets/Resource/LocalResource/Animation/Load Anime AP.png") continue;
            throw new BuildFailedException("启动场景出现额外资源依赖：" + path);
        }
        Debug.Log("[RemoteStartup] 首包配置校验通过：仅启动场景和加载动画，其他分组均使用远程路径。");
    }
}
