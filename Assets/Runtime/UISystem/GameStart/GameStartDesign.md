# GameStart UI 系统设计计划书

## 一、系统概述

本模块负责游戏启动界面的关卡选择功能，由三个脚本协作完成：

| 脚本 | 职责 |
| :--- | :--- |
| **`LevelConfigLoader`** | 通过 Addressables 按命名规则逐个异步加载关卡配置（`LevelConfig`），加载完后将结果列表交给布局脚本 |
| **`LevelButtonLayout`** | 接收关卡配置列表，按 x×y 分页规则将关卡按钮排列到正确的局部坐标位置，并为每个按钮绑定配置 |
| **`LevelButton`** | 挂载在单个关卡按钮预制体上，持有 `LevelConfig`，按下时调用 `StageLoader.Instance.StartLoad()` |

---

## 二、各脚本设计

### 2.1 LevelConfigLoader

**挂载位置**：场景中任意管理者对象（建议与 `LevelButtonLayout` 同 GameObject）。

**字段**：

```csharp
// 布局脚本引用，加载完成后将数据交给它
[SerializeField] private LevelButtonLayout _layout;
```

**加载逻辑**（Coroutine）：

```
命名规则：Key = "stage1", "stage2", ... "stageN"
从 stage1 开始，逐个尝试加载：
  Addressables.LoadAssetAsync<LevelConfig>("stage{i}")
  若加载成功 → 加入 results 列表，i++，继续
  若加载失败（KeyNotFoundException 或 Status != Succeeded）→ 停止，认为读取完成

加载结束后：
  将 results 传给 _layout.configs
  调用 _layout.LayoutButtons()
```

**注意事项**：
- Addressables 加载失败时 `AsyncOperationHandle.Status == Failed`，需要正确释放句柄后再停止循环（`Addressables.Release(handle)`）。
- 若 `stage1` 即不存在，则 `results` 为空列表，布局脚本应对空列表做容错处理。
- 成功加载的句柄需要统一管理，以便后续场景切换时可释放（可交由 `ResourceManager` 管理，或本脚本自行维护列表）。

---

### 2.2 LevelButtonLayout

**挂载位置**：关卡列表面板的根节点（`RectTransform`，锚点为 `TopCenter` 或 `TopLeft`）。

**字段**：

```csharp
// 每页显示列数(x) × 行数(y)，Inspector 中限制 x,y > 0
[SerializeField] private Vector2Int pageSize = new Vector2Int(3, 3);

// 按钮之间的间距（x = 横向间距，y = 纵向间距）
[SerializeField] private Vector2 spacing = new Vector2(200f, 200f);

// 关卡按钮预制体（需挂载 LevelButton 组件和 RectTransform）
[SerializeField] private GameObject buttonPrefab;

// 从 LevelConfigLoader 填入的关卡配置列表（公开，方便外部写入）
public List<LevelConfig> configs = new List<LevelConfig>();

// 缓存已生成的按钮，方便重新布局时清理
private List<GameObject> _spawnedButtons = new List<GameObject>();
```

**`LayoutButtons()` 方法逻辑**：

```
1. 清理已有按钮（_spawnedButtons 遍历 Destroy，Clear 列表）

2. 遍历 configs（索引 i = 0, 1, 2, ...）：
   a. 实例化 buttonPrefab，设置父级为自身 RectTransform
   b. 计算局部坐标（锚点为 Top，Y 轴向下为负）：
      col = i % pageSize.x          // 当前列
      row = i / pageSize.x          // 当前行
      localX = col * spacing.x
      localY = -row * spacing.y     // 负号确保向下排列
      rectTransform.anchoredPosition = new Vector2(localX, localY)
   c. 获取按钮上的 LevelButton 组件，调用 Init(configs[i])

3. 将所有实例化的按钮加入 _spawnedButtons 缓存
```

**Inspector 限制 pageSize.x, pageSize.y > 0**：

通过自定义 `OnValidate` 钳位：

```csharp
private void OnValidate()
{
    pageSize.x = Mathf.Max(1, pageSize.x);
    pageSize.y = Mathf.Max(1, pageSize.y);
}
```

---

### 2.3 LevelButton

**挂载位置**：关卡按钮预制体根节点。

**字段**：

```csharp
// 由 LevelButtonLayout.LayoutButtons() 注入
private LevelConfig _config;

// 缓存自身 RectTransform（供布局脚本操作）
private RectTransform _rectTransform;
```

**初始化方法**：

```csharp
public void Init(LevelConfig config)
{
    _config = config;
    // 可在此更新按钮上的关卡名文本、图标、星级等显示
}
```

**点击事件**（通过 `IPointerDownHandler` 或 Unity Button 组件均可，建议用 `IPointerUpHandler` 与其他按钮系统保持一致）：

```csharp
public void OnPointerUp(PointerEventData eventData)
{
    if (_config == null) return;
    if (StageLoader.Instance == null) return;
    StageLoader.Instance.StartLoad(_config);
}
```

---

## 三、数据流图

```
[场景启动]
    │
    ▼
LevelConfigLoader.Start()
    │ Coroutine：逐个加载 stage1, stage2, ...
    │ 遇到缺失即停止
    ▼
List<LevelConfig> results 传入 LevelButtonLayout.configs
    │
    ▼
LevelButtonLayout.LayoutButtons()
    │ 按 pageSize × spacing 计算锚点坐标（Y≤0）
    │ 实例化 buttonPrefab → LevelButton.Init(config)
    ▼
玩家点击关卡按钮
    │
    ▼
LevelButton.OnPointerUp()
    └─ StageLoader.Instance.StartLoad(_config)
```

---

## 四、文件规划

| 文件路径 | 类名 |
| :--- | :--- |
| `Runtime/UISystem/GameStart/LevelConfigLoader.cs` | `LevelConfigLoader` |
| `Runtime/UISystem/GameStart/LevelButtonLayout.cs` | `LevelButtonLayout` |
| `Runtime/UISystem/GameStart/LevelButton.cs` | `LevelButton` |

---

## 五、实施顺序

1. **`LevelButton.cs`**：最无依赖，先实现 `Init` 与点击回调
2. **`LevelButtonLayout.cs`**：依赖 `LevelButton`，实现布局逻辑与 `OnValidate`
3. **`LevelConfigLoader.cs`**：依赖 `LevelButtonLayout`，实现 Addressables 逐个加载协程

---

## 六、待确认事项

> [!NOTE]
> 以下几点在实现前建议确认：
> 1. **分页翻页**：当关卡数量超过 `pageSize.x × pageSize.y` 时，是否需要支持翻页（本期暂不考虑翻页，超出部分直接向下延伸）？
> 2. **按钮 UI 内容**：`LevelButton.Init()` 中是否需要同步更新关卡名文本（`TMP_Text` / `Text`）或解锁状态图标？
> 3. **Addressables 句柄释放**：成功加载的 `LevelConfig` 句柄是否需要随场景切换一并释放？建议注册到 `ResourceManager._handlesToRelease` 统一管理。
