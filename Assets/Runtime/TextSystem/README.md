# Dialogue System

## 运行时结构

- `DialogueConfigSO`：一条对话，保存人物 Sprite Key 与 `[TextArea] string` 正文。
- `DialogueSeriesSO`：按顺序直接引用多条 `DialogueConfigSO`。
- `DialogueView`：直接位于 Canvas 下的框架组件，只负责内容、射线拦截和不受暂停影响的协程动画。
- `DialogueManager`：由上层框架托管的调度组件，负责 latest-wins、系列推进、暂停及输入控制。

`DialogueManager` 和 `DialogueView` 都不是自行创建或跨场景持久化的独立对象。它们的宿主、父子层级、Canvas 顺序和销毁时机由上层框架负责；DialogueView 自身的激活状态由 DialogueManager 根据播放状态管理。

## 框架接入

1. 将 `DialogueView` 挂在上层框架 Canvas 的直接子节点，并把该组件拖给 `DialogueManager` 的 `Dialogue View` 字段。这里引用的是框架内组件实例，不是 Prefab 资源资产。
2. DialogueView 自身的 `RectTransform` 就是对话框根节点：进退场动画会直接修改它的 `anchoredPosition`，不需要 `Animated Root`、`CanvasGroup` 或额外包装层。它在不绘制图像的同时作为射线拦截区域，所以 RectTransform 大小应覆盖需要拦截点击的范围。
3. 人物 `Image` 与正文 `TMP_Text` 可以直接放在 DialogueView 下，由你在 Inspector 手动赋值；脚本不会自动查找它们。所属 Canvas 仍需由上层框架配置 `GraphicRaycaster`。
4. DialogueView 在 `OnEnable` 时会检查 DialogueManager：Manager 未初始化、View 不属于该 Manager，或当前没有正在准备/播放/退场的对话时，View 会立即停用自己。Manager 会在接受首条请求时激活它，在最后一条完整退场或请求失败后停用它。
5. Manager 不会实例化、销毁或重挂 DialogueView 的父级。框架停用或销毁 Manager 时，Manager 会终止对话、停用 View、清空 Sprite 引用并恢复 `Time.timeScale` 和 `UIStack`。

不要把对话框加入 `UIStack`。对话显示期间，Manager 会临时停止 `UIStack` 的原始输入轮询，并用全屏 View 拦截底层 UI 与游戏点击。

## 资源约定

人物立绘通过 `Sprite Key` 从 `ResourceManager` 的当前关卡预加载缓存获取。非空的对话人物 Key 必须由关卡加载逻辑加入对应的 `StageConfig.sprites`；Key 留空时不加载资源，`Image` 使用 Unity UI 内建白色纹理。

> StageConfig 的“扫描场景资源 Key”和新关卡导出会递归进入关卡物品组件引用的 `DialogueSeriesSO` / `DialogueConfigSO`，并自动把人物 Key 加入 `StageConfig.sprites`。对应对话资产必须能从某个 `StageObjectMarker` 物品的序列化字段到达；扫描器不会把项目里的全部对话资产无差别加入每个关卡。

公开入口为 `Show`、`Hide`、`PlaySeries`、`Advance` 和 `CancelSeries`。框架未启用 Manager 时，请求会被忽略。
