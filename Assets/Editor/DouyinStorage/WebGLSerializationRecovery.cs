using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Rebuild player data and code together after a serialization layout mismatch.</summary>
public static class WebGLSerializationRecovery
{
    [MenuItem("Tools/抖音对象存储/修复序列化布局/启用 TTSDK 干净构建")]
    public static void ConfigureCleanExport()
    {
        if (BuildPipeline.isBuildingPlayer || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new BuildFailedException("请等待当前构建、编译和导入完成。");
        var sdk = AssetDatabase.LoadMainAssetAtPath("Assets/Editor/StarkBuilderSetting.asset");
        if (sdk == null) throw new BuildFailedException("找不到 TTSDK 构建设置。");
        var serialized = new SerializedObject(sdk);
        var options = serialized.FindProperty("buildOptions");
        if (options == null) throw new BuildFailedException("TTSDK 设置缺少 buildOptions 字段。");
        options.intValue = (options.intValue | (int)BuildOptions.CleanBuildCache) & ~(int)BuildOptions.BuildScriptsOnly;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sdk);

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) throw new BuildFailedException("缺少 Addressables 设置。");
        settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log("[WebGLSerializationRecovery] 已启用 TTSDK Clean Build Cache，关闭 Scripts Only 和导出时自动重建 Addressables。请在 TTSDK 中执行完整构建 WebGL。");
    }

    [MenuItem("Tools/抖音对象存储/修复序列化布局/干净构建 WebGL 验证包")]
    public static void BuildCleanPlayer()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            throw new BuildFailedException("请等待编译、导入结束，并退出 Play 模式后重试。");
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            throw new BuildFailedException("请先切换到 WebGL，等待导入完成后重试。");

        ConfigureCleanExport();
        RemoteStartupBuildValidation.Validate();

        // A local diagnostic player only: never invoke the cloud-upload builder here.
        string output = Path.GetFullPath("Temp/WebGLSerializationCheck");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = output,
            target = BuildTarget.WebGL,
            options = BuildOptions.CleanBuildCache
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException("WebGL 干净构建失败，请查看 Console。");
        Debug.Log("[WebGLSerializationRecovery] 干净构建完成：" + output +
                  "。此包仅用于验证；正式发布请先单独构建 Addressables，再用 TTSDK 完整构建小游戏（不要仅转换旧包）。");
    }
}
