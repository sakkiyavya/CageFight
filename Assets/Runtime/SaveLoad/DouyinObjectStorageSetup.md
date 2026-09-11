# 抖音对象存储接入状态

环境：笼斗 dev，`env-sQC4XOSOGq`。未配置 prod，未启用 CDN。

桶：`tt572c2191c956e13607-env-sqc4xosogq`。

## 云端目录

- `addressables/<BuildTarget>/`：用户已明确授权公开读，已在控制台保存并验证；抖音客户端 SDK 上传显示“不支持”。不要把存档放到此目录。
- `saves/<SHA256(stablePlayerId)>/<fileName>.json`：私有存档。控制台“仅创建者可读写”已保存。必须用两个真实玩家账号验证不能跨账号读写。
- 不生成、不向客户端放入 AK/SK。存档访问使用 TTSDK 的登录态。

## Addressables

菜单 `Tools/抖音对象存储/配置 dev 资源路径` 创建/选择 `Douyin-TOS-dev` Profile，设置：

```
Remote.BuildPath = ServerData/Douyin-dev/[BuildTarget]
Remote.LoadPath = https://tt572c2191c956e13607-env-sqc4xosogq.tos-cn-beijing.volces.com/addressables/[BuildTarget]
```

将名字以 `Remote ` 开头的组切换为 Remote 路径，其他组不变，启用远程 Catalog。
本次尚未执行该菜单，因此项目已有 Addressables 配置未改变。

在 WebGL 平台运行 `Tools/抖音对象存储/构建 WebGL 远程资源`，上传输出目录内容至 `addressables/WebGL/`。
先上传 Bundle，再发布 Catalog/Hash；保留旧版本仍需使用的 Bundle。
`addressables_content_state.bin` 留在构建归档中，不作为客户端下载资源上传。

发布前需要验证默认域名 HTTPS 下载、抖音下载域名配置、目录权限和真机加载。
公开目录配置已验证保存，实际文件下载尚未验证，因此不要将此 Profile 用于正式上线。

## 存档后端

`DouyinObjectSaveStorage` 实现 `ISaveStorage`，仅在抖音运行环境调用 SDK。
在 TT.InitSDK、TT.Login 成功且账号系统已提供稳定标识后：

```csharp
var storage = new DouyinObjectSaveStorage(
    DouyinObjectSaveStorage.DevEnvironmentId, stablePlayerId);
SaveLoadSystem.ConfigureStorage(storage);
```

当前 TT.Login 回调只返回临时 code、anonymousCode、isLogin，没有稳定 openid。
目前项目尚未提供稳定账号标识获取流程，因此本实现未自动启用，仍使用原有本地存档。
不能使用临时 code、昵称、设备 ID 或随机本地 ID 冒充跨设备账号 ID。
玩家标识只是寻址信息；云端创建者 ACL 负责鉴权。

切换账号之前停止发起存档请求并调用 `storage.Invalidate()`；不要将 A 账号的本地 JSON 自动迁移至 B 账号。
API 应从 Unity 主线程调用，每个账号会话仅复用一个后端实例。
SDK 成功/失败回调决定结果；平台自定义错误码尚须真机记录核对，无法识别的错误统一视为不可用。

限制：单份 JSON 最大 1 MiB；同一实例串行；没有跨设备条件更新/CAS，也没有防作弊。
取消/超时不等于撤销上传：网络请求可能已提交，此时实例失效，必须确认结果再继续；不自动重试覆盖写。

## 验证

本地 WebGL 分支及 Editor 构建工具用 Unity 自带 C# 编译器检查。
Unity 批处理完整验证尝试卡在许可证客户端连接阶段，尚未完成资源构建。
必须补做：账号初始化、首次保存、覆盖、重启/换设备读取、账号隔离、弱网超时、资源下载和加载。
