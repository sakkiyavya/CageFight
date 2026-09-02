using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 批量移除与透明区域相邻的指定颜色连通域，并直接写回 PNG 源文件。
/// </summary>
public sealed class WhiteBorderRemovalWindow : EditorWindow
{
    [SerializeField] private List<Texture2D> _sourceTextures = new List<Texture2D>();
    [SerializeField] private Color _removalColor = Color.white;
    [SerializeField] private float _colorTolerance = 0.08f;
    [SerializeField] private float _alphaThreshold = 0.01f;
    [SerializeField] private bool _useEightConnected = true;
    [SerializeField] private int _selectedTextureIndex;
    [SerializeField] private Vector2 _windowScroll;
    [SerializeField] private Vector2 _textureListScroll;

    private const string MenuPath = "Tools/剔除白边";
    private const float PreviewHeight = 280f;

    private PreviewData _preview;
    private List<FileBackup> _undoBackups;
    private bool _previewDirty = true;
    private string _statusMessage;
    private MessageType _statusType = MessageType.Info;

    private void OnEnable()
    {
        _sourceTextures ??= new List<Texture2D>();
        RemoveMissingTextures();

        if (_sourceTextures.Count == 0)
        {
            AddObjects(Selection.objects, false);
        }

        ClampSelectedIndex();
        if (_sourceTextures.Count > 0)
        {
            RefreshPreview();
        }
    }

    private void OnDisable()
    {
        DestroyPreview();
    }

    private void OnDestroy()
    {
        DestroyPreview();
        _undoBackups?.Clear();
        _undoBackups = null;
    }

    private void OnGUI()
    {
        _windowScroll = EditorGUILayout.BeginScrollView(_windowScroll);
        EditorGUILayout.Space(8f);

        DrawTextureSelection();
        EditorGUILayout.Space(10f);
        DrawParameters();
        EditorGUILayout.Space(10f);
        DrawActions();
        DrawStatus();
        EditorGUILayout.Space(10f);
        DrawPreview();

        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// 打开批量剔除白边工具，并复用当前 Project 选择作为初始素材。
    /// </summary>
    [MenuItem(MenuPath)]
    public static void Open()
    {
        WhiteBorderRemovalWindow window = GetWindow<WhiteBorderRemovalWindow>("剔除白边");
        window.minSize = new Vector2(760f, 650f);
        window.Show();
    }

    private void DrawTextureSelection()
    {
        EditorGUILayout.LabelField("素材", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        Texture2D pickedTexture = (Texture2D)EditorGUILayout.ObjectField(
            new GUIContent("添加 PNG", "选择 Project 中的 PNG 纹理"),
            null,
            typeof(Texture2D),
            false);
        if (pickedTexture != null)
        {
            AddObjects(new UnityEngine.Object[] { pickedTexture }, true);
        }

        if (GUILayout.Button("添加当前选择", GUILayout.Width(110f)))
        {
            AddObjects(Selection.objects, true);
        }

        using (new EditorGUI.DisabledScope(_sourceTextures.Count == 0))
        {
            if (GUILayout.Button("清空", GUILayout.Width(55f)))
            {
                _sourceTextures.Clear();
                _selectedTextureIndex = 0;
                DestroyPreview();
                _previewDirty = true;
            }
        }
        EditorGUILayout.EndHorizontal();

        Rect dropArea = GUILayoutUtility.GetRect(0f, 52f, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "将一个或多个 PNG（也可拖入文件夹）拖到这里", EditorStyles.helpBox);
        HandleDragAndDrop(dropArea);

        if (_sourceTextures.Count == 0)
        {
                EditorGUILayout.HelpBox("请先添加至少一个位于 Assets 目录下（不含 Plugins）的 PNG 素材。", MessageType.Info);
            return;
        }

        _textureListScroll = EditorGUILayout.BeginScrollView(_textureListScroll, GUILayout.Height(130f));
        int removeIndex = -1;
        for (int i = 0; i < _sourceTextures.Count; i++)
        {
            Texture2D texture = _sourceTextures[i];
            string assetPath = texture != null ? AssetDatabase.GetAssetPath(texture) : "（素材已丢失）";

            EditorGUILayout.BeginHorizontal();
            Color oldBackgroundColor = GUI.backgroundColor;
            if (i == _selectedTextureIndex)
            {
                GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);
            }

            if (GUILayout.Button(new GUIContent(assetPath, assetPath), GUILayout.ExpandWidth(true)))
            {
                SelectTexture(i);
            }
            GUI.backgroundColor = oldBackgroundColor;

            using (new EditorGUI.DisabledScope(texture == null))
            {
                if (GUILayout.Button("定位", GUILayout.Width(46f)))
                {
                    EditorGUIUtility.PingObject(texture);
                }
            }

            if (GUILayout.Button("移除", GUILayout.Width(46f)))
            {
                removeIndex = i;
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        if (removeIndex >= 0)
        {
            RemoveTextureAt(removeIndex);
        }

        EditorGUILayout.LabelField($"共 {_sourceTextures.Count} 个 PNG；点击列表项可切换对比预览。", EditorStyles.miniLabel);
    }

    private void DrawParameters()
    {
        EditorGUILayout.LabelField("剔除参数", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        _removalColor = EditorGUILayout.ColorField(
            new GUIContent("剔除颜色", "点击右侧取色器后，可直接从屏幕上的原图预览取色"),
            _removalColor,
            true,
            false,
            false);
        _colorTolerance = EditorGUILayout.Slider(
            new GUIContent("颜色容错率", "归一化 RGB 欧氏距离，范围为 0 到 1"),
            _colorTolerance,
            0f,
            1f);
        _alphaThreshold = EditorGUILayout.Slider(
            new GUIContent("相邻 Alpha 阈值", "连通域邻居的 alpha 严格小于此值时，整块连通域会被置透明"),
            _alphaThreshold,
            0f,
            1f);
        _useEightConnected = EditorGUILayout.Toggle(
            new GUIContent("使用八邻域", "开启时包含斜向像素；关闭时只使用上下左右四邻域"),
            _useEightConnected);

        if (EditorGUI.EndChangeCheck())
        {
            _colorTolerance = Mathf.Clamp01(_colorTolerance);
            _alphaThreshold = Mathf.Clamp01(_alphaThreshold);
            _previewDirty = true;
        }

            EditorGUILayout.HelpBox(
                "颜色距离 = √((ΔR² + ΔG² + ΔB²) / 3)，RGB 均按 0～1 归一化，Alpha 不参与颜色距离。" +
                "仅颜色距离严格小于容错率的像素参与扩散；图像外部不视作透明像素。",
                MessageType.None);

            if (Mathf.Approximately(_colorTolerance, 0f))
            {
                EditorGUILayout.HelpBox("颜色容错率采用严格“小于”判断；设为 0 时不会选中任何像素。", MessageType.Warning);
            }

        if (Mathf.Approximately(_alphaThreshold, 0f))
        {
            EditorGUILayout.HelpBox("Alpha 阈值采用严格“小于”判断；设为 0 时不会有连通域满足触发条件。", MessageType.Warning);
        }
    }

    private void DrawActions()
    {
        EditorGUILayout.BeginHorizontal();

        using (new EditorGUI.DisabledScope(GetSelectedTexture() == null))
        {
            if (GUILayout.Button("生成 / 刷新预览", GUILayout.Height(34f)))
            {
                RefreshPreview();
            }
        }

        using (new EditorGUI.DisabledScope(_sourceTextures.Count == 0))
        {
            if (GUILayout.Button("剔除并覆盖全部原素材", GUILayout.Height(34f)))
            {
                ApplyToAllTextures();
            }
        }

        using (new EditorGUI.DisabledScope(_undoBackups == null || _undoBackups.Count == 0))
        {
            if (GUILayout.Button("撤回上次覆盖", GUILayout.Height(34f)))
            {
                UndoLastApply();
            }
        }

        EditorGUILayout.EndHorizontal();

        if (_undoBackups != null && _undoBackups.Count > 0)
        {
            long byteCount = 0;
            for (int i = 0; i < _undoBackups.Count; i++)
            {
                byteCount += _undoBackups[i].OriginalBytes.LongLength;
            }

            EditorGUILayout.HelpBox(
                    $"可撤回最近一次覆盖：{_undoBackups.Count} 个文件，备份约 {byteCount / (1024f * 1024f):0.##} MB。" +
                    "再次覆盖会替换这份记录，关闭窗口或发生脚本重载后记录失效。",
                MessageType.Warning);
        }
    }

    private void DrawStatus()
    {
        if (!string.IsNullOrEmpty(_statusMessage))
        {
            EditorGUILayout.HelpBox(_statusMessage, _statusType);
        }

        if (_previewDirty && _preview != null)
        {
            EditorGUILayout.HelpBox("参数或素材已变化，当前右侧结果可能已过期；请刷新预览。", MessageType.Info);
        }
    }

    private void DrawPreview()
    {
        EditorGUILayout.LabelField("对比预览", EditorStyles.boldLabel);

        if (_preview == null)
        {
            EditorGUILayout.HelpBox("添加 PNG 后生成预览。", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField(_preview.AssetPath, EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        DrawPreviewPanel(_preview.LeftLabel, _preview.OriginalTexture);
        GUILayout.Space(6f);
        DrawPreviewPanel(_preview.RightLabel, _preview.ProcessedTexture);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField(
            $"尺寸：{_preview.OriginalTexture.width} × {_preview.OriginalTexture.height}    " +
            $"剔除连通域：{_preview.Stats.RemovedComponentCount}    " +
            $"实际置透明像素：{_preview.Stats.ClearedPixelCount}",
            EditorStyles.miniLabel);
    }

    private static void DrawPreviewPanel(string title, Texture2D texture)
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField(title, EditorStyles.centeredGreyMiniLabel);
        Rect rect = GUILayoutUtility.GetRect(100f, PreviewHeight, GUILayout.ExpandWidth(true));
        GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
        Rect contentRect = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f);
        if (texture != null)
        {
            EditorGUI.DrawTextureTransparent(contentRect, texture, ScaleMode.ScaleToFit);
        }
        EditorGUILayout.EndVertical();
    }

    private void HandleDragAndDrop(Rect dropArea)
    {
        Event currentEvent = Event.current;
        if (!dropArea.Contains(currentEvent.mousePosition))
        {
            return;
        }

        if (currentEvent.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = EventCanAddAnyTexture() ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            currentEvent.Use();
        }
        else if (currentEvent.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            AddObjects(DragAndDrop.objectReferences, true);
            currentEvent.Use();
        }
    }

    private static bool EventCanAddAnyTexture()
    {
        UnityEngine.Object[] draggedObjects = DragAndDrop.objectReferences;
        for (int i = 0; i < draggedObjects.Length; i++)
        {
            string path = AssetDatabase.GetAssetPath(draggedObjects[i]);
            if (AssetDatabase.IsValidFolder(path) || IsSupportedAssetPath(path))
            {
                return true;
            }
        }

        return false;
    }

    private void AddObjects(UnityEngine.Object[] objects, bool refreshPreview)
    {
        if (objects == null || objects.Length == 0)
        {
            return;
        }

        int addedCount = 0;
        int rejectedCount = 0;
        var pathsToAdd = new List<string>();

        for (int i = 0; i < objects.Length; i++)
        {
            string path = AssetDatabase.GetAssetPath(objects[i]);
            if (AssetDatabase.IsValidFolder(path))
            {
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { path });
                for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
                {
                    string texturePath = AssetDatabase.GUIDToAssetPath(guids[guidIndex]);
                    if (IsSupportedAssetPath(texturePath))
                    {
                        pathsToAdd.Add(texturePath);
                    }
                }
            }
            else if (IsSupportedAssetPath(path))
            {
                pathsToAdd.Add(path);
            }
            else
            {
                rejectedCount++;
            }
        }

        pathsToAdd.Sort(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < pathsToAdd.Count; i++)
        {
            string path = pathsToAdd[i];
            if (ContainsAssetPath(path))
            {
                continue;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                rejectedCount++;
                continue;
            }

            _sourceTextures.Add(texture);
            addedCount++;
        }

        if (addedCount > 0)
        {
            if (_sourceTextures.Count == addedCount)
            {
                _selectedTextureIndex = 0;
            }
            _previewDirty = true;
            _statusMessage = $"已添加 {addedCount} 个 PNG。";
            _statusType = MessageType.Info;

            if (refreshPreview)
            {
                RefreshPreview();
            }
        }
        else if (rejectedCount > 0)
        {
            _statusMessage = "未添加素材：仅支持 Assets 目录内（不含 Plugins）可原地写回 Alpha 的 PNG 文件。";
            _statusType = MessageType.Warning;
        }

        if (rejectedCount > 0 && addedCount > 0)
        {
            _statusMessage += $" 另有 {rejectedCount} 个对象不是受支持的 PNG，已忽略。";
            _statusType = MessageType.Warning;
        }
    }

    private void SelectTexture(int index)
    {
        if (index < 0 || index >= _sourceTextures.Count || index == _selectedTextureIndex)
        {
            return;
        }

        _selectedTextureIndex = index;
        _previewDirty = true;

        string assetPath = AssetDatabase.GetAssetPath(_sourceTextures[index]);
        FileBackup backup = FindUndoBackup(assetPath);
        if (backup != null)
        {
            try
            {
                byte[] currentBytes = File.ReadAllBytes(GetAbsoluteAssetPath(assetPath));
                SetComparisonPreview(
                    assetPath,
                    backup.OriginalBytes,
                    currentBytes,
                    backup.Stats,
                    "原素材（覆盖前）",
                    "新素材（已覆盖）");
                _previewDirty = false;
                return;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        RefreshPreview();
    }

    private void RemoveTextureAt(int index)
    {
        if (index < 0 || index >= _sourceTextures.Count)
        {
            return;
        }

        _sourceTextures.RemoveAt(index);
        if (_selectedTextureIndex > index)
        {
            _selectedTextureIndex--;
        }
        ClampSelectedIndex();
        _previewDirty = true;

        if (_sourceTextures.Count == 0)
        {
            DestroyPreview();
        }
        else
        {
            RefreshPreview();
        }
    }

    private void RefreshPreview()
    {
        Texture2D selectedTexture = GetSelectedTexture();
        if (selectedTexture == null)
        {
            DestroyPreview();
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(selectedTexture);
        try
        {
            byte[] sourceBytes = File.ReadAllBytes(GetAbsoluteAssetPath(assetPath));
            ProcessedFile processedFile = ProcessPng(assetPath, sourceBytes);
            SetComparisonPreview(
                assetPath,
                sourceBytes,
                processedFile.OutputBytes,
                processedFile.Stats,
                "原素材",
                "新素材预览");
            _previewDirty = false;
            _statusMessage = processedFile.Stats.ClearedPixelCount > 0
                ? $"预览完成：{processedFile.Stats.RemovedComponentCount} 个连通域，共 {processedFile.Stats.ClearedPixelCount} 个像素会被置透明。"
                : "预览完成：当前参数下没有需要置透明的像素。";
            _statusType = MessageType.Info;
        }
        catch (Exception exception)
        {
            DestroyPreview();
            _statusMessage = $"预览失败：{exception.Message}";
            _statusType = MessageType.Error;
            Debug.LogException(exception);
        }

        Repaint();
    }

    /// <summary>
    /// 先在内存中完成整批 PNG 的解码与处理，全部成功后才写回发生变化的文件。
    /// 每次成功覆盖都会以本批原始文件字节替换上一份撤回记录。
    /// </summary>
    private void ApplyToAllTextures()
    {
        RemoveMissingTextures();
        if (_sourceTextures.Count == 0)
        {
            _statusMessage = "没有可处理的 PNG 素材。";
            _statusType = MessageType.Warning;
            return;
        }

        string undoWarning = _undoBackups != null && _undoBackups.Count > 0
            ? "这会覆盖全部所选 PNG，并替换当前的一次撤回记录。"
            : "这会直接覆盖全部所选 PNG。";
        if (!EditorUtility.DisplayDialog(
                "确认剔除白边",
                undoWarning + "\n\n覆盖后可在关闭本窗口或脚本重载前撤回一次。是否继续？",
                "覆盖原素材",
                "取消"))
        {
            return;
        }

        var processedFiles = new List<ProcessedFile>(_sourceTextures.Count);
        try
        {
            for (int i = 0; i < _sourceTextures.Count; i++)
            {
                Texture2D texture = _sourceTextures[i];
                string assetPath = AssetDatabase.GetAssetPath(texture);
                EditorUtility.DisplayProgressBar(
                    "剔除白边",
                    $"计算 {i + 1}/{_sourceTextures.Count}：{assetPath}",
                    (float)i / _sourceTextures.Count);

                byte[] sourceBytes = File.ReadAllBytes(GetAbsoluteAssetPath(assetPath));
                processedFiles.Add(ProcessPng(assetPath, sourceBytes));
            }
        }
        catch (Exception exception)
        {
            _statusMessage = $"处理失败，尚未覆盖任何文件：{exception.Message}";
            _statusType = MessageType.Error;
            Debug.LogException(exception);
            return;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        var changedFiles = new List<ProcessedFile>();
        int totalClearedPixelCount = 0;
        int totalRemovedComponentCount = 0;
        for (int i = 0; i < processedFiles.Count; i++)
        {
            ProcessedFile processedFile = processedFiles[i];
            totalClearedPixelCount += processedFile.Stats.ClearedPixelCount;
            totalRemovedComponentCount += processedFile.Stats.RemovedComponentCount;
            if (processedFile.Stats.ClearedPixelCount > 0)
            {
                changedFiles.Add(processedFile);
            }
        }

        if (changedFiles.Count == 0)
        {
            _statusMessage = "处理完成：当前参数下没有任何像素需要修改，未覆盖文件。";
            _statusType = MessageType.Info;
            RefreshPreview();
            return;
        }

        var pendingWrites = new List<PendingWrite>(changedFiles.Count);
        for (int i = 0; i < changedFiles.Count; i++)
        {
            ProcessedFile file = changedFiles[i];
            pendingWrites.Add(new PendingWrite(file.AssetPath, file.OriginalBytes, file.OutputBytes));
        }

        if (!TryWriteFiles(pendingWrites, out string writeError, out List<PendingWrite> rollbackFailures))
        {
            if (rollbackFailures.Count > 0)
            {
                _undoBackups = CreateEmergencyBackups(rollbackFailures);
            }
            _statusMessage = $"覆盖失败，已尝试恢复本批次写入：{writeError}";
            _statusType = MessageType.Error;
            return;
        }

        var newUndoBackups = new List<FileBackup>(changedFiles.Count);
        for (int i = 0; i < changedFiles.Count; i++)
        {
            ProcessedFile file = changedFiles[i];
            newUndoBackups.Add(new FileBackup(
                file.AssetPath,
                file.OriginalBytes,
                ComputeSha256(file.OutputBytes),
                file.Stats));
        }
        _undoBackups = newUndoBackups;

        string selectedAssetPath = GetSelectedAssetPath();
        ProcessedFile selectedProcessedFile = processedFiles.Find(file =>
            string.Equals(file.AssetPath, selectedAssetPath, StringComparison.OrdinalIgnoreCase));
        if (selectedProcessedFile != null)
        {
            bool selectedFileChanged = selectedProcessedFile.Stats.ClearedPixelCount > 0;
            SetComparisonPreview(
                selectedProcessedFile.AssetPath,
                selectedProcessedFile.OriginalBytes,
                selectedProcessedFile.OutputBytes,
                selectedProcessedFile.Stats,
                selectedFileChanged ? "原素材（覆盖前）" : "原素材",
                selectedFileChanged ? "新素材（已覆盖）" : "当前素材（无需修改）");
        }

        _previewDirty = false;
        _statusMessage =
            $"覆盖完成：修改 {changedFiles.Count}/{processedFiles.Count} 个 PNG，" +
            $"剔除 {totalRemovedComponentCount} 个连通域，实际置透明 {totalClearedPixelCount} 个像素。";
        _statusType = MessageType.Info;
        Debug.Log($"[剔除白边] {_statusMessage}");
        Repaint();
    }

    /// <summary>
    /// 恢复最近一次成功覆盖前的整批 PNG 字节；恢复成功后立即消费并清空撤回记录。
    /// </summary>
    private void UndoLastApply()
    {
        if (_undoBackups == null || _undoBackups.Count == 0)
        {
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "撤回上次覆盖",
                $"将恢复最近一次覆盖前的 {_undoBackups.Count} 个 PNG。是否继续？",
                "恢复",
                "取消"))
        {
            return;
        }

        var pendingWrites = new List<PendingWrite>(_undoBackups.Count);
        var externallyChangedPaths = new List<string>();
        try
        {
            for (int i = 0; i < _undoBackups.Count; i++)
            {
                FileBackup backup = _undoBackups[i];
                string absolutePath = GetAbsoluteAssetPath(backup.AssetPath);
                if (!File.Exists(absolutePath))
                {
                    throw new FileNotFoundException("待撤回的 PNG 已被移动或删除。", absolutePath);
                }

                byte[] currentBytes = File.ReadAllBytes(absolutePath);
                if (!HashesEqual(ComputeSha256(currentBytes), backup.AppliedHash))
                {
                    externallyChangedPaths.Add(backup.AssetPath);
                }
                pendingWrites.Add(new PendingWrite(backup.AssetPath, currentBytes, backup.OriginalBytes));
            }
        }
        catch (Exception exception)
        {
            _statusMessage = $"撤回准备失败，未写入文件：{exception.Message}";
            _statusType = MessageType.Error;
            Debug.LogException(exception);
            return;
        }

        if (externallyChangedPaths.Count > 0 && !ConfirmOverwriteExternalChanges(externallyChangedPaths))
        {
            _statusMessage = "已取消撤回：检测到素材在本工具覆盖后又被修改。";
            _statusType = MessageType.Warning;
            return;
        }

        if (!TryWriteFiles(pendingWrites, out string writeError, out _))
        {
            _statusMessage = $"撤回失败，已尝试保留撤回前的状态：{writeError}";
            _statusType = MessageType.Error;
            return;
        }

        int restoredCount = _undoBackups.Count;
        _undoBackups.Clear();
        _undoBackups = null;
        RefreshPreview();
        _statusMessage = $"已撤回上次覆盖并恢复 {restoredCount} 个 PNG。本次撤回记录已用完。";
        _statusType = MessageType.Info;
        Debug.Log($"[剔除白边] {_statusMessage}");
    }

    /// <summary>
    /// 直接解码 PNG 源字节、执行连通域处理并重新编码，不依赖或改变 TextureImporter 的可读性与压缩设置。
    /// </summary>
    private ProcessedFile ProcessPng(string assetPath, byte[] sourceBytes)
    {
        Texture2D decodedTexture = null;
        try
        {
            decodedTexture = DecodePng(sourceBytes, Path.GetFileName(assetPath));
            Color32[] pixels = decodedTexture.GetPixels32();
            WhiteBorderRemovalStats stats = WhiteBorderRemovalProcessor.RemoveConnectedRegions(
                pixels,
                decodedTexture.width,
                decodedTexture.height,
                _removalColor,
                _colorTolerance,
                _alphaThreshold,
                _useEightConnected);

            byte[] outputBytes = sourceBytes;
            if (stats.ClearedPixelCount > 0)
            {
                decodedTexture.SetPixels32(pixels);
                decodedTexture.Apply(false, false);
                outputBytes = ImageConversion.EncodeToPNG(decodedTexture);
                if (outputBytes == null || outputBytes.Length == 0)
                {
                    throw new InvalidOperationException($"PNG 编码失败：{assetPath}");
                }
            }

            return new ProcessedFile(assetPath, sourceBytes, outputBytes, stats);
        }
        finally
        {
            if (decodedTexture != null)
            {
                DestroyImmediate(decodedTexture);
            }
        }
    }

    private static Texture2D DecodePng(byte[] bytes, string textureName)
    {
        if (bytes == null || bytes.Length == 0)
        {
            throw new InvalidDataException($"PNG 文件为空：{textureName}");
        }

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
        {
            name = textureName,
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        try
        {
            if (!ImageConversion.LoadImage(texture, bytes, false))
            {
                throw new InvalidDataException($"无法解码 PNG：{textureName}");
            }

            return texture;
        }
        catch
        {
            DestroyImmediate(texture);
            throw;
        }
    }

    private void SetComparisonPreview(
        string assetPath,
        byte[] originalBytes,
        byte[] processedBytes,
        WhiteBorderRemovalStats stats,
        string leftLabel,
        string rightLabel)
    {
        Texture2D originalTexture = null;
        Texture2D processedTexture = null;
        try
        {
            originalTexture = DecodePng(originalBytes, $"{Path.GetFileName(assetPath)} - 原图");
            processedTexture = DecodePng(processedBytes, $"{Path.GetFileName(assetPath)} - 新图");
            DestroyPreview();
            _preview = new PreviewData(
                assetPath,
                originalTexture,
                processedTexture,
                stats,
                leftLabel,
                rightLabel);
        }
        catch
        {
            if (originalTexture != null)
            {
                DestroyImmediate(originalTexture);
            }
            if (processedTexture != null)
            {
                DestroyImmediate(processedTexture);
            }
            throw;
        }
    }

    /// <summary>
    /// 写入一批已完成预计算的文件。任意写入或导入失败时，按逆序用写入前字节回滚已触碰的文件。
    /// 返回 false 表示批次未能可靠完成；error 汇总失败原因，rollbackFailures 返回未能自动恢复的文件。
    /// </summary>
    private static bool TryWriteFiles(
        List<PendingWrite> pendingWrites,
        out string error,
        out List<PendingWrite> rollbackFailures)
    {
        var writtenFiles = new List<PendingWrite>(pendingWrites.Count);
        rollbackFailures = new List<PendingWrite>();
        try
        {
            for (int i = 0; i < pendingWrites.Count; i++)
            {
                PendingWrite write = pendingWrites[i];
                EditorUtility.DisplayProgressBar(
                    "剔除白边",
                    $"写入 {i + 1}/{pendingWrites.Count}：{write.AssetPath}",
                    (float)i / pendingWrites.Count);
                WriteAllBytesAtomically(GetAbsoluteAssetPath(write.AssetPath), write.NewBytes);
                writtenFiles.Add(write);
            }

            ImportAssets(pendingWrites);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            for (int i = writtenFiles.Count - 1; i >= 0; i--)
            {
                PendingWrite write = writtenFiles[i];
                try
                {
                    WriteAllBytesAtomically(GetAbsoluteAssetPath(write.AssetPath), write.PreviousBytes);
                }
                catch (Exception rollbackException)
                {
                    rollbackFailures.Add(write);
                    Debug.LogError($"[剔除白边] 回滚失败：{write.AssetPath}\n{rollbackException}");
                }
            }

            string rollbackImportError = null;
            try
            {
                ImportAssets(writtenFiles);
            }
            catch (Exception importException)
            {
                rollbackImportError = importException.Message;
                Debug.LogError($"[剔除白边] 回滚后重新导入失败：{importException}");
            }

            var errorBuilder = new System.Text.StringBuilder(exception.Message);
            if (rollbackFailures.Count > 0)
            {
                errorBuilder.Append("；以下文件自动恢复失败，已保留窗口内应急撤回记录：");
                for (int i = 0; i < rollbackFailures.Count; i++)
                {
                    errorBuilder.AppendLine();
                    errorBuilder.Append(rollbackFailures[i].AssetPath);
                }
            }
            if (!string.IsNullOrEmpty(rollbackImportError))
            {
                errorBuilder.Append("；恢复后的资源重新导入失败：");
                errorBuilder.Append(rollbackImportError);
            }

            error = errorBuilder.ToString();
            return false;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    /// <summary>
    /// 为自动回滚失败的文件保留写入前字节，使用户修复磁盘或权限问题后仍可在窗口内重试撤回。
    /// </summary>
    private static List<FileBackup> CreateEmergencyBackups(List<PendingWrite> rollbackFailures)
    {
        var backups = new List<FileBackup>(rollbackFailures.Count);
        for (int i = 0; i < rollbackFailures.Count; i++)
        {
            PendingWrite write = rollbackFailures[i];
            byte[] currentHash;
            try
            {
                byte[] currentBytes = File.ReadAllBytes(GetAbsoluteAssetPath(write.AssetPath));
                currentHash = ComputeSha256(currentBytes);
            }
            catch (Exception exception)
            {
                currentHash = Array.Empty<byte>();
                Debug.LogWarning($"[剔除白边] 无法为应急撤回计算当前文件哈希：{write.AssetPath}\n{exception.Message}");
            }

            backups.Add(new FileBackup(
                write.AssetPath,
                write.PreviousBytes,
                currentHash,
                default));
        }

        return backups;
    }

    private static void ImportAssets(List<PendingWrite> writes)
    {
        for (int i = 0; i < writes.Count; i++)
        {
            AssetDatabase.ImportAsset(
                writes[i].AssetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }
    }

    /// <summary>
    /// 先在目标文件同目录写完临时文件，再原子替换目标，避免写入中断留下半个 PNG。
    /// </summary>
    private static void WriteAllBytesAtomically(string absolutePath, byte[] bytes)
    {
        string temporaryPath = absolutePath + ".white-border-removal-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, bytes);
            File.Replace(temporaryPath, absolutePath, null);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception cleanupException)
                {
                    Debug.LogWarning($"[剔除白边] 临时文件清理失败：{temporaryPath}\n{cleanupException.Message}");
                }
            }
        }
    }

    private static byte[] ComputeSha256(byte[] bytes)
    {
        using SHA256 sha256 = SHA256.Create();
        return sha256.ComputeHash(bytes);
    }

    private static bool HashesEqual(byte[] left, byte[] right)
    {
        if (left == null || right == null || left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool ConfirmOverwriteExternalChanges(List<string> changedPaths)
    {
        const int MaximumDisplayedPathCount = 6;
        int displayedPathCount = Mathf.Min(changedPaths.Count, MaximumDisplayedPathCount);
        var message = new System.Text.StringBuilder();
        message.AppendLine("以下素材在本工具覆盖后又发生了变化：");
        message.AppendLine();
        for (int i = 0; i < displayedPathCount; i++)
        {
            message.AppendLine(changedPaths[i]);
        }
        if (changedPaths.Count > displayedPathCount)
        {
            message.AppendLine($"……另有 {changedPaths.Count - displayedPathCount} 个文件");
        }
        message.AppendLine();
        message.Append("继续撤回会覆盖这些后续修改。是否仍要恢复本工具保存的原素材？");

        return EditorUtility.DisplayDialog(
            "检测到覆盖后的外部修改",
            message.ToString(),
            "仍然恢复",
            "取消");
    }

    private static string GetAbsoluteAssetPath(string assetPath)
    {
        if (!IsSupportedAssetPath(assetPath))
        {
            throw new InvalidOperationException($"不是可写的项目 PNG 路径：{assetPath}");
        }

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot))
        {
            throw new InvalidOperationException("无法取得 Unity 项目根目录。");
        }

        string absolutePath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        string assetsRoot = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                            + Path.DirectorySeparatorChar;
        if (!absolutePath.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"素材路径超出 Assets 目录：{assetPath}");
        }

        return absolutePath;
    }

    private bool ContainsAssetPath(string assetPath)
    {
        for (int i = 0; i < _sourceTextures.Count; i++)
        {
            if (_sourceTextures[i] != null &&
                string.Equals(AssetDatabase.GetAssetPath(_sourceTextures[i]), assetPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSupportedAssetPath(string assetPath)
    {
        return !string.IsNullOrEmpty(assetPath)
               && assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
               && !assetPath.StartsWith("Assets/Plugins/", StringComparison.OrdinalIgnoreCase)
               && string.Equals(Path.GetExtension(assetPath), ".png", StringComparison.OrdinalIgnoreCase);
    }

    private Texture2D GetSelectedTexture()
    {
        ClampSelectedIndex();
        return _sourceTextures.Count > 0 ? _sourceTextures[_selectedTextureIndex] : null;
    }

    private string GetSelectedAssetPath()
    {
        Texture2D selectedTexture = GetSelectedTexture();
        return selectedTexture != null ? AssetDatabase.GetAssetPath(selectedTexture) : null;
    }

    private void ClampSelectedIndex()
    {
        _selectedTextureIndex = _sourceTextures.Count == 0
            ? 0
            : Mathf.Clamp(_selectedTextureIndex, 0, _sourceTextures.Count - 1);
    }

    private void RemoveMissingTextures()
    {
        for (int i = _sourceTextures.Count - 1; i >= 0; i--)
        {
            Texture2D texture = _sourceTextures[i];
            if (texture == null || !IsSupportedAssetPath(AssetDatabase.GetAssetPath(texture)))
            {
                _sourceTextures.RemoveAt(i);
            }
        }

        ClampSelectedIndex();
    }

    private FileBackup FindUndoBackup(string assetPath)
    {
        if (_undoBackups == null)
        {
            return null;
        }

        for (int i = 0; i < _undoBackups.Count; i++)
        {
            if (string.Equals(_undoBackups[i].AssetPath, assetPath, StringComparison.OrdinalIgnoreCase))
            {
                return _undoBackups[i];
            }
        }

        return null;
    }

    private void DestroyPreview()
    {
        if (_preview == null)
        {
            return;
        }

        if (_preview.OriginalTexture != null)
        {
            DestroyImmediate(_preview.OriginalTexture);
        }
        if (_preview.ProcessedTexture != null)
        {
            DestroyImmediate(_preview.ProcessedTexture);
        }
        _preview = null;
    }

    private sealed class PreviewData
    {
        internal string AssetPath { get; }
        internal Texture2D OriginalTexture { get; }
        internal Texture2D ProcessedTexture { get; }
        internal WhiteBorderRemovalStats Stats { get; }
        internal string LeftLabel { get; }
        internal string RightLabel { get; }

        internal PreviewData(
            string assetPath,
            Texture2D originalTexture,
            Texture2D processedTexture,
            WhiteBorderRemovalStats stats,
            string leftLabel,
            string rightLabel)
        {
            AssetPath = assetPath;
            OriginalTexture = originalTexture;
            ProcessedTexture = processedTexture;
            Stats = stats;
            LeftLabel = leftLabel;
            RightLabel = rightLabel;
        }
    }

    private sealed class ProcessedFile
    {
        internal string AssetPath { get; }
        internal byte[] OriginalBytes { get; }
        internal byte[] OutputBytes { get; }
        internal WhiteBorderRemovalStats Stats { get; }

        internal ProcessedFile(
            string assetPath,
            byte[] originalBytes,
            byte[] outputBytes,
            WhiteBorderRemovalStats stats)
        {
            AssetPath = assetPath;
            OriginalBytes = originalBytes;
            OutputBytes = outputBytes;
            Stats = stats;
        }
    }

    private sealed class FileBackup
    {
        internal string AssetPath { get; }
        internal byte[] OriginalBytes { get; }
        internal byte[] AppliedHash { get; }
        internal WhiteBorderRemovalStats Stats { get; }

        internal FileBackup(
            string assetPath,
            byte[] originalBytes,
            byte[] appliedHash,
            WhiteBorderRemovalStats stats)
        {
            AssetPath = assetPath;
            OriginalBytes = originalBytes;
            AppliedHash = appliedHash;
            Stats = stats;
        }
    }

    private sealed class PendingWrite
    {
        internal string AssetPath { get; }
        internal byte[] PreviousBytes { get; }
        internal byte[] NewBytes { get; }

        internal PendingWrite(string assetPath, byte[] previousBytes, byte[] newBytes)
        {
            AssetPath = assetPath;
            PreviousBytes = previousBytes;
            NewBytes = newBytes;
        }
    }
}

internal readonly struct WhiteBorderRemovalStats
{
    internal int RemovedComponentCount { get; }
    internal int ClearedPixelCount { get; }

    internal WhiteBorderRemovalStats(int removedComponentCount, int clearedPixelCount)
    {
        RemovedComponentCount = removedComponentCount;
        ClearedPixelCount = clearedPixelCount;
    }
}

internal static class WhiteBorderRemovalProcessor
{
    private static readonly float[] NormalizedByteValues = CreateNormalizedByteValues();

    /// <summary>
    /// 查找颜色距离在容错率内的连通域。若域内任意像素的图内邻居 Alpha 小于阈值，
    /// 则将整个连通域的 Alpha 设为 0。颜色判断与透明邻接判断始终基于本次处理前的像素。
    /// </summary>
    internal static WhiteBorderRemovalStats RemoveConnectedRegions(
        Color32[] pixels,
        int width,
        int height,
        Color targetColor,
        float tolerance,
        float alphaThreshold,
        bool useEightConnected)
    {
        if (pixels == null)
        {
            throw new ArgumentNullException(nameof(pixels));
        }
        if (width <= 0 || height <= 0 || pixels.Length != width * height)
        {
            throw new ArgumentException("像素数量与纹理尺寸不匹配。", nameof(pixels));
        }

        tolerance = Mathf.Clamp01(tolerance);
        alphaThreshold = Mathf.Clamp01(alphaThreshold);
        targetColor.r = Mathf.Clamp01(targetColor.r);
        targetColor.g = Mathf.Clamp01(targetColor.g);
        targetColor.b = Mathf.Clamp01(targetColor.b);

        int pixelCount = pixels.Length;
        var candidate = new bool[pixelCount];
        var originalAlpha = new byte[pixelCount];
        float unnormalizedToleranceSquared = tolerance * tolerance * 3f;

        for (int i = 0; i < pixelCount; i++)
        {
            Color32 pixel = pixels[i];
            originalAlpha[i] = pixel.a;
            float deltaR = NormalizedByteValues[pixel.r] - targetColor.r;
            float deltaG = NormalizedByteValues[pixel.g] - targetColor.g;
            float deltaB = NormalizedByteValues[pixel.b] - targetColor.b;
            float distanceSquared = deltaR * deltaR + deltaG * deltaG + deltaB * deltaB;
            candidate[i] = distanceSquared < unnormalizedToleranceSquared;
        }

        var visited = new bool[pixelCount];
        var componentQueue = new int[pixelCount];
        int removedComponentCount = 0;
        int clearedPixelCount = 0;

        for (int startIndex = 0; startIndex < pixelCount; startIndex++)
        {
            if (!candidate[startIndex] || visited[startIndex])
            {
                continue;
            }

            int queueHead = 0;
            int queueTail = 0;
            bool touchesLowAlphaPixel = false;
            componentQueue[queueTail++] = startIndex;
            visited[startIndex] = true;

            while (queueHead < queueTail)
            {
                int currentIndex = componentQueue[queueHead++];
                int currentX = currentIndex % width;
                int currentY = currentIndex / width;

                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    for (int offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        if ((offsetX == 0 && offsetY == 0) ||
                            (!useEightConnected && offsetX != 0 && offsetY != 0))
                        {
                            continue;
                        }

                        int neighbourX = currentX + offsetX;
                        int neighbourY = currentY + offsetY;
                        if (neighbourX < 0 || neighbourX >= width || neighbourY < 0 || neighbourY >= height)
                        {
                            continue;
                        }

                        int neighbourIndex = neighbourY * width + neighbourX;
                        if (NormalizedByteValues[originalAlpha[neighbourIndex]] < alphaThreshold)
                        {
                            touchesLowAlphaPixel = true;
                        }

                        if (candidate[neighbourIndex] && !visited[neighbourIndex])
                        {
                            visited[neighbourIndex] = true;
                            componentQueue[queueTail++] = neighbourIndex;
                        }
                    }
                }
            }

            if (!touchesLowAlphaPixel)
            {
                continue;
            }

            int componentClearedPixelCount = 0;
            for (int componentIndex = 0; componentIndex < queueTail; componentIndex++)
            {
                int pixelIndex = componentQueue[componentIndex];
                Color32 pixel = pixels[pixelIndex];
                if (pixel.a == 0)
                {
                    continue;
                }

                pixel.a = 0;
                pixels[pixelIndex] = pixel;
                componentClearedPixelCount++;
            }

            if (componentClearedPixelCount > 0)
            {
                removedComponentCount++;
                clearedPixelCount += componentClearedPixelCount;
            }
        }

        return new WhiteBorderRemovalStats(removedComponentCount, clearedPixelCount);
    }

    private static float[] CreateNormalizedByteValues()
    {
        var values = new float[256];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = i / 255f;
        }

        return values;
    }
}
