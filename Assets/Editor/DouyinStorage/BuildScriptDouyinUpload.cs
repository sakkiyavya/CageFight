using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

/// <summary>Explicit Addressables build option for publishing the dev WebGL resources.</summary>
[CreateAssetMenu(fileName = "BuildScriptDouyinUpload", menuName = "Addressables/Content Builders/Douyin Cloud Build and Upload")]
public sealed class BuildScriptDouyinUpload : BuildScriptPackedMode
{
    public override string Name => "抖音云 dev：构建并上传";

    protected override TResult BuildDataImplementation<TResult>(AddressablesDataBuilderInput input)
    {
        try
        {
            ValidateInput(input);
            DouyinUploadSettings.Run("--check");
        }
        catch (Exception e)
        {
            return AddressableAssetBuildResult.CreateResult<TResult>(null, 0, e.Message);
        }
        var result = base.BuildDataImplementation<TResult>(input);
        if (result == null || !string.IsNullOrEmpty(result.Error)) return result;
        try
        {
            string root = Path.GetFullPath("ServerData/Douyin-dev/WebGL");
            var files = input.Registry.GetFilePaths().Select(Path.GetFullPath)
                .Where(p => string.Equals(Path.GetDirectoryName(p), root, StringComparison.OrdinalIgnoreCase))
                .Distinct().Select(p => new UploadFile { path = p, sha256 = Sha256(p) }).ToArray();
            Directory.CreateDirectory("Library/DouyinUpload");
            string manifest = Path.GetFullPath("Library/DouyinUpload/manifest.json");
            File.WriteAllText(manifest, JsonUtility.ToJson(new UploadManifest { root = root, files = files }, true));
            DouyinUploadSettings.Run("--manifest " + DouyinUploadSettings.Quote(manifest));
            Debug.Log("[DouyinUpload] 构建及上传完成，资源包已验证，Catalog/Hash 已发布到 dev。");
        }
        catch (Exception e)
        {
            result.Error = "本地构建成功，但上传未完成：" + e.Message;
        }
        return result;
    }

    private static void ValidateInput(AddressablesDataBuilderInput input)
    {
        var s = input.AddressableSettings;
        if (input.Target != BuildTarget.WebGL || input.PreviousContentState != null)
            throw new InvalidOperationException("此脚本只用于 WebGL New Build；内容更新请使用原构建脚本。");
        string load = s.profileSettings.GetValueByName(s.activeProfileId, "Remote.LoadPath");
        string build = s.profileSettings.GetValueByName(s.activeProfileId, "Remote.BuildPath");
        if (s.profileSettings.GetProfileName(s.activeProfileId) != DouyinStorageBuild.ProfileName ||
            load != DouyinStorageBuild.RemoteRoot + "/[BuildTarget]" || build != "ServerData/Douyin-dev/[BuildTarget]" ||
            !s.BuildRemoteCatalog || s.RemoteCatalogBuildPath.GetValue(s) != "ServerData/Douyin-dev/WebGL" ||
            s.RemoteCatalogLoadPath.GetValue(s) != DouyinStorageBuild.RemoteRoot + "/WebGL")
            throw new InvalidOperationException("请先执行 Tools/抖音对象存储/配置 dev 资源路径，保持 dev 路径不变。");
        foreach (var g in s.groups.Where(g => g != null))
        {
            var schema = g.GetSchema<BundledAssetGroupSchema>();
            if (schema == null || !schema.IncludeInBuild) continue;
            if (schema.LoadPath.GetValue(s).StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                (schema.LoadPath.GetValue(s) != DouyinStorageBuild.RemoteRoot + "/WebGL" ||
                 schema.BuildPath.GetValue(s) != "ServerData/Douyin-dev/WebGL"))
                throw new InvalidOperationException("远程组路径不匹配 dev 上传目标：" + g.Name);
        }
    }

    private static string Sha256(string path)
    {
        using (var h = SHA256.Create())
        using (var stream = File.OpenRead(path))
            return BitConverter.ToString(h.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    [Serializable] private sealed class UploadManifest { public string root; public UploadFile[] files; }
    [Serializable] private sealed class UploadFile { public string path; public string sha256; }
}
