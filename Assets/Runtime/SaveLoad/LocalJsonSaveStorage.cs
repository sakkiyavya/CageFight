using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 用于当前 Editor/Standalone 测试阶段的本地 JSON 存储。
/// 文件位于 Application.persistentDataPath/SaveData/{fileName}.json。
/// 抖音小游戏/WebGL 应在接入时替换为平台或云存储实现。
/// </summary>
public sealed class LocalJsonSaveStorage : ISaveStorage
{
    public const string SaveDirectoryName = "SaveData";
    public const string SaveFileExtension = SaveFileNameUtility.JsonFileExtension;

    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false, true);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> IoGatesByPath
        = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

    private readonly string _directoryPath;

    public string DirectoryPath => _directoryPath;

    public static string DefaultDirectoryPath => Path.Combine(
        Application.persistentDataPath,
        SaveDirectoryName);

    public LocalJsonSaveStorage()
        : this(DefaultDirectoryPath)
    {
    }

    /// <summary>
    /// 允许测试或其他项目传入自定义目录；正式运行默认使用本地固定目录。
    /// </summary>
    public LocalJsonSaveStorage(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("存档目录不能为空。", nameof(directoryPath));
        }

        _directoryPath = Path.GetFullPath(directoryPath);
    }

    /// <summary>
    /// 获取逻辑文件名对应的完整本地 JSON 路径。
    /// </summary>
    public string GetFilePath(string fileName)
    {
        if (!TryResolveFilePath(fileName, out string filePath, out string error))
        {
            throw new ArgumentException(error, nameof(fileName));
        }

        return filePath;
    }

    public async Task<SaveLoadResult> WriteAsync(
        string fileName,
        string json,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveFilePath(fileName, out string filePath, out string fileNameError))
        {
            return SaveLoadResult.Failed(SaveLoadStatus.InvalidFileName, fileNameError);
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return SaveLoadResult.Failed(SaveLoadStatus.InvalidData, "不能写入空的 JSON 存档。");
        }

        SemaphoreSlim ioGate = GetIoGate(filePath);
        bool gateEntered = false;
        // 同一路径的所有实例共享 I/O 锁，因此可以安全复用固定临时文件。
        // 上次异常退出留下的临时文件会在本次写入时被覆盖。
        string temporaryFilePath = filePath + ".tmp";

        try
        {
            await ioGate.WaitAsync(cancellationToken);
            gateEntered = true;
            cancellationToken.ThrowIfCancellationRequested();

            Directory.CreateDirectory(_directoryPath);

            using (FileStream stream = new FileStream(
                       temporaryFilePath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None))
            using (StreamWriter writer = new StreamWriter(stream, Utf8WithoutBom))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            CommitTemporaryFile(temporaryFilePath, filePath);
            return SaveLoadResult.Succeeded($"本地存档 {fileName} 写入成功。");
        }
        catch (OperationCanceledException)
        {
            return SaveLoadResult.Failed(SaveLoadStatus.Cancelled, "保存操作已取消。");
        }
        catch (EncoderFallbackException exception)
        {
            return SaveLoadResult.Failed(
                SaveLoadStatus.InvalidData,
                $"存档中包含无法编码为 UTF-8 的文本：{exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return SaveLoadResult.Failed(
                SaveLoadStatus.Unauthorized,
                $"没有权限写入本地存档：{exception.Message}");
        }
        catch (IOException exception)
        {
            return CreateIoFailure("写入本地存档失败", exception);
        }
        catch (NotSupportedException exception)
        {
            return SaveLoadResult.Failed(
                SaveLoadStatus.Unavailable,
                $"当前平台不支持本地存档写入：{exception.Message}");
        }
        catch (Exception exception)
        {
            return SaveLoadResult.Failed(
                SaveLoadStatus.UnknownError,
                $"写入本地存档时发生未知错误：{exception.Message}");
        }
        finally
        {
            if (gateEntered)
            {
                TryDeleteTemporaryFile(temporaryFilePath);
                ioGate.Release();
            }
        }
    }

    public async Task<SaveReadResult> ReadAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveFilePath(fileName, out string filePath, out string fileNameError))
        {
            return CreateReadFailure(SaveLoadStatus.InvalidFileName, fileNameError);
        }

        SemaphoreSlim ioGate = GetIoGate(filePath);
        bool gateEntered = false;

        try
        {
            await ioGate.WaitAsync(cancellationToken);
            gateEntered = true;
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(filePath))
            {
                return new SaveReadResult(SaveLoadResult.Failed(
                    SaveLoadStatus.NotFound,
                    $"尚未创建本地存档 {fileName}。"));
            }

            string json = File.ReadAllText(filePath, Utf8WithoutBom);
            return new SaveReadResult(
                SaveLoadResult.Succeeded($"本地存档 {fileName} 读取成功。"),
                json);
        }
        catch (OperationCanceledException)
        {
            return CreateReadFailure(SaveLoadStatus.Cancelled, "读取操作已取消。");
        }
        catch (FileNotFoundException)
        {
            return CreateReadFailure(SaveLoadStatus.NotFound, "尚未创建本地存档。");
        }
        catch (DirectoryNotFoundException)
        {
            return CreateReadFailure(SaveLoadStatus.NotFound, "尚未创建本地存档。");
        }
        catch (DecoderFallbackException exception)
        {
            return CreateReadFailure(
                SaveLoadStatus.InvalidData,
                $"本地存档不是有效的 UTF-8 文本：{exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return CreateReadFailure(
                SaveLoadStatus.Unauthorized,
                $"没有权限读取本地存档：{exception.Message}");
        }
        catch (IOException exception)
        {
            return CreateReadIoFailure("读取本地存档失败", exception);
        }
        catch (NotSupportedException exception)
        {
            return CreateReadFailure(
                SaveLoadStatus.Unavailable,
                $"当前平台不支持本地存档读取：{exception.Message}");
        }
        catch (Exception exception)
        {
            return CreateReadFailure(
                SaveLoadStatus.UnknownError,
                $"读取本地存档时发生未知错误：{exception.Message}");
        }
        finally
        {
            if (gateEntered)
            {
                ioGate.Release();
            }
        }
    }

    private static void CommitTemporaryFile(string temporaryFilePath, string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Replace(temporaryFilePath, filePath, null);
            return;
        }

        File.Move(temporaryFilePath, filePath);
    }

    private static SemaphoreSlim GetIoGate(string filePath)
    {
        return IoGatesByPath.GetOrAdd(
            filePath,
            _ => new SemaphoreSlim(1, 1));
    }

    private bool TryResolveFilePath(
        string fileName,
        out string filePath,
        out string error)
    {
        filePath = null;

        if (!SaveFileNameUtility.TryValidate(fileName, out error))
        {
            return false;
        }

        try
        {
            filePath = Path.GetFullPath(Path.Combine(
                _directoryPath,
                fileName + SaveFileExtension));
        }
        catch (Exception exception) when (
            exception is ArgumentException ||
            exception is NotSupportedException ||
            exception is PathTooLongException)
        {
            error = $"存档文件名 '{fileName}' 无法转换为有效路径：{exception.Message}";
            return false;
        }

        string directoryPrefix = _directoryPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!filePath.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase))
        {
            filePath = null;
            error = "存档路径不能位于存档目录之外。";
            return false;
        }

        error = null;
        return true;
    }

    private static SaveLoadResult CreateIoFailure(string message, Exception exception)
    {
        return SaveLoadResult.Failed(
            SaveLoadStatus.IoError,
            $"{message}：{exception.Message}");
    }

    private static SaveReadResult CreateReadIoFailure(string message, Exception exception)
    {
        return CreateReadFailure(
            SaveLoadStatus.IoError,
            $"{message}：{exception.Message}");
    }

    private static SaveReadResult CreateReadFailure(SaveLoadStatus status, string message)
    {
        return new SaveReadResult(SaveLoadResult.Failed(status, message));
    }

    private static void TryDeleteTemporaryFile(string temporaryFilePath)
    {
        try
        {
            if (File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
            }
        }
        catch
        {
            // 临时文件清理失败不应覆盖原本的保存结果，下次保存会重新创建该文件。
        }
    }
}
