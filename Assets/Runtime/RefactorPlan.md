# GameObjectProperty 解耦重构计划

本计划旨在将系统中硬编码的资源引用（如 GameObject）重构为纯数据驱动的字符串 Key，并通过自定义特性（Attribute）实现更智能的编辑器交互与自动化的 Addressables 资源加载。

## ⚠️ User Review Required

本计划已按您的要求大幅精简，去除了所有实现代码，仅保留了功能架构设计。新增了**资源标记特性（Attribute）**以及**加载前置热更检查管线**，请您确认该功能范围是否完全符合您的预期。

## 🛠️ Proposed Changes

### 1. 资源类型标记特性支持 (新增)
**涉及文件**：
*   `[NEW] Runtime\StageSystem\Model\ResourceKeyAttribute.cs`

**修改功能**：
*   新增一个自定义 C# 特性（Attribute），用于修饰脚本中的 `string` 字段，以声明该字符串实际代表的资源类型（如 `GameObject`, `AudioClip`, `Texture2D` 等）。
*   **运行时收益**：使资源管理器（`ResourceManager`）在深度扫描 `LevelConfig` 时，能够通过反射精准过滤，只抓取带有该特性的字符串作为“需要使用 Addressables 加载的 Key”，避免与普通的纯文本字符串混淆。
*   **编辑器收益**：供 Inspector 工具链读取，限定拖入槽位的资源必须符合声明的类型。

### 2. 数据与组件模型重构
**涉及文件**：
*   `[MODIFY] Runtime\StageSystem\Model\GameObjectPropertyData.cs`
*   `[MODIFY] Runtime\Gameplay\GameObjectProperty.cs`

**修改功能**：
*   将原有的物理引用字段（如 `atkObj` 和 `buildAnime`）彻底更改为 `string` 类型的 Key。
*   在这些字符串字段上添加新的 `ResourceKeyAttribute`，标记它们对应的目标类型为 `GameObject`。
*   更新 `ILevelComponent` 的序列化/反序列化接口逻辑，确保基于字符串的数据同步正常流转。

### 3. 实机行为与业务逻辑重构
**涉及文件**：
*   `[MODIFY] Runtime\Gameplay\Character\CharacterAI.cs`
*   `[MODIFY] Runtime\Gameplay\Character\CommonFunc\Attack.cs`
*   `[MODIFY] Runtime\Gameplay\Building\BuildingBase.cs`

**修改功能**：
*   剔除旧版直接通过物理预制体实例化的代码。
*   全面重构为：在发射子弹或播放建筑动画前，统一调用 `ResourceManager`，传入对应的字符串 Key 获取在内存中缓存好的资源对象，进而交付给对象池或实例化指令。

### 4. 资源管理器智能加载与热更强化
**涉及文件**：
*   `[MODIFY] Runtime\ResourcesSystem\ResourceManager.cs`

**修改功能**：
*   **前置 Catalog 比对与 AB 更新**：在执行 `LoadStageResources` 实际加载资产之前，先请求并比对远程的 Catalog 目录。系统将根据比对结果，自动发现是否有需要更新的 AB 包，并在加载前完成这些 AB 包的热更新下载。
*   **基于特性的智能扫描收集**：不再局限于手动写死的 `prefabKey`，而是通过反射遍历 `LevelConfig` 中的所有数据，自动收集所有标记了 `ResourceKeyAttribute` 的字符串键值，汇聚后通过 Addressables 执行统一的依赖合并预下载。

### 5. 编辑器工作流定制
**涉及文件**：
*   `[NEW] Editor\Gameplay\GameObjectPropertyEditor.cs` 或通用 PropertyDrawer

**修改功能**：
*   为打上 `ResourceKeyAttribute` 的字符串定制 Inspector 绘制层。
*   在保留底层纯字符串存储的前提下，在编辑器面板上提供与原来一样的拖拽槽位。
*   拦截并处理拖拽事件：验证拖入的资产类型是否与 Attribute 限定一致，验证通过后自动提取资源的名称 (Key) 并填入底层的字符串字段。
