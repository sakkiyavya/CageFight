# ProcedureFSM 设计评审与改进方案

## 一、现有设计梳理

### 1.1 状态分层定义

| 状态 | 职责 | 对应场景 |
| :--- | :--- | :--- |
| `MenuState` | 主菜单流程 | 游戏启动后的主界面，含关卡选择等 |
| `LoadingState` | 资源加载过渡 | 玩家确认进入关卡 → 关卡资源下载/加载完毕 |
| `GameplayState` | 局内战斗 | 正式游戏对局进行中 |
| `GameOverState` | 结束结算 | 战斗结束后的胜利/失败结算界面 |

### 1.2 当前架构概览

```
SceneFSM（单例，MonoBehaviour，协程驱动）
    ├── Dictionary<GameState, SceneStateBase> 状态映射
    └── TransitionToStateRoutine：Exit() → Enter() 串行执行

SceneStateBase（抽象基类）
    ├── abstract Enter() : IEnumerator
    └── abstract Exit()  : IEnumerator

MenuState / LoadingState / GameplayState / GameOverState
    └── 各自实现 Enter / Exit
```

---

## 二、现有设计的问题点

### ❌ 问题 1：`MenuState` 与 `UISystemBase` 的耦合方式过于特化

**现状**：`MenuState.Awake()` 通过 `GetComponentsInChildren<UISystemBase>` 自动收集所有子对象 UI，并以 `transitionTime` 最大的那个作为协程等待基准。

**问题**：
- 该逻辑**只服务于 MenuState**，其他状态（GameplayState、GameOverState）也需要管理一批 UI 模块，却不得不各自重复实现类似逻辑，无法复用。
- 策划无法直观地在 Inspector 中看到"这个状态打开了哪些 UI 模块"，需要靠隐式的子对象层级关系推断。
- 若需要某个 UI 模块在该状态**不打开**，只能将其移出子节点，场景层级管理成本高。

### ❌ 问题 2：`SceneStateBase` 中的 `stateOpenUIs` 语义不清晰

**现状**：`SceneStateBase` 中新增了 `List<UISystemBase> stateOpenUIs`，但该字段是 `private`，策划无法在 Inspector 中配置，且 `Enter` / `Exit` 中也没有使用它的逻辑。

**问题**：
- 字段未标注 `[SerializeField]`，Inspector 中不可见，策划配置意图无法实现。
- 字段未被基类的 `Enter` / `Exit` 利用，等于是"空挂"，不产生任何效果。

### ❌ 问题 3：`LoadingState` 缺少与资源系统的接入点

**现状**：`LoadingState` 的 `Enter` / `Exit` 仅是固定时长的 `WaitForSeconds` 占位。

**问题**：
- 资源系统（`ResourceManager`）有明确的 `InitializeGlobalHotUpdate()` 和 `PrepareLevelResources()` 两条加载链路，`LoadingState` 目前与之完全脱钩。
- 进度回调（`Progress`）无处驱动 Loading UI，用户体验缺失。

### ❌ 问题 4：`GameplayState` 与 `GameOverState` 的 UI 管理责任边界不清

**现状**：`GameplayState` / `GameOverState` 的 `Enter` / `Exit` 全为空占位。

**问题**：
- 局内战斗 UI（血条、经济面板、技能栏）以及结算 UI 的开关逻辑，目前没有任何统一的地方去驱动。
- 如果每个具体状态自己硬编码哪些 UI 要打开，策划若想调整（如在结算界面也显示关卡信息），就必须改代码。

### ❌ 问题 5：`MenuState` 的"最长过渡 UI 作为同步基准"策略有隐患

**现状**：`MenuState.Awake()` 找出 `transitionTime` 最长的 UI 作为 `yield return` 的等待对象，其余 UI 以"发射并忘记"方式异步播放。

**问题**：
- 如果最长 UI 不是最后一个完成动画的（如某个子 UI 有二段动画），基准选择会出错。
- 更稳健的方式是**并行启动所有 UI 动画，统一等待全部完成**，而非只等最长那一个。
- 该逻辑在基类中无法复用，所有状态都得重写一遍。

---

## 三、改进方案

### 3.1 核心思路

> **FSM 只管 UI 模块（`UISystemBase` 根节点）的整体开关；模块内部具体子 UI 的细节由 `UISystemBase.subUI` 链路自治。**

在 `SceneStateBase` 基类中封装统一的 UI 模块管理逻辑，由策划在 Inspector 中直接拖拽配置"该状态需要打开哪些 UI 模块"，子类无需再手动处理 UI 动画协程。  
**不引入任何新类**，直接以现有 `UISystemBase` 实例作为模块单元。

---

### 3.2 直接以 `UISystemBase` 作为 UI 模块

每个"UI 模块"就是一个挂有 `UISystemBase` 的 GameObject 根节点（如 `GameStartPanel`、`HUDPanel`、`GameOverPanel`），其内部的子 UI 通过 `UISystemBase.subUI` 列表自行管理。

**关键点**：
- FSM 只需调用 `UISystemBase.UIMotionEffectRoutine(true/false)`，该方法已内建对 `subUI` 列表的并行驱动逻辑，无需额外封装。
- 模块粒度由策划决定：Inspector 里拖哪个 `UISystemBase` 组件，那个节点就是模块的根。
- 无需新建任何类，架构更轻量。

---

### 3.3 改进后的 `SceneStateBase`

```csharp
public abstract class SceneStateBase : MonoBehaviour
{
    [Header("该状态激活时打开的 UI 模块（由策划在 Inspector 配置）")]
    [SerializeField] List<UISystemBase> stateModules = new List<UISystemBase>();

    // 进入状态：并行打开所有 UI 模块，等待全部完成
    public virtual IEnumerator Enter()
    {
        yield return OpenModules();
        yield return OnEnter(); // 子类扩展点
    }

    // 退出状态：并行关闭所有 UI 模块，等待全部完成
    public virtual IEnumerator Exit()
    {
        yield return CloseModules();
        yield return OnExit(); // 子类扩展点
    }

    // 并行打开所有模块，统一等待全部动画完成
    private IEnumerator OpenModules()
    {
        var coroutines = new List<Coroutine>();
        foreach (var module in stateModules)
        {
            if (module != null)
                coroutines.Add(StartCoroutine(module.UIMotionEffectRoutine(true)));
        }
        foreach (var co in coroutines)
            yield return co;
    }

    // 并行关闭所有模块，统一等待全部动画完成
    private IEnumerator CloseModules()
    {
        var coroutines = new List<Coroutine>();
        foreach (var module in stateModules)
        {
            if (module != null)
                coroutines.Add(StartCoroutine(module.UIMotionEffectRoutine(false)));
        }
        foreach (var co in coroutines)
            yield return co;
    }

    // 子类专属逻辑扩展点（不再需要手动处理 UI）
    protected virtual IEnumerator OnEnter() { yield return null; }
    protected virtual IEnumerator OnExit()  { yield return null; }
}
```

**效果**：
- 策划在每个状态对象的 Inspector 中直接拖拽需要开启的 `UISystemBase` 根节点组件。
- 新增/移除 UI 模块完全在编辑器内完成，零代码改动。
- `Enter` / `Exit` 的抽象约束改为 `virtual`，基类提供默认的 UI 模块驱动实现，子类只需 override `OnEnter` / `OnExit` 处理业务逻辑。

---

### 3.4 各状态的职责边界（改进后）

#### `MenuState`
- **开放给策划配置**：主菜单所有 UI 模块根节点（例：`GameStartPanel`、`SettingPanel`、`RankingPanel`）
- **OnEnter 业务逻辑**：可发起关卡列表的异步加载（触发 `LevelConfigLoader`）
- **OnExit 业务逻辑**：清理翻页状态、重置关卡选择滚动位置

#### `LoadingState`
- **开放给策划配置**：加载进度条 UI 根节点（`LoadingPanel`）
- **OnEnter 业务逻辑**：
  1. 调用 `ResourceManager.Instance.PrepareLevelResources(selectedConfig)` 启动关卡资源预下载
  2. 协程轮询 `ResourceManager.Instance.Progress`，驱动进度条 UI 更新
  3. 加载完毕后通知 `SceneFSM.Instance.LoadState(GameState.Gameplay)`
- **OnExit 业务逻辑**：无（UI 由基类 `CloseModules` 自动处理）

#### `GameplayState`
- **开放给策划配置**：局内 HUD 根节点（`HUDPanel`：血条、经济面板、技能栏等）
- **OnEnter 业务逻辑**：启动局内计时器、通知 StageSystem 实例化地图与单位
- **OnExit 业务逻辑**：停止计时器、清理场上所有怪物/子弹/网格数据

#### `GameOverState`
- **开放给策划配置**：结算 UI 根节点（`GameOverPanel`）
- **OnEnter 业务逻辑**：传入战斗结果数据，刷新结算面板内容
- **OnExit 业务逻辑**：释放本局关卡资源（调用 `ResourceManager.ReleaseLevelResources()`）

---

### 3.5 场景层级示例

```
场景层级示例（MenuState 对应的 GameObject 树）
├── MenuState （SceneStateBase子类）
│     stateModules = [GameStartPanel, SettingPanel]  ← Inspector 中拖拽
│
├── GameStartPanel （UISystemBase，作为模块根节点）
│   └── subUI：[TitlePanel, LevelButtonLayout, PageButtons ...]
│
└── SettingPanel （UISystemBase，作为模块根节点）
    └── subUI：[VolumeSlider, GraphicsOption ...]
```

- FSM 调用 `UISystemBase.UIMotionEffectRoutine(true/false)`，该方法已内建对 `subUI` 列表的并行驱动逻辑。
- FSM → UISystemBase（模块根）→ subUI，两层即可覆盖所有场景，职责边界清晰。

---

### 3.6 `LoadingState` 与资源系统的接入规范

`LoadingState.OnEnter()` 建议遵循以下时序：

```
1. 确保 LoadingPanel 已打开（基类 OpenModules 已处理）
2. 调用 ResourceManager.Instance.PrepareLevelResources(config, registry)
3. while (!ResourceManager.Instance.IsLoaded)
       loadingUI.SetProgress(ResourceManager.Instance.Progress)
       yield return null
4. SceneFSM.Instance.LoadState(GameState.Gameplay)
```

订阅 `ResourceManager.Instance.OnResourcesLoadFailed` 事件处理弱网失败，弹出重试 UI。

---

## 四、改进前后对比

| 维度 | 改进前 | 改进后 |
| :--- | :--- | :--- |
| **策划配置 UI** | 靠子对象层级隐式决定，不直观 | Inspector 列表直接拖拽 `UISystemBase`，一目了然 |
| **添加/移除 UI 模块** | 需要修改代码或移动场景节点 | 编辑器中增减列表元素，零代码改动 |
| **UI 等待逻辑** | 仅等最长那个 UI（有失效风险） | 并行启动、统一等待全部完成，鲁棒 |
| **代码复用** | 各状态各写一套 UI 收集逻辑 | 基类统一处理，子类只写业务逻辑 |
| **资源加载接入** | LoadingState 完全脱钩 | OnEnter 中标准化接入 ResourceManager |
| **职责边界** | FSM / UI 动画代码混在一起 | FSM → UISystemBase（模块根）→ subUI，两层即可 |
| **新增类** | — | **无需新增任何类**，直接复用现有 `UISystemBase` |

---

## 五、无需新建文件

> 直接使用现有 `UISystemBase` 作为模块单元，**本次改动不新增任何类文件**。

仅需改动已有文件：

| 文件 | 改动内容 |
| :--- | :--- |
| `SceneStateBase.cs` | 加入 `[SerializeField] List<UISystemBase> stateModules`；实现 `OpenModules` / `CloseModules`；`Enter` / `Exit` 改为 `virtual`；新增 `OnEnter` / `OnExit` 扩展点 |
| `MenuState.cs` | 删除旧的 `GetComponentsInChildren` 收集逻辑与最长等待策略；业务逻辑移入 `OnEnter` / `OnExit` |
| `LoadingState.cs` | `OnEnter` 接入 `ResourceManager` 加载链路与进度驱动 |
| `GameplayState.cs` | `OnEnter` / `OnExit` 接入 StageSystem 初始化与清理 |
| `GameOverState.cs` | `OnEnter` 接入结算数据；`OnExit` 释放关卡资源 |

---

## 六、实施顺序

1. **改写 `SceneStateBase.cs`**：加入 `[SerializeField] List<UISystemBase> stateModules`，实现 `OpenModules` / `CloseModules`，将 `Enter` / `Exit` 改为 `virtual`，新增 `OnEnter` / `OnExit` 扩展点
2. **重构 `MenuState.cs`**：删除旧的 UI 收集逻辑，将业务逻辑移入 `OnEnter` / `OnExit`
3. **重构 `LoadingState.cs`**：在 `OnEnter` 接入 `ResourceManager` 加载链路
4. **重构 `GameplayState.cs`**：在 `OnEnter` / `OnExit` 接入 StageSystem 初始化与清理
5. **重构 `GameOverState.cs`**：在 `OnEnter` 接入结算数据，`OnExit` 释放资源
6. **在 Inspector 中配置各状态的 `stateModules` 列表**，将对应的 `UISystemBase` 根节点拖入即可
