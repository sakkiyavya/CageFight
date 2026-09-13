# Addressables 构建并上传抖音云

打开 `Tools → 抖音对象存储 → 自动上传设置`，Python 默认使用 PATH 中的 `python`。
本机也可填写 `D:/Python313/python.exe`。依赖安装到项目 Library，不会进入游戏包：

```powershell
python -m pip install --target Library/DouyinUpload/python-packages tos==2.9.2
```

在抖音云对象存储密钥管理中获取 AK/SK，在窗口输入后点击“应用到当前会话”。
窗口不会保存密钥；Unity 重启后需要重新输入。也可由启动 Unity 的进程设置
`DOUYIN_TOS_AK` / `DOUYIN_TOS_SK` 环境变量。不要提交密钥或发送到聊天中。
点击配置检查只检查 Python、SDK 导入和凭证是否填写，不验证云端写权限。

1. 切换到 WebGL，执行 `Tools → 抖音对象存储 → 配置 dev 资源路径`。
2. 在 Addressables Groups 中选择 Profile `Douyin-TOS-dev`。
3. 点击 `Build → New Build → 抖音云 dev：构建并上传`。
4. 等待 Console 输出构建及上传完成。上传有错误时本次 Build 报错，本地构建文件仍保留。

固定目标为笼斗 dev 存储桶的 `addressables/WebGL/`；依赖已配置的公开读取目录。
只上传本次构建 Registry 中位于指定输出目录的文件，不扫描上传整个项目或旧构建目录。
先上传/校验全部 bundle，随后 Catalog，最后 Hash。不删除旧对象，不更改桶权限。
通过 SHA256 元数据判断是否可跳过上传；以前手动上传的包使用单次 PUT 的 MD5 ETag 比对。
如果同名 bundle 内容不一致则停止，需使用带内容 Hash 的 bundle 名称，避免破坏旧客户端。
每个包校验匿名 HEAD 状态及大小；Catalog/Hash 额外下载并校验 SHA256。

此选项专用于 New Build，不支持 Update a Previous Build。已发布版本的内容更新仍应按
Addressables 内容更新流程处理。不要在另一台机器同时向同一 dev 目录发布。
Catalog/Hash 是顺序上传，不是跨文件事务；失败后使用同一构建重试，旧资源包始终保留。
取消或超时不会回滚已经上传的对象。

选择这个构建器后 Unity 会记住活动构建器。如果 Player Build 配置会自动构建 Addressables，
打包小游戏时也会触发上传。只想构建本地内容时选回 Default Build Script。

SDK 官方依据：
https://developer.open-douyin.com/docs/resource/zh-CN/developer/tools/cloud/develop-guide/local-develop/douyin-cloud-features/tos/server-sdk
