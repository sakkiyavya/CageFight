using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public sealed class DouyinUploadSettings : EditorWindow
{
    private const string PythonPref = "CageFight.DouyinUpload.Python";
    private string ak = "", sk = "";
    [MenuItem("Tools/抖音对象存储/自动上传设置")]
    public static void Open() => GetWindow<DouyinUploadSettings>("抖音云自动上传");
    private void OnGUI()
    {
        EditorGUILayout.LabelField("目标：笼斗 / dev / addressables/WebGL/", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("使用对象存储 AK/SK。密钥仅保留在当前 Unity 进程；重启后重新输入，也可通过同名环境变量传入。", MessageType.Info);
        string python = EditorPrefs.GetString(PythonPref, "python");
        string value = EditorGUILayout.TextField("Python 可执行文件", python);
        if (value != python) EditorPrefs.SetString(PythonPref, value);
        ak = EditorGUILayout.PasswordField("Access Key ID", ak);
        sk = EditorGUILayout.PasswordField("Secret Access Key", sk);
        if (GUILayout.Button("应用到当前会话"))
        {
            Environment.SetEnvironmentVariable("DOUYIN_TOS_AK", ak.Trim());
            Environment.SetEnvironmentVariable("DOUYIN_TOS_SK", sk.Trim());
            ak = sk = "";
        }
        if (GUILayout.Button("检查 Python、SDK 和凭证配置"))
        {
            try { Run("--check"); Debug.Log("[DouyinUpload] 本机配置检查通过；尚未校验云端写权限。"); }
            catch (Exception e) { Debug.LogError(e.Message); }
        }
        EditorGUILayout.HelpBox("首次安装依赖：在项目根目录运行\npython -m pip install --target Library/DouyinUpload/python-packages tos==2.9.2\n\n然后在 Addressables Groups → Build → New Build 中选择“抖音云 dev：构建并上传”。", MessageType.None);
    }

    internal static string Quote(string s) => "\"" + s.Replace("\\", "/").Replace("\"", "\\\"") + "\"";
    internal static void Run(string arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = EditorPrefs.GetString(PythonPref, "python"),
            Arguments = "-B -u " + Quote(Path.GetFullPath("Assets/Editor/DouyinStorage/upload_tos.py")) + " " + arguments,
            WorkingDirectory = Path.GetFullPath("."), UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        info.EnvironmentVariables["PYTHONPATH"] = Path.GetFullPath("Library/DouyinUpload/python-packages");
        info.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
        var messages = new ConcurrentQueue<string>();
        using (var process = new Process { StartInfo = info })
        {
            process.OutputDataReceived += (_, e) => { if (e.Data != null) messages.Enqueue(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) messages.Enqueue(e.Data); };
            process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
            var timer = Stopwatch.StartNew();
            try
            {
                while (!process.WaitForExit(100))
                {
                    while (messages.TryDequeue(out var line)) Debug.Log("[DouyinUpload] " + line);
                    if (timer.Elapsed.TotalMinutes > 30 || (!Application.isBatchMode &&
                        EditorUtility.DisplayCancelableProgressBar("抖音云自动上传", "正在检查或上传；详细进度见 Console", 0.5f)))
                    {
                        process.Kill(); process.WaitForExit();
                        throw new OperationCanceledException("上传已取消或超时；已上传文件保留，可重新构建重试。");
                    }
                }
                process.WaitForExit();
                while (messages.TryDequeue(out var line)) Debug.Log("[DouyinUpload] " + line);
                if (process.ExitCode != 0) throw new InvalidOperationException("上传工具失败，请查看 Console 中的 ERROR（密钥不会输出）。");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }
    }
}
