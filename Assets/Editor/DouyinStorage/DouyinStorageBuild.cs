using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

/// <summary>只使用 dev 对象存储的构建入口；不上传文件，不创建云资源。</summary>
public static class DouyinStorageBuild
{
    public const string ProfileName = "Douyin-TOS-dev";
    public const string RemoteRoot = "https://tt572c2191c956e13607-env-sqc4xosogq.tos-cn-beijing.volces.com/addressables";

    [MenuItem("Tools/抖音对象存储/配置 dev 资源路径")]
    public static void Configure()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) throw new InvalidOperationException("缺少 Addressables 设置。");
        var profiles = settings.profileSettings;
        string profile = profiles.GetProfileId(ProfileName);
        if (string.IsNullOrEmpty(profile)) profile = profiles.AddProfile(ProfileName, settings.activeProfileId);
        profiles.SetValue(profile, "Remote.BuildPath", "ServerData/Douyin-dev/[BuildTarget]");
        profiles.SetValue(profile, "Remote.LoadPath", RemoteRoot + "/[BuildTarget]");
        settings.activeProfileId = profile;
        // Build content explicitly before exporting the player. Nested content builds
        // refresh the AssetDatabase while Unity is preparing player serialization.
        settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
        settings.BuildRemoteCatalog = true;
        settings.RemoteCatalogBuildPath.SetVariableByName(settings, "Remote.BuildPath");
        settings.RemoteCatalogLoadPath.SetVariableByName(settings, "Remote.LoadPath");
        int count = 0;
        foreach (var group in settings.groups.Where(g => g != null &&
                     (g.Name.StartsWith("Remote ", StringComparison.Ordinal) ||
                      g.Name == "LocalGroup" || g.Name == "UI Audio" || g.Name == "StageConfig")))
        {
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null) continue;
            schema.BuildPath.SetVariableByName(settings, "Remote.BuildPath");
            schema.LoadPath.SetVariableByName(settings, "Remote.LoadPath");
            schema.Timeout = 60;
            schema.RetryCount = 2;
            EditorUtility.SetDirty(schema);
            count++;
        }
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log($"[DouyinStorage] 配置了 {count} 个 Remote 资源组；其他资源组保持原路径。构建后需上传文件并验证下载。");
    }

    [MenuItem("Tools/抖音对象存储/构建 WebGL 远程资源")]
    public static void BuildWebGL()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            throw new InvalidOperationException("请先切换到 WebGL 平台，再执行构建。");
        Configure();
        AddressableAssetSettings.BuildPlayerContent(out var result);
        if (!string.IsNullOrEmpty(result.Error)) throw new InvalidOperationException(result.Error);
        string directory = Path.GetFullPath("ServerData/Douyin-dev/WebGL");
        Debug.Log("[DouyinStorage] 构建完成。上传目录内容到 addressables/WebGL/：" + directory);
    }

    // 可以用 Unity -batchmode -executeMethod DouyinStorageBuild.Validate 运行。
    public static void Validate()
    {
        var storage = new DouyinObjectSaveStorage(DouyinObjectSaveStorage.DevEnvironmentId, "validation-player");
        var invalidPath = storage.WriteAsync("../other", "{}").GetAwaiter().GetResult();
        if (invalidPath.Status != SaveLoadStatus.InvalidFileName) throw new Exception("路径越界校验失败。");
        var oversize = storage.WriteAsync("slot", new string('a', DouyinObjectSaveStorage.MaxSaveBytes + 1)).GetAwaiter().GetResult();
        if (oversize.Status != SaveLoadStatus.InvalidData) throw new Exception("存档大小校验失败。");
        storage.Invalidate();
        if (storage.ReadAsync("slot").GetAwaiter().GetResult().Status != SaveLoadStatus.Unauthorized)
            throw new Exception("账号会话失效校验失败。");
        Debug.Log("[DouyinStorage] 本地校验通过：路径越界、大小限制、会话失效。未进行真机云读写。");
    }
}
