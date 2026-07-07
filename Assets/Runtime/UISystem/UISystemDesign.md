# UI 系统设计计划书

## 一、系统概述

本系统基于现有 `UISystemBase` 扩展，添加三个新组件：

| 脚本 | 职责 |
| :--- | :--- |
| **`UIStack`** | 单例，维护打开 UI 的栈；负责出栈与关闭逻辑；检测点击空白处事件 |
| **`UIOpenButton`** | 挂载在按钮上；持有目标 `UISystemBase`；检测指针按下/抬起，入栈并触发打开动画 |

---

## 二、各组件设计

### 2.1 UIStack（单例 · 出栈逻辑）

**职责**：维护当前打开的 UI 栈，并在点击最上层 UI 空白处时将其关闭。

**字段**：

```csharp
public static UIStack Instance { get; private set; }

// 打开中的 UI 栈，栈顶为当前最上层 UI
private Stack<UISystemBase> _openStack = new Stack<UISystemBase>();

// 用于检测点击空白处的射线/事件（GraphicRaycaster + EventSystem）
private GraphicRaycaster _raycaster;
private EventSystem _eventSystem;
```

**对外方法**：

```csharp
/// <summary>由 UIOpenButton 调用，将 UI 压入栈并播放打开动画</summary>
public void Push(UISystemBase ui)

/// <summary>将栈顶 UI 弹出并播放关闭动画</summary>
public void Pop()

/// <summary>返回当前栈顶 UI，若栈空则返回 null</summary>
public UISystemBase Peek()
```

**空白处检测逻辑**（在 `Update` 中执行）：

```
每帧检测到输入（触屏或鼠标按下）时：
  1. 用 GraphicRaycaster 对点击位置做射线检测
  2. 遍历命中的 RaycastResult 列表
  3. 如果没有任何一个命中对象属于栈顶 UI 的 RectTransform 层级内
     → 调用 Pop()
```

> 使用 `RectTransformUtility.RectangleContainsScreenPoint` 或 `GraphicRaycaster` 均可；推荐后者以兼容 Canvas 缩放模式。

**Push / Pop 实现要点**：

```
Push(ui):
  _openStack.Push(ui)
  ui.UIMotionEffect(true)    ← 入栈时交给 UIOpenButton 调用，UIStack 只负责维护栈结构

Pop():
  if (_openStack.Count == 0) return
  UISystemBase top = _openStack.Pop()
  top.UIMotionEffect(false)  ← 关闭动画
```

> **职责划分**：入栈 + 打开动画 由 `UIOpenButton` 完成（调用 `UIStack.Instance.Push` + `ui.UIMotionEffect(true)`）；出栈 + 关闭动画 由 `UIStack.Pop()` 完成。

---

### 2.2 UIOpenButton（按钮脚本 · 入栈逻辑）

**职责**：检测指针事件，将目标 UI 入栈并触发打开动画。

**字段**：

```csharp
[Tooltip("点击此按钮后要打开的 UI")]
public UISystemBase targetUI;

// 缓存自身 RectTransform，用于按压视觉反馈（可选）
private RectTransform _rectTransform;
```

**接口实现**：

```csharp
public class UIOpenButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        // 可选：播放按压动画 / 音效
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (targetUI == null) return;
        // 入栈并打开
        UIStack.Instance.Push(targetUI);
        targetUI.UIMotionEffect(true);
    }
}
```

> `IPointerDownHandler` / `IPointerUpHandler` 需要 `using UnityEngine.EventSystems`，且按钮所在 Canvas 需要挂载 `GraphicRaycaster`，同场景需有 `EventSystem`。

---

## 三、空白处点击检测的边界情况

| 情况 | 处理方式 |
| :--- | :--- |
| 栈为空 | 不处理，直接跳过检测 |
| 点击在栈顶 UI 内部（含子 UI） | 不触发 Pop |
| 点击在栈顶 UI 之外（空白处）| 触发 `Pop()`，关闭最上层 UI |
| 点击到 UIOpenButton 所在区域 | UIOpenButton 的事件会先于空白处检测响应，需在 UIStack 内判断本帧是否已有 Push 操作，避免瞬间打开后立即关闭 |
| 多层 UI 叠加 | 每次只 Pop 最顶层，逐层关闭 |

**防打开后立即关闭的策略**：

```csharp
// UIStack 内维护一个帧标记
private bool _pushedThisFrame = false;

// Push 时标记
public void Push(UISystemBase ui)
{
    _pushedThisFrame = true;
    _openStack.Push(ui);
}

// Update 中，若本帧已 Push，则跳过空白处检测
void Update()
{
    if (_pushedThisFrame) { _pushedThisFrame = false; return; }
    // ... 空白处检测
}
```

---

## 四、脚本文件规划

| 文件路径 | 内容 |
| :--- | :--- |
| `Runtime/UISystem/UIStack.cs` | 单例栈管理器，含空白处检测与 Pop 逻辑 |
| `Runtime/UISystem/UIOpenButton.cs` | 按钮脚本，含 Push + UIMotionEffect(true) 逻辑 |

---

## 五、依赖与场景配置要求

- Canvas 上需要挂载 `GraphicRaycaster`
- 场景中需要存在 `EventSystem`
- `UIStack` 建议作为场景内常驻单例（挂在 Canvas 或独立空物体上，不需要 DontDestroyOnLoad）
- `UIOpenButton` 挂在 UGUI Button 的同层 GameObject 上（可与原 Button 组件共存）

---

## 六、实施顺序

1. 实现 `UIStack.cs`（单例 + Push/Pop + 帧标记 + 空白处检测）
2. 实现 `UIOpenButton.cs`（IPointerDownHandler/IPointerUpHandler + Push 调用）
3. 在场景中配置并验证开/关动画链路
