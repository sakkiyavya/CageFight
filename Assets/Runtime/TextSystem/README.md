# Dialogue System

## 运行时结构

- `DialogueConfigSO`：一条对话，保存人物 Sprite Key 与 `[TextArea] string` 正文。
- `DialogueSeriesSO`：按顺序直接引用多条 `DialogueConfigSO`。
- `DialogueView`：直接位于 Canvas 下的框架组件，只负责内容、射线拦截和不受暂停影响的协程动画。
- `DialogueManager`：由上层框架托管的调度组件，负责 latest-wins、系列推进、暂停及输入控制。

`DialogueManager` 和 `DialogueView` 都不是自行创建或跨场景持久化的独立对象。它们的宿主、父子层级、激活状态、Canvas 顺序和销毁时机全部由上层框架负责。

## 框架接入

1. 将 `DialogueView` 挂在上层框架 Canvas 的直接子节点，并把该组件拖给 `DialogueManager` 的 `Dialogue View` 字段。这里引用的是框架内组件实例，不是 Prefab 资源资产。
2. DialogueView 自身的 `RectTransform` 应铺满 Canvas。它继承了一个不绘制图像的 UI Graphic，用于全屏射线拦截，因此不需要额外的 `CanvasGroup`、透明 `Image` 或 `Input Blocker`。
3. 真正显示和进行位移动画的内容对象必须是 View 的子节点，并赋给 `Animated Root`。隐藏时脚本只会停用这个内容子节点，不会切换 Canvas 或 DialogueView 根对象。
4. 人物 `Image` 与正文 `TMP_Text` 由你在 Inspector 手动赋值，脚本不会自动查找或强制添加它们。所属 Canvas 仍需由上层框架配置 `GraphicRaycaster`。
5. 上层框架必须保证 DialogueView 根对象在 Manager 启用期间保持激活。Manager 不会实例化、销毁、重挂父级或切换它的 GameObject。
6. 框架停用或销毁 Manager 时，Manager 会终止对话、清空 Sprite 引用、恢复 `Time.timeScale` 和 `UIStack`，但不会改动框架对象的所有权。

不要把对话框加入 `UIStack`。对话显示期间，Manager 会临时停止 `UIStack` 的原始输入轮询，并用全屏 View 拦截底层 UI 与游戏点击。

## 资源约定

人物立绘通过 `Sprite Key` 从 `ResourceManager` 的当前关卡预加载缓存获取。所有对话人物 Key 必须由关卡加载逻辑加入对应的 `StageConfig.sprites`。

> StageConfig 的“扫描场景资源 Key”和新关卡导出会递归进入关卡物品组件引用的 `DialogueSeriesSO` / `DialogueConfigSO`，并自动把人物 Key 加入 `StageConfig.sprites`。对应对话资产必须能从某个 `StageObjectMarker` 物品的序列化字段到达；扫描器不会把项目里的全部对话资产无差别加入每个关卡。

公开入口为 `Show`、`Hide`、`PlaySeries`、`Advance` 和 `CancelSeries`。框架未启用 Manager 时，请求会被忽略。
