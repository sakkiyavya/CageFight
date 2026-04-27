# 关卡编辑器与运行时加载系统架构设计

本方案为“关卡编辑器 + 运行时加载系统”量身定制，严格遵循“编辑器与运行时完全隔离”、“数据驱动”以及“无场景依赖”的核心原则，适配抖音小游戏等对性能和包体敏感的环境。

---

## 一、 总体架构分层

系统采用严格的**单向依赖**与**分层隔离**架构，确保运行时环境的纯净与轻量。

1. **Data Layer（数据层）**
   - **定位**：系统的绝对核心与唯一契约，表现为 ScriptableObject（SO）及纯 C# 数据结构（POCO）。
   - **规则**：不包含任何逻辑代码，被 Editor 层和 Runtime 层共同依赖。
2. **Editor Layer（编辑器层）**
   - **定位**：负责数据生产。运行在 Unity Editor 环境下。
   - **规则**：依赖 Data Layer。允许使用所有 `UnityEditor` API，绝对禁止将代码编译进最终包体（必须放在 Editor 目录下）。
3. **Build Layer（构建层 - 管线化）**
   - **定位**：连接生产与运行的桥梁。在出包前（Pre-build）自动执行。
   - **规则**：负责数据校验、冗余数据剔除、以及运行时资源的映射表（Registry）生成。
4. **Runtime Layer（运行时层）**
   - **定位**：负责数据消费。在小游戏实机环境中运行。
   - **规则**：依赖 Data Layer。**严禁**依赖任何 Scene 预设内容，**严禁**包含任何 `UnityEditor` 命名空间的代码。所有游戏对象必须通过数据动态生成。

---

## 二、 数据结构设计（Data Layer）

数据结构的设计决定了系统的扩展性。采用 `[SerializeReference]` 实现多态序列化，支持组件化扩展。

### 核心数据结构：
*   **LevelConfig (ScriptableObject)**
    *   `int levelId`：关卡唯一标识。
    *   `LevelSettings settings`：关卡全局配置（如背景音乐、重力、时间限制）。
    *   `List<LevelObjectData> objects`：关卡内所有物体的集合。
*   **LevelObjectData (POCO)**
    *   `int instanceId`：物体实例唯一 ID（运行时寻址用）。
    *   `string prefabKey`：**核心**。映射具体资源，绝对不能用 GUID 或 AssetPath。
    *   `TransformData transform`：空间数据。
    *   `List<ComponentData> components`：附加组件数据列表。
*   **TransformData (POCO)**
    *   `Vector3 position`, `Vector3 rotation`, `Vector3 scale`。
*   **ComponentData (Abstract Class / Interface)**
    *   这是数据扩展的基础。例如：
        *   `class SpawnerData : ComponentData { public int spawnCount; }`
        *   `class PatrolData : ComponentData { public Vector3[] waypoints; }`

---

## 三、 编辑器系统设计（Editor Layer）

为了保持最大限度的简洁与非侵入性，系统**摒弃了挂载额外 Authoring 脚本**的传统方式，采用 Inspector 原生扩展与独立的构建窗口。

1. **无侵入式标记机制 (Inspector 扩展)**
   - **无需额外挂载任何组件**。通过扩展 Editor 底层（如 `Editor.finishedDefaultHeaderGUI`），在场景中选中任何 GameObject 时，其 Inspector 面板顶部会多出一个名为“**关卡物品**”的复选框。
   - 只有同时满足以下两个条件的物体，才会被系统认为是合法的关卡元素：
     1. 该复选框被勾选。
     2. 该物体必须是一个 Prefab 的实例（确保在运行时可以通过 `prefabKey` 追溯并生成）。
2. **关卡构建工具窗口 (Level Builder Window)**
   - 在顶部菜单栏新增功能入口：**[关卡构建 -> 创建新关卡]**。
   - 点击后弹出一个简洁的 Editor 窗口，包含三个核心部分：
     1. **保存路径**：指定生成的 `LevelConfig` SO 文件存放的具体目录（例如 `Assets/Config/Levels/`）。
     2. **关卡编号 (Level ID)**：`uint` 类型，用于标识这是第几关。
     3. **确认按钮**：点击后触发采集与保存管线。
3. **Scene → 数据（采集与保存管线）**
   - 点击确认后，工具会遍历当前场景中的所有根节点。
   - 筛选出所有勾选了“关卡物品”且是 Prefab 的对象。
   - 提取它们的 Transform 数据，并利用反射或其他机制提取它们身上已有组件的参数，封装为 `ComponentData`。
   - 最终根据填写的路径和关卡编号，生成或覆盖对应的 `LevelConfig` ScriptableObject。
4. **Scene 清理机制**
   - 同样提供从配置加载场景预览的功能。在加载前，会自动销毁场景中所有被标记为“关卡物品”的对象，确保预览环境纯净。

---

## 四、 构建阶段设计（Build Layer）

小游戏对包体和内存极度敏感，构建层是优化的关键。

1. **Prefab Registry 自动生成**
   - **痛点**：运行时拿到了 `prefabKey`（字符串），如何变成 GameObject？
   - **方案**：构建脚本扫描所有 `LevelConfig`，收集所有用到的 `prefabKey`。生成一个 `PrefabRegistry` (SO)，内部包含字典/列表映射：`Key (string) -> AssetReference / GameObject`（配合 Addressables 或 Resources 使用）。
2. **数据校验 (Validation)**
   - 检查 `LevelConfig` 中是否存在空引用、重复的 `instanceId`、未注册的 `prefabKey`，如有错误则报错并阻断出包流程。
3. **数据裁剪 (Stripping)**
   - 剥离仅在编辑器中使用的辅助数据（如节点备注名、编辑器下用于显示 Gizmos 的颜色配置），减小 SO 文件体积。

---

## 五、 运行时系统设计（Runtime Layer）

运行时必须像流水线一样高效地将数据转化为游戏实体。

1. **LevelLoader（关卡加载器）**
   - 接收指令 `LoadLevel(int levelId)`。
   - 异步加载对应的 `LevelConfig` SO。
   - 提取所有不重复的 `prefabKey`，通过 `PrefabRegistry` 批量预加载资源（AssetBundle / Addressables），避免实例化时卡顿。
2. **ObjectFactory（对象工厂）**
   - 资源就绪后，遍历 `LevelConfig.objects`。
   - 通过 `PrefabRegistry` 获取实际 Prefab 并调用 `Instantiate`。
   - 赋予生成的物体相应的 `instanceId`。
3. **ComponentBinder（组件绑定器）**
   - 针对实例化的 GameObject，应用 `TransformData`。
   - 遍历 `List<ComponentData>`，根据数据类型获取或添加对应的 Runtime MonoBehaviour（例如遇到 `PatrolData` 则给物体添加 `PatrolLogic` 组件），并将数据注入进去：`patrolLogic.Init(patrolData)`。
4. **生命周期管理**
   - `Load`：加载 SO 与资源。
   - `Init`：实例化对象，完成 Component 绑定与数据注入。
   - `Start`：所有对象就绪，触发关卡开始事件。
   - `Unload`：关卡结束，销毁所有工厂生成的 GameObject，释放资源。

---

## 六、 关键设计决策说明

1. **为什么使用 prefabKey 而非 AssetPath / GUID？**
   - GUID 强依赖 Unity 编辑器的 meta 文件，AssetPath 在资源移动时会失效。
   - `prefabKey` 是一种稳定的“逻辑契约”。只要 Key 不变，美术可以随意重构目录或替换资产模型，甚至在不同平台（如 iOS 和 WebGL）映射到不同的低/高质量资源，实现代码与资源的完全解耦。
2. **为什么必须完全隔离代码（不能共享逻辑）？**
   - 将 MonoBehaviour 混合用于编辑器数据收集和运行时逻辑，会导致类极其臃肿且包含无用的编辑器引用。
   - 强制隔离后，运行时的类只有纯粹的游戏逻辑，无任何序列化冗余，这对抖音小游戏的 JavaScript 转换（如 WebGL/WASM）以及内存控制有着决定性的性能优势。
3. **如何保证扩展性（组件系统）？**
   - 使用 `ComponentData` 列表而非在 `LevelObjectData` 里平铺字段。如果未来需要新增一种机制，只需新增对应的数据类和逻辑脚本即可，无需修改核心的序列化结构，符合开闭原则。
4. **如何适配小游戏环境？**
   - **零场景切换**：整个游戏只有一个空的主 Scene。关卡加载全靠 Instantiate，避免了 `SceneManager.LoadScene` 带来的巨大耗时和内存峰值。
   - **预加载友好**：数据驱动使得 Loader 可以在关卡开始前，精准知道需要加载哪些资源，完美契合 Addressables 异步加载机制。

---

## 七、 工作流设计

在这个架构下，各团队的工作流是高度并行且互不干扰的：

1. **研发工作流 (程序)**
   - 定义新的 `ComponentData` 结构。
   - 编写 `RuntimeLogic` 脚本（用于读取数据执行逻辑），并挂载到 Prefab 上。
   - 编写 Editor 下的参数提取逻辑（如果该组件需要被记录）。
2. **关卡编辑工作流 (策划/关卡设计)**
   - 打开专门的 LevelEditor 场景（纯净画布）。
   - 从库中拖拽预先做好的 Prefab 到场景中搭建关卡。
   - 选中需要记录的物体，在 Inspector 中勾选**“关卡物品”**复选框。
   - 点击菜单栏 **[关卡构建 -> 创建新关卡]**，在弹出的窗口中填好路径和关数，点击确认生成配置。
3. **测试预览工作流**
   - 编辑器内清空场景，提供入口从生成的 Config 中还原场景以供检查。
   - 点击 Play 按钮，系统自动进入 Runtime 模式，使用 LevelLoader 加载配置进行实机试玩。
