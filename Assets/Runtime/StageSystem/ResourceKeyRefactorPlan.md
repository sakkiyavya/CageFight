# 资源 Key 系统重构计划书

## 背景与目标

当前 `ResourceManager` 通过**运行时反射递归扫描** `LevelConfig` 来收集资源 Key，存在以下问题：
- **运行时性能开销**：每次关卡加载都要反射遍历整个对象树。
- **隐式依赖**：资源依赖散落在各组件数据中，无法直接审查。

**目标**：将资源 Key 收集工作移至**编辑器导出阶段**，持久化到 `LevelConfig`，运行时仅做简单读取。

---

## 涉及文件

| 文件 | 变更类型 |
|------|----------|
| `Runtime/StageSystem/Model/LevelConfig.cs` | **修改** — 新增 `resourceKeys` 字段 |
| `Runtime/ResourcesSystem/ResourceManager.cs` | **修改** — 移除反射扫描，改为读取清单并查 Registry |
| `Editor/StageSystem/LevelExporter.cs` | **修改** — 新增场景扫描并写入 `resourceKeys` |

---

## 第一步：扩展 `LevelConfig`

在 `LevelConfig` 类中新增一个字段：

```csharp
[Tooltip("该关卡所有需要预加载的资源 Key 清单（由 LevelExporter 导出时自动生成）")]
public List<string> resourceKeys = new List<string>();
```

**说明**：
- 不需要单独存储类型，因为项目约定**资源名全局唯一**，运行时可通过逐一查询各 Registry 确定类型。
- 字段**暴露给 Inspector**，方便导出后直接检查清单内容是否正确。

---

## 第二步：重构 `ResourceManager`

### 2.1 移除
- 完整移除私有方法 `ScanForResourceKeys`。

### 2.2 修改 `CoLoadStageResources`

将反射扫描替换为读取 `resourceKeys`，并通过查询各 Registry 确定资源类型：

```csharp
// 防御性检查
if (level.resourceKeys == null || level.resourceKeys.Count == 0)
{
    Debug.LogWarning("[ResourceManager] LevelConfig.resourceKeys 为空！请重新通过 LevelExporter 导出关卡。");
}

// 遍历 Key 清单，查询各 Registry 确定类型并加载
foreach (string key in level.resourceKeys)
{
    if (string.IsNullOrEmpty(key)) continue;

    // 按优先级查询 Registry，命中即加载
    if (prefabRegistry?.GetReference(key) is { } prefabRef && prefabRef.RuntimeKeyIsValid())
    {
        // 加载 GameObject，逻辑同原有流程
    }
    else if (audioRegistry?.GetReference(key) != null)
    {
        // 加载 AudioClip
    }
    else if (textureRegistry?.GetReference(key) != null)
    {
        // 加载 Texture2D
    }
    // ... 其余类型同理
    else
    {
        Debug.LogWarning($"[ResourceManager] Key '{key}' 在所有 Registry 中均未找到，已跳过。");
    }
}
```

> **注意**：`ResourceManager` 需要在 Inspector 中引用所有 Registry（`prefabRegistry`、`audioRegistry`、`textureRegistry` 等），与现有 `prefabRegistry` 字段同级扩展即可。

---

## 第三步：扩展 `LevelExporter`

### 3.1 新增 `CollectResourceKeys`

扫描**单个场景 GameObject** 的所有直接 Component，收集带有 `[ResourceKey]` 标注的字段值：

```
输入：GameObject（场景实例）、HashSet<string> visitedKeys（全局去重）、结果列表
逻辑：
  1. GetComponents<Component>() — 只扫直接附加的组件
  2. 对每个组件，反射遍历所有字段（Public + NonPublic + Instance）
  3. 若字段类型为 string 且带有 [ResourceKey] Attribute：
      a. 读取字段值（即资源 Key）
      b. 若不为空且 visitedKeys 中不存在，则加入结果列表并记录到 visitedKeys
      c. 若 Attribute.ResourceType == typeof(GameObject)，则递归（见 3.2）
```

### 3.2 新增 `RecursiveCollectFromPrefabAsset`

当 `[ResourceKey(typeof(GameObject))]` 字段被命中时，该字符串值指向另一个 Prefab，需要深入扫描该 Prefab 自身的组件依赖：

```
输入：prefabKey（Addressable address）、结果集、HashSet<string> visitedPrefabs（防环）
逻辑：
  1. 检查 visitedPrefabs — 若已访问则直接 return（防 A→B→A 的循环）
  2. 在 Addressables Settings 的所有 Group.entries 中查找 address == prefabKey 的条目
  3. AssetDatabase.LoadAssetAtPath 加载 Prefab Asset
  4. 遍历其 Component，扫描 [ResourceKey] 字段（同 3.1 逻辑）
  5. 遇到新的 [ResourceKey(typeof(GameObject))] 则继续递归
```

> **边界**：若 Addressables 中找不到对应 Prefab，打印 `LogWarning` 并跳过，不中断导出。

### 3.3 整合到 `ExportLevel` 主流程

```csharp
// 在 markers 循环外初始化收集容器
HashSet<string> visitedKeys = new HashSet<string>();
HashSet<string> visitedPrefabs = new HashSet<string>();
List<string> resultKeys = new List<string>();

// 在每个 marker 处理完 objData 后扫描资源 Key
CollectResourceKeys(go, visitedKeys, visitedPrefabs, resultKeys);

// 循环结束后写入 config
config.resourceKeys = resultKeys;
```

---

## 数据流对比

```
【重构前 — 运行时反射】
  关卡加载 → ScanForResourceKeys(LevelConfig) → 反射递归 → 加载

【重构后 — 编辑时静态分析】
  导出关卡 → CollectResourceKeys(场景GO) → 写入 LevelConfig.resourceKeys
  关卡加载 → 读取 resourceKeys → 查 Registry → 加载
```

---

## 实施顺序

1. **`LevelConfig.cs`** — 新增 `resourceKeys` 字段（无破坏性）
2. **`LevelExporter.cs`** — 添加扫描逻辑，重新导出关卡验证 Inspector 中的 `resourceKeys` 内容
3. **`ResourceManager.cs`** — 验证无误后，替换反射扫描为读取清单

> [!WARNING]
> 第三步完成后，所有旧版 `LevelConfig`（`resourceKeys` 为空）必须重新通过 `LevelExporter` 导出，否则运行时将无法加载关卡资源。
