using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 逻辑文件名、原始 JSON 与存储后端之间的协调层。
/// 由 SaveLoadManager 调用；业务模块不接触本地完整路径或未来的云端用户路径。
/// </summary>
public static class SaveLoadSystem
{
    private static ISaveStorage _storage = new LocalJsonSaveStorage();

    public static string DefaultLocalSaveDirectoryPath =>
        LocalJsonSaveStorage.DefaultDirectoryPath;

    /// <summary>
    /// 替换后续请求使用的存储后端。
    /// 已经开始的请求会继续使用其捕获的旧后端，新的请求使用新后端。
    /// </summary>
    public static void ConfigureStorage(ISaveStorage storage)
    {
        if (storage == null)
        {
            throw new ArgumentNullException(nameof(storage));
        }

        Interlocked.Exchange(ref _storage, storage);
    }

    /// <summary>
    /// 恢复当前测试阶段使用的默认本地目录。
    /// </summary>
    public static void UseDefaultLocalStorage()
    {
        ConfigureStorage(new LocalJsonSaveStorage());
    }

    /// <summary>
    /// 将原始 JSON 保存到指定逻辑文件名。
    /// 文件扩展名及实际地址由存储后端负责。
    /// </summary>
    public static async Task<SaveLoadResult> SaveAsync(
        string fileName,
        string json,
        CancellationToken cancellationToken = default)
    {
        if (!SaveFileNameUtility.TryValidate(fileName, out string fileNameError))
        {
            return SaveLoadResult.Failed(
                SaveLoadStatus.InvalidFileName,
                fileNameError);
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return SaveLoadResult.Failed(
                SaveLoadStatus.InvalidData,
                "不能写入空的 JSON 存档。");
        }

        ISaveStorage storage = Volatile.Read(ref _storage);

        try
        {
            return await storage.WriteAsync(fileName, json, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return CreateCancellationResult(cancellationToken, "保存");
        }
        catch (Exception exception)
        {
            return SaveLoadResult.Failed(
                SaveLoadStatus.UnknownError,
                $"存储后端保存失败：{exception.Message}");
        }
    }

    /// <summary>
    /// 读取指定逻辑文件名对应的原始 JSON。
    /// </summary>
    public static async Task<SaveReadResult> LoadAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (!SaveFileNameUtility.TryValidate(fileName, out string fileNameError))
        {
            return new SaveReadResult(SaveLoadResult.Failed(
                SaveLoadStatus.InvalidFileName,
                fileNameError));
        }

        ISaveStorage storage = Volatile.Read(ref _storage);

        try
        {
            SaveReadResult result = await storage.ReadAsync(fileName, cancellationToken);
            if (result.IsSuccess && string.IsNullOrWhiteSpace(result.Json))
            {
                return new SaveReadResult(SaveLoadResult.Failed(
                    SaveLoadStatus.InvalidData,
                    "存储后端返回了空的 JSON。"));
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            return new SaveReadResult(
                CreateCancellationResult(cancellationToken, "读取"));
        }
        catch (Exception exception)
        {
            return new SaveReadResult(SaveLoadResult.Failed(
                SaveLoadStatus.UnknownError,
                $"存储后端读取失败：{exception.Message}"));
        }
    }

    private static SaveLoadResult CreateCancellationResult(
        CancellationToken cancellationToken,
        string operationName)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return SaveLoadResult.Failed(
                SaveLoadStatus.Cancelled,
                $"{operationName}操作已取消。");
        }

        return SaveLoadResult.Failed(
            SaveLoadStatus.Unavailable,
            $"{operationName}操作被存储后端中止或超时，请稍后重试。");
    }
}
