using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TTSDK;

/// <summary>
/// 私有对象存储存档。必须在 TT.InitSDK、TT.Login 成功后，使用账号系统提供的
/// 稳定玩家标识创建；不要传登录 code、昵称或设备随机 ID。云端 ACL 才是权限边界。
/// 切换账号前调用 Invalidate，再创建新实例。调用必须在 Unity 主线程进行。
/// 对象存储覆盖写没有跨设备 CAS 保证；超时/取消的上传可能已经提交，不能自动重试。
/// </summary>
public sealed class DouyinObjectSaveStorage : ISaveStorage
{
    public const string DevEnvironmentId = "env-sQC4XOSOGq";
    public const int MaxSaveBytes = 1024 * 1024;
    private readonly string environmentId;
    private readonly string playerDirectory;
    private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
    private bool invalidated;

    public DouyinObjectSaveStorage(string environmentId, string stablePlayerId)
    {
        if (string.IsNullOrWhiteSpace(environmentId)) throw new ArgumentException("缺少云环境 ID。");
        if (string.IsNullOrWhiteSpace(stablePlayerId)) throw new ArgumentException("缺少稳定玩家标识。");
        this.environmentId = environmentId;
        using (var sha = SHA256.Create())
            playerDirectory = "saves/" + BitConverter.ToString(sha.ComputeHash(
                Encoding.UTF8.GetBytes(stablePlayerId))).Replace("-", "").ToLowerInvariant() + "/";
    }

    public void Invalidate() { invalidated = true; }

    public async Task<SaveLoadResult> WriteAsync(string fileName, string json,
        CancellationToken cancellationToken = default)
    {
        if (!SaveFileNameUtility.TryValidate(fileName, out string error))
            return SaveLoadResult.Failed(SaveLoadStatus.InvalidFileName, error);
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaxSaveBytes)
            return SaveLoadResult.Failed(SaveLoadStatus.InvalidData, "存档为空或超过 1 MiB。");
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (invalidated) return Inactive();
            cancellationToken.ThrowIfCancellationRequested();
#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
            var files = TT.GetFileSystemManager();
            string localPath = TTFileSystemManager.USER_DATA_PATH + "/cage-save-" + Guid.NewGuid().ToString("N") + ".json";
            string writeError = files.WriteFileSync(localPath, json, "utf8");
            if (!string.IsNullOrEmpty(writeError))
                return SaveLoadResult.Failed(SaveLoadStatus.IoError, "写入上传临时文件失败。");
            var completion = new TaskCompletionSource<SaveLoadResult>();
            Action cleanup = () => { try { files.UnlinkSync(localPath); } catch { } };
            try
            {
                TT.CreateCloud().UploadFile(environmentId, ObjectPath(fileName), localPath, null,
                    response =>
                    {
                        cleanup();
                        completion.TrySetResult(SaveLoadResult.Succeeded());
                    },
                    response => { cleanup(); completion.TrySetResult(Failure(response.StatusCode)); });
            }
            catch { cleanup(); throw; }
            // 等待结束不等于网络请求已中止，临时文件由最终 SDK 回调清理。
            var result = await AwaitBounded(completion.Task, cancellationToken);
            if (invalidated) return Inactive();
            if (result.Status == SaveLoadStatus.Cancelled || result.Status == SaveLoadStatus.Unavailable)
                invalidated = true; // 禁止未确认的旧请求与新的覆盖写交错。
            return result;
#else
            return SaveLoadResult.Failed(SaveLoadStatus.Unavailable, "云存档需要抖音小游戏真机环境；Editor 使用本地后端。");
#endif
        }
        finally { gate.Release(); }
    }

    public async Task<SaveReadResult> ReadAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (!SaveFileNameUtility.TryValidate(fileName, out string error))
            return new SaveReadResult(SaveLoadResult.Failed(SaveLoadStatus.InvalidFileName, error));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (invalidated) return new SaveReadResult(Inactive());
            cancellationToken.ThrowIfCancellationRequested();
#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
            var files = TT.GetFileSystemManager();
            var completion = new TaskCompletionSource<SaveReadResult>();
            string downloadPath = TTFileSystemManager.USER_DATA_PATH + "/cage-read-" + Guid.NewGuid().ToString("N") + ".json";
            TT.CreateCloud().DownloadFile(environmentId, ObjectPath(fileName), downloadPath, null,
                response =>
                {
                    string path = response.FilePath;
                    try
                    {
                        byte[] bytes = files.ReadFileSync(path);
                        if (bytes == null || bytes.Length == 0 || bytes.Length > MaxSaveBytes)
                            completion.TrySetResult(new SaveReadResult(SaveLoadResult.Failed(SaveLoadStatus.InvalidData, "云存档为空、不可读取或超过大小限制。")));
                        else
                            completion.TrySetResult(new SaveReadResult(SaveLoadResult.Succeeded(), new UTF8Encoding(false, true).GetString(bytes)));
                    }
                    catch { completion.TrySetResult(new SaveReadResult(SaveLoadResult.Failed(SaveLoadStatus.InvalidData, "云存档解码失败。"))); }
                    finally { if (!string.IsNullOrEmpty(path)) { try { files.UnlinkSync(path); } catch { } } }
                },
                response => completion.TrySetResult(new SaveReadResult(Failure(response.StatusCode))));
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(75)))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token))
            {
                var delay = Task.Delay(Timeout.Infinite, linked.Token);
                var winner = await Task.WhenAny(completion.Task, delay);
                if (invalidated) return new SaveReadResult(Inactive());
                if (winner == completion.Task) { timeout.Cancel(); return await completion.Task; }
                return new SaveReadResult(WaitingFailed(cancellationToken));
            }
#else
            return new SaveReadResult(SaveLoadResult.Failed(SaveLoadStatus.Unavailable, "云存档需要抖音小游戏真机环境。"));
#endif
        }
        finally { gate.Release(); }
    }

    private string ObjectPath(string fileName) { return playerDirectory + Uri.EscapeDataString(fileName) + ".json"; }
    private static SaveLoadResult Inactive() { return SaveLoadResult.Failed(SaveLoadStatus.Unauthorized, "存档会话已失效，需重新确认账号和云端存档状态。"); }
    private static SaveLoadResult Failure(int status)
    {
        var kind = status == 404 ? SaveLoadStatus.NotFound : status == 401 || status == 403
            ? SaveLoadStatus.Unauthorized : SaveLoadStatus.Unavailable;
        return SaveLoadResult.Failed(kind, "对象存储请求失败，状态码：" + status);
    }
    private static SaveLoadResult WaitingFailed(CancellationToken token)
    {
        return SaveLoadResult.Failed(token.IsCancellationRequested ? SaveLoadStatus.Cancelled : SaveLoadStatus.Unavailable,
            "等待已取消或超时；上传提交状态可能不确定，请确认云端结果后再保存。");
    }
    private static async Task<SaveLoadResult> AwaitBounded(Task<SaveLoadResult> operation, CancellationToken token)
    {
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(75)))
        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token))
        {
            var winner = await Task.WhenAny(operation, Task.Delay(Timeout.Infinite, linked.Token));
            if (winner == operation) { timeout.Cancel(); return await operation; }
            return WaitingFailed(token);
        }
    }
}
