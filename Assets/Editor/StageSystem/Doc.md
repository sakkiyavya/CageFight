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

### 3.2 关卡导出 (Build)
*   点击菜单栏 **[关卡构建 -> 创建新关卡]**。
*   设置存储路径、关卡编号，点击确认即可生成 `StageX.asset`。

### 3.3 资源绑定 (Binding)
*   点击菜单栏 **[关卡构建 -> 一键生成预制体注册表]**。
*   系统会自动扫描所有配置中用到的 Prefab 并生成映射表 `Assets/Resource/PrefabRegistry.asset`。

### 3.4 编辑器预览 (Preview)
*   选中生成的 `StageX.asset` 文件，点击 Inspector 里的 **“加载关卡”** 按钮。
*   系统会自动清理非垂直物品并还原配置中的所有内容。

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
