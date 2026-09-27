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
    private string checkMessage;
    private MessageType checkMessageType;
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
        bool hasCredentials = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOUYIN_TOS_AK")) &&
                              !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOUYIN_TOS_SK"));
        EditorGUILayout.HelpBox(hasCredentials ? "当前会话：AK/SK 已配置（尚未验证云端权限）。" :
            "当前会话：缺少 AK 或 SK，请填写下方两项。", hasCredentials ? MessageType.Info : MessageType.Warning);
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(ak) || string.IsNullOrWhiteSpace(sk)))
        {
            if (GUILayout.Button("应用到当前会话"))
            {
                ApplyCredentials();
                checkMessage = "密钥已应用。输入框已清空，当前会话中的密钥仍然有效。";
                checkMessageType = MessageType.Info;
            }
        }
        if (GUILayout.Button("检查 Python、SDK 和凭证配置"))
        {
            try
            {
                // Checking immediately after filling the fields should also work without a separate Apply click.
                if (!string.IsNullOrWhiteSpace(ak) || !string.IsNullOrWhiteSpace(sk)) ApplyCredentials();
                Run("--check");
                checkMessage = "本机配置检查通过；尚未校验云端写权限。";
                checkMessageType = MessageType.Info;
                Debug.Log("[DouyinUpload] " + checkMessage);
            }
            catch (Exception e)
            {
                checkMessage = e.Message;
                checkMessageType = MessageType.Error;
                Debug.LogError(checkMessage);
            }
        }
        if (!string.IsNullOrEmpty(checkMessage)) EditorGUILayout.HelpBox(checkMessage, checkMessageType);
        EditorGUILayout.HelpBox("首次安装依赖：在项目根目录运行\npython -m pip install --target Library/DouyinUpload/python-packages tos==2.9.2\n\n然后在 Addressables Groups → Build → New Build 中选择“抖音云 dev：构建并上传”。", MessageType.None);
    }

    private void ApplyCredentials()
    {
        if (string.IsNullOrWhiteSpace(ak) || string.IsNullOrWhiteSpace(sk))
            throw new InvalidOperationException("请同时填写 Access Key ID 和 Secret Access Key。原有会话配置未被修改。");
        Environment.SetEnvironmentVariable("DOUYIN_TOS_AK", ak.Trim());
        Environment.SetEnvironmentVariable("DOUYIN_TOS_SK", sk.Trim());
        ak = sk = "";
    }

    internal static string Quote(string s) => "\"" + s.Replace("\\", "/").Replace("\"", "\\\"") + "\"";
    internal static void Run(string arguments)
    {
        string accessKey = Environment.GetEnvironmentVariable("DOUYIN_TOS_AK");
        string secretKey = Environment.GetEnvironmentVariable("DOUYIN_TOS_SK");
        if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException("当前 Unity 会话缺少上传密钥。请打开 Tools/抖音对象存储/自动上传设置，填写 AK/SK 并点击“应用到当前会话”。Unity 重启后需要重新配置。");
        var info = new ProcessStartInfo
        {
            FileName = EditorPrefs.GetString(PythonPref, "python"),
            Arguments = "-B -u " + Quote(Path.GetFullPath("Assets/Editor/DouyinStorage/upload_tos.py")) + " " + arguments,
            WorkingDirectory = Path.GetFullPath("."), UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        info.EnvironmentVariables["PYTHONPATH"] = Path.GetFullPath("Library/DouyinUpload/python-packages");
        info.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
        // Explicitly pass the current managed-process values to Python, including keys applied after Unity started.
        info.EnvironmentVariables["DOUYIN_TOS_AK"] = accessKey;
        info.EnvironmentVariables["DOUYIN_TOS_SK"] = secretKey;
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
