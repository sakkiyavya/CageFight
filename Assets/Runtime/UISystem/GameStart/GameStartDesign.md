# GameStart UI 系统设计计划书

## 一、系统概述

本模块负责游戏启动界面的关卡选择功能，由四个脚本协作完成：

| 脚本 | 职责 |
| :--- | :--- |
| **`LevelConfigLoader`** | 通过 Addressables 按命名规则逐个异步加载关卡配置（`LevelConfig`），加载完后将结果列表交给布局脚本 |
| **`LevelButtonLayout`** | 接收关卡配置列表，按 x×y 分页规则将当前页的关卡按钮排列到正确的局部坐标位置，并为每个按钮绑定配置；暴露翻页方法供 `LevelPageButton` 调用 |
| **`LevelButton`** | 挂载在单个关卡按钮预制体上，持有 `LevelConfig`，按下时调用 `StageLoader.Instance.StartLoad()` |
| **`LevelPageButton`** | 挂载在翻页按钮上，通过 `public bool isNext` 控制前翻/后翻，点击后调用 `LevelButtonLayout` 对应的翻页方法 |

---

## 二、各脚本设计

### 2.1 LevelConfigLoader

**挂载位置**：场景中任意管理者对象（建议与 `LevelButtonLayout` 同 GameObject）。

**字段**：

```csharp
[SerializeField] private LevelButtonLayout _layout;
```

**加载逻辑**（Coroutine）：

```
命名规则：Key = "stage1", "stage2", ... "stageN"
从 stage1 开始，逐个尝试加载：
  Addressables.LoadAssetAsync<LevelConfig>("stage{i}")
  若加载成功 → 加入 results 列表，i++，继续
  若加载失败（Status != Succeeded）→ 释放句柄，停止循环

加载结束后：
  _layout.configs = results
  _layout.LayoutButtons()
```

**注意事项**：
- 加载失败的句柄须调用 `Addressables.Release(handle)` 释放，成功的句柄自行维护列表以便后续释放。
- 若 `stage1` 即不存在，`results` 为空列表，布局脚本须对空列表做容错。

---

### 2.2 LevelButtonLayout

**挂载位置**：关卡列表面板的根节点（`RectTransform`，锚点为 `Top`）。

**字段**：

```csharp
// 每页显示 列数(x) × 行数(y)，OnValidate 钳位 x,y ≥ 1
[SerializeField] private Vector2Int pageSize = new Vector2Int(3, 3);

// 按钮之间的间距
[SerializeField] private Vector2 spacing = new Vector2(200f, 200f);

// 关卡按钮预制体（需挂载 LevelButton 与 RectTransform）
[SerializeField] private GameObject buttonPrefab;

// 由 LevelConfigLoader 写入
public List<LevelConfig> configs = new List<LevelConfig>();

// 当前显示页（从 0 开始）
private int _currentPage = 0;

// 缓存本页已生成的按钮
private List<GameObject> _spawnedButtons = new List<GameObject>();
```

**计算属性**：

```csharp
private int PageCapacity => pageSize.x * pageSize.y;  // 每页最多显示数量
private int TotalPages   => Mathf.Max(1, Mathf.CeilToInt((float)configs.Count / PageCapacity));
```

**`LayoutButtons()` 方法逻辑**：

```
1. 清理已有按钮（遍历 _spawnedButtons → Destroy，Clear）
2. 计算本页起始索引：startIndex = _currentPage * PageCapacity
3. 遍历本页的配置（startIndex 到 Min(startIndex + PageCapacity, configs.Count)）：
   a. 实例化 buttonPrefab，父级设为自身
   b. 计算局部坐标（锚点 Top，Y 向下为负）：
      pageLocalIndex = i - startIndex
      col    = pageLocalIndex % pageSize.x
      row    = pageLocalIndex / pageSize.x
      localX = col * spacing.x
      localY = -row * spacing.y
      rectTransform.anchoredPosition = new Vector2(localX, localY)
   c. button.Init(configs[i])
4. 加入 _spawnedButtons 缓存
```

**翻页方法**（供 `LevelPageButton` 调用）：

```csharp
/// <summary>翻页：isNext=true 下一页，false 上一页；边界时不响应</summary>
public void TurnPage(bool isNext)
{
    int target = _currentPage + (isNext ? 1 : -1);
    if (target < 0 || target >= TotalPages) return;
    _currentPage = target;
    LayoutButtons();
}
```

**`OnValidate`**：

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
private LevelConfig _config;
private RectTransform _rectTransform;
```

**接口**：实现 `IPointerDownHandler` + `IPointerUpHandler`（与全局按钮系统保持一致）。

```csharp
public void Init(LevelConfig config)
{
    _config = config;
    // 可在此刷新关卡名文本、图标等显示
}

public void OnPointerDown(PointerEventData eventData) { }  // 预留视觉反馈

public void OnPointerUp(PointerEventData eventData)
{
    if (_config == null || StageLoader.Instance == null) return;
    StageLoader.Instance.StartLoad(_config);
}
```

---

### 2.4 LevelPageButton（新增）

**挂载位置**：前翻/后翻按钮 GameObject。场景中放置两个，分别配置 `isNext`。

**字段**：

```csharp
// true = 下一页；false = 上一页
public bool isNext;

// 目标布局脚本
[SerializeField] private LevelButtonLayout _layout;
```

**接口**：实现 `IPointerDownHandler` + `IPointerUpHandler`。

```csharp
public void OnPointerDown(PointerEventData eventData) { }  // 预留视觉反馈

public void OnPointerUp(PointerEventData eventData)
{
    if (_layout == null)
    {
        Debug.LogWarning("[LevelPageButton] _layout 未配置！", this);
        return;
    }
    _layout.TurnPage(isNext);
}
```

**边界处理**：翻页的边界判断在 `LevelButtonLayout.TurnPage()` 内部完成，`LevelPageButton` 无需额外判断。如有需要，可在 `TurnPage` 返回值（`bool`）成功与否后，在 `LevelPageButton` 侧控制按钮灰化状态。

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
LevelButtonLayout.configs = results
LevelButtonLayout.LayoutButtons()   ← 显示第 0 页
    │ 按 pageSize × spacing 计算 anchoredPosition（Y≤0）
    │ 实例化 buttonPrefab → LevelButton.Init(config)
    ▼
玩家点击翻页按钮 (LevelPageButton)
    │ OnPointerUp → LevelButtonLayout.TurnPage(isNext)
    │   → _currentPage ±1（边界校验）→ LayoutButtons()
    ▼
玩家点击关卡按钮 (LevelButton)
    │ OnPointerUp
    └─ StageLoader.Instance.StartLoad(_config)
```

---

## 四、文件规划

| 文件路径 | 类名 |
| :--- | :--- |
| `Runtime/UISystem/GameStart/LevelConfigLoader.cs` | `LevelConfigLoader` |
| `Runtime/UISystem/GameStart/LevelButtonLayout.cs` | `LevelButtonLayout` |
| `Runtime/UISystem/GameStart/LevelButton.cs` | `LevelButton` |
| `Runtime/UISystem/GameStart/LevelPageButton.cs` | `LevelPageButton` |

---

## 五、实施顺序

1. **`LevelButton.cs`**：最无依赖，先实现 `Init` 与点击回调
2. **`LevelButtonLayout.cs`**：实现分页布局、`TurnPage(bool)` 与 `OnValidate`
3. **`LevelPageButton.cs`**：依赖 `LevelButtonLayout`，极简
4. **`LevelConfigLoader.cs`**：最后实现，依赖布局脚本，串联整个流程
