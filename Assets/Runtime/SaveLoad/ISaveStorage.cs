using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 存档操作的结果类型。
/// 云存储实现可以继续复用未授权、服务不可用等状态。
/// </summary>
public enum SaveLoadStatus
{
    // 让 default(SaveLoadResult) 表示未知错误，避免未初始化结果被误判为成功。
    UnknownError = 0,
    Success,
    NotFound,
    InvalidFileName,
    InvalidData,
    IoError,
    Unauthorized,
    Unavailable,
    Cancelled
}

/// <summary>
/// 一次存档操作的结果。
/// </summary>
public readonly struct SaveLoadResult
{
    public SaveLoadStatus Status { get; }
    public string Message { get; }
    public bool IsSuccess => Status == SaveLoadStatus.Success;

    public SaveLoadResult(SaveLoadStatus status, string message = null)
    {
        Status = status;
        Message = message ?? string.Empty;
    }

    public static SaveLoadResult Succeeded(string message = null)
    {
        return new SaveLoadResult(SaveLoadStatus.Success, message);
    }

    public static SaveLoadResult Failed(SaveLoadStatus status, string message)
    {
        return new SaveLoadResult(status, message);
    }
}

/// <summary>
/// 读取存档的结果。存储层只返回原始 JSON，不解析游戏数据。
/// </summary>
public readonly struct SaveReadResult
{
    public SaveLoadResult Result { get; }
    public string Json { get; }
    public SaveLoadStatus Status => Result.Status;
    public string Message => Result.Message;
    public bool IsSuccess => Result.IsSuccess;

    public SaveReadResult(SaveLoadResult result, string json = null)
    {
        Result = result;
        Json = json;
    }
}

/// <summary>
/// 所有存储后端共享的逻辑文件名规则。
/// </summary>
public static class SaveFileNameUtility
{
    public const string JsonFileExtension = ".json";
    public const int MaxFileNameLength = 128;

    private static readonly char[] InvalidFileNameCharacters =
        Path.GetInvalidFileNameChars();

    /// <summary>
    /// 校验不带路径及扩展名的逻辑文件名。
    /// </summary>
    public static bool TryValidate(string fileName, out string error)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            error = "存档文件名不能为空。";
            return false;
        }

        if (!string.Equals(fileName, fileName.Trim(), StringComparison.Ordinal))
        {
            error = "存档文件名不能包含首尾空白字符。";
            return false;
        }

        if (fileName.Length > MaxFileNameLength)
        {
            error = $"存档文件名不能超过 {MaxFileNameLength} 个字符。";
            return false;
        }

        if (fileName.EndsWith(JsonFileExtension, StringComparison.OrdinalIgnoreCase))
        {
            error = "存档文件名不需要携带 .json 扩展名。";
            return false;
        }

        if (fileName == "." ||
            fileName == ".." ||
            fileName.EndsWith(".", StringComparison.Ordinal) ||
            fileName.EndsWith(" ", StringComparison.Ordinal))
        {
            error = $"存档文件名 '{fileName}' 无效。";
            return false;
        }

        foreach (char character in fileName)
        {
            if (char.IsControl(character) ||
                "<>:\"/\\|?*".IndexOf(character) >= 0 ||
                Array.IndexOf(InvalidFileNameCharacters, character) >= 0)
            {
                error = $"存档文件名 '{fileName}' 包含非法字符。";
                return false;
            }
        }

        if (IsReservedWindowsFileName(fileName))
        {
            error = $"存档文件名 '{fileName}' 是系统保留名称。";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsReservedWindowsFileName(string fileName)
    {
        string nameWithoutAdditionalExtensions = fileName.Split('.')[0];
        switch (nameWithoutAdditionalExtensions.ToUpperInvariant())
        {
            case "CON":
            case "PRN":
            case "AUX":
            case "NUL":
            case "COM1":
            case "COM2":
            case "COM3":
            case "COM4":
            case "COM5":
            case "COM6":
            case "COM7":
            case "COM8":
            case "COM9":
            case "LPT1":
            case "LPT2":
            case "LPT3":
            case "LPT4":
            case "LPT5":
            case "LPT6":
            case "LPT7":
            case "LPT8":
            case "LPT9":
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// 原始 JSON 的存储接口。
/// 当前由本地文件实现；迁移抖音云时替换此接口的实现即可。
/// fileName 是不带扩展名的逻辑文件名，存储实现负责转换为实际地址或云端键。
/// 存储实现不得在自身读写过程中反向调用 SaveLoadSystem，以免递归请求。
/// </summary>
public interface ISaveStorage
{
    Task<SaveLoadResult> WriteAsync(
        string fileName,
        string json,
        CancellationToken cancellationToken = default);

    Task<SaveReadResult> ReadAsync(
        string fileName,
        CancellationToken cancellationToken = default);
}
