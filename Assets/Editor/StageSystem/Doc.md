# 关卡系统 (Stage System) 技术文档

## 1. 系统概述
本系统是一套为抖音小游戏设计的**数据驱动 (Data-Driven)**、**无侵入式 (Non-intrusive)** 的关卡编辑与加载框架。
其核心目标是实现场景编辑与运行时逻辑的彻底解耦，通过将场景物体序列化为 ScriptableObject 配置文件，实现轻量化的关卡切换与动态加载。

---

## 2. 核心架构设计

### 2.1 数据层 (Model)
*   **LevelConfig (SO)**: 存储整个关卡的资源文件，包含一组 `LevelObjectData`。
*   **LevelObjectData**: 记录单个物体的“身份证”，包括：
    *   `instanceId`: 唯一实例 ID。
    *   `prefabKey`: 预制体索引（字符串，通常为预制体名称）。
    *   `TransformData`: 坐标、旋转、缩放。
    *   `List<ComponentData>`: 挂载在该物体上的自定义业务数据。
*   **ComponentData**: 抽象基类，所有需要保存的组件参数（如建筑血量、巡逻点等）都需继承此类。

### 2.2 核心接口 (Core)
*   **ILevelComponent**: 业务脚本必须实现的接口。
    *   `DataType`: 声明关联的数据类类型。
    *   `ExtractData()`: [编辑器用] 将脚本参数打包进 `ComponentData`。
    *   `ApplyData()`: [运行时用] 将 `ComponentData` 还原到脚本变量中。

### 2.3 绑定机制 (Binding)
*   **方案 B (Registry模式)**: 使用 `PrefabRegistry` 将 `prefabKey (string)` 映射到真实的 `GameObject` 资源。这保证了数据层的绝对纯净，方便未来进行前后端校验或跨平台迁移。

---

## 3. 编辑器功能与操作指南

### 3.1 物品标记 (Authoring)
*   **关卡物品 (Level Object)**: 选中场景中的预制体实例，在 Inspector 顶部勾选。只有勾选的物体才会被导出到关卡文件。
*   **常驻物品 (Permanent Object)**: 勾选后，该物体在“一键加载预览”时不会被清除（如主摄像机、UI根节点）。
*   *提示：支持多选批量操作。*
*   **己方大本营**：选中场景中的最外层预制体根对象，勾选 **设为己方大本营**，支持撤销和多选。构建会扫描已加载场景（包含未激活对象），根据位置与 `GameObjectProperty.occupySpace` 自动保存左下网格坐标；没有属性组件时按 1×1 计算。
*   每个关卡最多一个己方大本营标记。多个标记会阻止导出，弹窗列出场景及完整层级路径，自动选中这些对象，并在 Console 中输出可定位的错误；没有标记时关闭自动生成。
*   大本营及其子层级不会作为普通关卡物品导出或扫描资源，即使同时勾选了关卡物品。实际大本营由玩家所选种族提供。SO 中对应的开关和坐标为只读。

### 3.2 关卡导出 (Build)
*   点击菜单栏 **[关卡构建 -> 创建新关卡]**。
*   设置存储路径、关卡编号、关卡玩法类型（Defense / Attack）、防守时限（秒，非负）和关卡图标（Sprite），点击确认即可生成 `StageX.asset`。
*   `StageConfig` 的 Inspector 中，玩法类型、防守时限和图标仅供查看；需要修改时，在构建窗口设置后重新生成对应关卡。

### 3.3 资源绑定 (Binding)
*   点击菜单栏 **[关卡构建 -> 一键生成预制体注册表]**。
*   系统会自动扫描所有配置中用到的 Prefab 并生成映射表 `Assets/Resource/PrefabRegistry.asset`。

### 3.4 编辑器预览 (Preview)
*   选中生成的 `StageX.asset` 文件，点击 Inspector 里的 **“加载关卡”** 按钮。
*   系统会自动清理非垂直物品并还原配置中的所有内容。

### 3.5 资源 Key 自动扫描
*   新关卡导出和 `StageConfig` Inspector 的扫描按钮共用同一套收集逻辑。
*   扫描从所有 `StageObjectMarker` 物品开始，包含其子物体组件。
*   `[ResourceKey]` 支持 `string`、`string[]` 与 `List<string>`。
*   扫描器会继续进入组件序列化引用的 ScriptableObject、数组/List 与自定义可序列化对象，并自动去重、防止循环引用。
*   例如组件引用 `DialogueSeriesSO` 后，扫描器会沿系列中的 `DialogueConfigSO` 自动收集人物 Sprite Key。
*   配置资产必须能从关卡物品的序列化字段到达；扫描器不会全局收集与当前关卡无关的配置资产。

---

## 4. 运行时加载流程

1.  在初始场景中挂载 **LevelLoader** 组件。
2.  将生成的 `PrefabRegistry.asset` 拖入 `LevelLoader` 的引用位。
3.  通过代码调用：
    ```csharp
    levelLoader.LoadLevelAtRuntime(targetConfig);
    ```

---

## 5. 如何扩展新业务组件？

如果您需要让一个新的脚本（例如 `EnemyAI`）支持关卡保存，请遵循以下步骤：

1.  **定义数据类**:
    ```csharp
    [Serializable]
    public class EnemyAIData : ComponentData {
        public float moveSpeed;
        public int health;
    }
    ```
2.  **实现接口**: 让 `EnemyAI` 继承 `ILevelComponent` 并实现其方法，在 `ApplyData` 和 `ExtractData` 中完成变量的读写。
3.  **导出**: 重新执行关卡导出流程，新数据将自动包含在 `.asset` 文件中。
