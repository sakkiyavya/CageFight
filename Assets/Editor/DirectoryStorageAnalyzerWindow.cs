using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>统计构建输出目录中的文件长度，不代表平台压缩后的上传包大小。</summary>
public sealed class DirectoryStorageAnalyzerWindow : EditorWindow
{
    private sealed class FileEntry
    {
        public string Path;
        public string RelativePath;
        public long Size;
    }

    [SerializeField] private string directory = "Assets";
    private bool includeMeta = false;
    private readonly List<FileEntry> files = new List<FileEntry>();
    private readonly List<FileEntry> visibleFiles = new List<FileEntry>();
    private readonly Stack<string> pendingDirectories = new Stack<string>();
    private IEnumerator<string> entries;
    private string scanRoot;
    private string search = "";
    private string status = "请选择抖音小游戏构建输出目录并点击开始分析。";
    private string lastError;
    private bool scanning;
    private bool descending = true;
    private bool scanIncludeMeta;
    private long totalSize;
    private long filteredSize;
    private int skippedCount;
    private Vector2 scroll;
    private const float RowHeight = 22f;

    [MenuItem("Tools/目录存储占用分析")]
    private static void Open()
    {
        GetWindow<DirectoryStorageAnalyzerWindow>("目录存储分析");
    }

    private void OnEnable()
    {
        minSize = new Vector2(640f, 380f);
    }

    private void OnDisable()
    {
        if (scanning)
            FinishScan(true);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(scanning))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                directory = EditorGUILayout.TextField("分析目录", directory);
                if (GUILayout.Button("选择目录", GUILayout.Width(80f)))
                {
                    string selected = EditorUtility.OpenFolderPanel("选择抖音小游戏构建输出目录", Application.dataPath, "");
                    if (!string.IsNullOrEmpty(selected))
                        directory = selected;
                }
                if (GUILayout.Button("使用选中目录", GUILayout.Width(100f)))
                {
                    string selected = AssetDatabase.GetAssetPath(Selection.activeObject);
                    if (AssetDatabase.IsValidFolder(selected))
                        directory = selected;
                    else
                        ShowNotification(new GUIContent("请在 Project 窗口中选中一个文件夹"));
                }
            }
            includeMeta = EditorGUILayout.ToggleLeft("包含 .meta 文件（默认排除）", includeMeta);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(scanning ? "取消扫描" : "开始分析", GUILayout.Width(100f)))
            {
                if (scanning)
                    FinishScan(true);
                else
                    StartScan();
            }
            GUILayout.Label(status);
        }
        EditorGUILayout.HelpBox("请选择抖音小游戏构建输出目录。递归统计输出文件大小，默认排除 .meta 文件（每 KB = 1024 字节）。结果为所选目录的文件总量，不区分主包、分包和远程资源，也不等同于平台压缩后的上传包大小。跳过符号链接及目录联接。", MessageType.Info);
        if (!string.IsNullOrEmpty(scanRoot))
            EditorGUILayout.LabelField("本次分析", scanRoot);
        EditorGUILayout.LabelField("统计结果", $"{files.Count:N0} 个文件    合计 {FormatSize(totalSize)}（{totalSize:N0} 字节）    跳过 {skippedCount:N0} 项");
        if (skippedCount > 0 && !string.IsNullOrEmpty(lastError))
            EditorGUILayout.HelpBox("最近跳过项：" + lastError, MessageType.Warning);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginChangeCheck();
            search = EditorGUILayout.TextField("搜索路径", search);
            if (EditorGUI.EndChangeCheck())
                RefreshVisibleFiles();
            if (GUILayout.Button(descending ? "大小 ↓" : "大小 ↑", GUILayout.Width(80f)))
            {
                descending = !descending;
                SortFiles();
                RefreshVisibleFiles();
            }
        }
        EditorGUILayout.LabelField($"显示 {visibleFiles.Count:N0} 个文件，共 {FormatSize(filteredSize)}");
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("相对路径");
            GUILayout.Label("大小 / 占总量", GUILayout.Width(175f));
            GUILayout.Space(60f);
        }
        DrawFileList();
    }

    private void DrawFileList()
    {
        // 只绘制可见行，避免大量文件导致 IMGUI 卡顿。
        Rect viewport = GUILayoutUtility.GetRect(0f, 100000f, 0f, 100000f, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
        float width = Mathf.Max(1f, viewport.width - 18f);
        scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0f, 0f, width, visibleFiles.Count * RowHeight));
        int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / RowHeight));
        int end = Mathf.Min(visibleFiles.Count, first + Mathf.CeilToInt(viewport.height / RowHeight) + 1);
        for (int i = first; i < end; i++)
        {
            FileEntry file = visibleFiles[i];
            float y = i * RowHeight;
            if (i % 2 == 0)
                EditorGUI.DrawRect(new Rect(0f, y, width, RowHeight), new Color(0.5f, 0.5f, 0.5f, 0.08f));
            GUI.Label(new Rect(4f, y, Mathf.Max(1f, width - 245f), RowHeight), new GUIContent(file.RelativePath, file.Path));
            double percent = totalSize == 0 ? 0d : file.Size * 100d / totalSize;
            GUI.Label(new Rect(width - 235f, y, 175f, RowHeight), new GUIContent($"{FormatSize(file.Size)} / {percent:F2}%", $"{file.Size:N0} 字节"));
            if (GUI.Button(new Rect(width - 58f, y + 1f, 56f, RowHeight - 2f), "定位"))
                EditorUtility.RevealInFinder(file.Path);
        }
        GUI.EndScrollView();
    }

    private void StartScan()
    {
        string root;
        try
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("请输入目录路径。");
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            root = Path.GetFullPath(Path.IsPathRooted(directory) ? directory : Path.Combine(projectRoot, directory));
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException("目录不存在：" + root);
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("请选择实际目录，不能扫描符号链接或目录联接。");
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            status = exception.Message;
            return;
        }

        scanRoot = root;
        scanIncludeMeta = includeMeta;
        files.Clear();
        visibleFiles.Clear();
        pendingDirectories.Clear();
        pendingDirectories.Push(root);
        totalSize = filteredSize = 0;
        skippedCount = 0;
        lastError = null;
        scroll = Vector2.zero;
        scanning = true;
        status = "正在扫描，完成后显示文件列表…";
        EditorApplication.update += ScanStep;
    }

    private void ScanStep()
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (timer.ElapsedMilliseconds < 8)
        {
            string currentPath = scanRoot;
            try
            {
                if (entries == null)
                {
                    if (pendingDirectories.Count == 0)
                    {
                        FinishScan(false);
                        return;
                    }
                    currentPath = pendingDirectories.Pop();
                    entries = Directory.EnumerateFileSystemEntries(currentPath).GetEnumerator();
                }
                if (!entries.MoveNext())
                {
                    DisposeEntries();
                    continue;
                }
                currentPath = entries.Current;
            }
            catch (Exception exception) when (IsFileSystemException(exception))
            {
                RecordSkipped(currentPath, exception.Message);
                DisposeEntries();
                continue;
            }

            try
            {
                FileAttributes attributes = File.GetAttributes(currentPath);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    RecordSkipped(currentPath, "符号链接或目录联接");
                    continue;
                }
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pendingDirectories.Push(currentPath);
                    continue;
                }
                if (!scanIncludeMeta && string.Equals(Path.GetExtension(currentPath), ".meta", StringComparison.OrdinalIgnoreCase))
                    continue;

                long size = new FileInfo(currentPath).Length;
                files.Add(new FileEntry
                {
                    Path = currentPath,
                    RelativePath = currentPath.Substring(scanRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    Size = size
                });
                totalSize += size;
            }
            catch (Exception exception) when (IsFileSystemException(exception))
            {
                RecordSkipped(currentPath, exception.Message);
            }
        }
        Repaint();
    }

    private void FinishScan(bool cancelled)
    {
        EditorApplication.update -= ScanStep;
        DisposeEntries();
        pendingDirectories.Clear();
        scanning = false;
        status = cancelled ? "已取消，显示已扫描的部分结果。" : "分析完成。";
        SortFiles();
        RefreshVisibleFiles();
        Repaint();
    }

    private void DisposeEntries()
    {
        entries?.Dispose();
        entries = null;
    }

    private void RecordSkipped(string path, string reason)
    {
        skippedCount++;
        lastError = path + "：" + reason;
    }

    private static bool IsFileSystemException(Exception exception)
    {
        return exception is IOException || exception is UnauthorizedAccessException ||
               exception is System.Security.SecurityException || exception is ArgumentException ||
               exception is NotSupportedException;
    }

    private void SortFiles()
    {
        files.Sort((left, right) =>
        {
            int comparison = descending ? right.Size.CompareTo(left.Size) : left.Size.CompareTo(right.Size);
            return comparison != 0 ? comparison : string.Compare(left.RelativePath, right.RelativePath, StringComparison.OrdinalIgnoreCase);
        });
    }

    private void RefreshVisibleFiles()
    {
        visibleFiles.Clear();
        filteredSize = 0;
        foreach (FileEntry file in files)
        {
            if (!string.IsNullOrEmpty(search) && file.RelativePath.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            visibleFiles.Add(file);
            filteredSize += file.Size;
        }
        scroll = Vector2.zero;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return bytes + " B";
        if (bytes < 1024L * 1024) return (bytes / 1024d).ToString("F2") + " KB";
        if (bytes < 1024L * 1024 * 1024) return (bytes / (1024d * 1024)).ToString("F2") + " MB";
        if (bytes < 1024L * 1024 * 1024 * 1024) return (bytes / (1024d * 1024 * 1024)).ToString("F2") + " GB";
        return (bytes / (1024d * 1024 * 1024 * 1024)).ToString("F2") + " TB";
    }
}
