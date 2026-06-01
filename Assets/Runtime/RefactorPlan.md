# GameObjectProperty 解耦重构计划

本计划详细列出了将 `GameObjectPropertyData` 和 `GameObjectProperty` 中硬编码的 `GameObject` 引用（如 `atkObj`, `buildAnime`）彻底转换为 `string`，并接驳至新版 `ResourcesSystem` 的改动方案，同时包含为了体验优化的定制编辑器开发。

## ⚠️ User Review Required

- 属性变更：在 `GameObjectProperty` 脚本中，原来的 `public GameObject atkObj;` 和 `public GameObject buildAnime;` 将变更为 `public string atkObj;` 和 `public string buildAnime;`。
- 我们将配套编写一个 CustomEditor，虽然底层是 `string`，但您在 Inspector 面板中依然可以直接拖拽 Prefab，系统会自动提取名字并写入字符串字段，体验完全无缝！请确认以下计划即可。

## 🛠️ Proposed Changes

### 1. 数据与组件模型重构 (Runtime/StageSystem & Runtime/Gameplay)

- #### [MODIFY] `Runtime\StageSystem\Model\GameObjectPropertyData.cs`
    - 修改字段：`public GameObject atkObj;` ➔ `public string atkObj;`
    - 修改字段：`public GameObject buildAnime;` ➔ `public string buildAnime;`

- #### [MODIFY] `Runtime\Gameplay\GameObjectProperty.cs`
    - 同步修改类内的物理引用：`public GameObject atkObj;` ➔ `public string atkObj;`
    - 同步修改类内的物理引用：`public GameObject buildAnime;` ➔ `public string buildAnime;`
    - 修改实现接口 `ILevelComponent` 时的 `ExtractData()` 与 `ApplyData()`，让字符串顺利在数据与组件之间双向传递。

### 2. 行为逻辑重构 (Runtime/Gameplay/Character & Building)

- #### [MODIFY] `Runtime\Gameplay\Character\CharacterAI.cs`
    - 定位：`ShootProjectile()` 核心攻击逻辑。
    - 变更：不再直接通过 `_prop.atkObj` 实例化对象，而是改为：
      ```csharp
      if (string.IsNullOrEmpty(_prop.atkObj)) return;
      GameObject atkPrefab = ResourcesSystem.Instance.GetPrefab(_prop.atkObj);
      if (atkPrefab != null) {
          GameObject projectile = GameObjectPool.Instance.Get(atkPrefab);
          // ... 赋值伤害逻辑不变
      }
      ```

- #### [MODIFY] `Runtime\Gameplay\Character\CommonFunc\Attack.cs`
    - 定位：其中被注释掉的备用 `ShootProjectile()` 逻辑。
    - 变更：同样重构为使用 `ResourcesSystem.Instance.GetPrefab(_prop.atkObj)`，保持代码整洁规范。

- #### [MODIFY] `Runtime\Gameplay\Building\BuildingBase.cs`
    - 定位：`BuildRoutine()` 协程中的特效实例化。
    - 变更：将原来的 `Instantiate(_prop.buildAnime)` 替换为从资源管理器提取的实例化逻辑：
      ```csharp
      if (!string.IsNullOrEmpty(_prop.buildAnime)) {
          GameObject animePrefab = ResourcesSystem.Instance.GetPrefab(_prop.buildAnime);
          if (animePrefab != null) {
              buildAnimeInstance = Instantiate(animePrefab, transform.position, transform.rotation);
          }
      }
      ```

### 3. 编辑器工具重构 (Editor/Gameplay)

- #### [NEW] `Editor\Gameplay\GameObjectPropertyEditor.cs`
    - 目标：为 `GameObjectProperty` 编写 `[CustomEditor(typeof(GameObjectProperty))]` 的专属检查器。
    - 核心功能：在 Inspector 脚本末尾渲染两个拖拽槽位（对象类型限定为 `GameObject`）。当您拖入任何物体或者 Prefab 时，拦截拖拽事件，读取其 `name` 属性，并自动赋值给对应的 `atkObj` 或 `buildAnime` 字符串字段，实现底层字符串和高层拖拽体验的完美解耦。

---

## 💡 Antigravity AI 补充与架构优化建议 (极高推荐)

为了使本重构计划达到 100% 工业级闭环，并杜绝因人工操作疏忽导致的线上崩溃，强烈建议在实施时追加以下 4 点技术改造：

### 1. 拖拽时未标记 Addressable 的弹窗引导机制
*   **隐藏痛点**：策划或美术在 `GameObjectPropertyEditor` 拖入一个新特效 Prefab 时，系统虽然自动提取了它的名字字符串，但该 Prefab **本身可能还没有打上 Addressable 标签**。这会导致在实机运行时因“资源未在 Catalog 中”而加载失败。
*   **安全防御方案**：在自定义编辑器拦截到拖拽 Prefab 事件时，**自动检测该 Prefab 的 Addressable 状态**。如果检测到未注册，系统将**自动弹出 Unity 编辑器警告弹窗 (`EditorUtility.DisplayDialog`)**，强高亮提醒：“您拖入的特效预制体 '{name}' 尚未被标记为 Addressable，实机包将无法加载！请务必先将其标记为 Addressable。”
*   **收益**：通过强制预警弹窗机制，系统能即时发现并纠正开发操作疏忽，引导美术/策划主动确认资产热更状态；同时，**避免在后台自动静默标记导致测试或垃圾预制体被误塞入热更新资源组**，保证资产库的纯净与可维护秩序。

### 2. 深度扫描联动（一键注册表查漏补缺）
*   **优化方案**：在 `PrefabRegistryBuilder.cs`（一键生成注册表）工具中增加**深度依赖扫描**。除了扫描 `LevelObjectData.prefabKey` 外，还要深度扫描场景配置中所有 `GameObjectPropertyData` 下的 `atkObj` 和 `buildAnime` 字符串。
*   **收益**：如果策划直接在文本框里手填或复制粘贴了特效名称，或者在多人协作中被冲突合并了配置，一键扫描工具能在打包前**自动将这些非空字符串对应的 Prefab 全部打上 Addressable 标签**，提供终极的“安全保障网”。

### 3. 与已定接口规范统一对齐
*   **已修正细节**：已将计划中引用的接口统一为已实现的 `ResourceManager`，使用其提供的获取方法。
    - 统一类名为 **`ResourceManager`**（单例为 `ResourceManager.Instance`）。
    - 检索方法使用 **`GetGameObject(string key)`**、`GetAudio(string key)`、`GetTexture(string key)`、`GetAnimation(string key)`、`GetAnimatorController(string key)`，保持与 `ResourcesSystemDesign.md` 中实现一致。

### 4. 级联内存释放与对象池 Clear 联动
*   **优化方案**：在 `GameObjectProperty` 销毁或关卡结束（`ReleaseLevelResources`）时，除了释放预制体句柄外，必须显式调用 `GameObjectPool.Instance.Clear(atkPrefab)`，以彻底断开对象池对该原始 Prefab 模板的强引用。
*   **收益**：只有断开对象池中的模板强引用，Addressables 的引用计数才能真正归零，并在 WebGL 中彻底释放显存，达成完美的内存生命周期闭环。

---

## ✅ Verification Plan
1. 所有的编译错误必须被彻底修复。
2. 在 Inspector 中测试：随意拖拽一个特效 Prefab 到专属槽位，验证对应的 string 文本框能够立刻自动填充正确的资源名称，且当拖入一个未打 Addressable 标签的 Prefab 时，能够立刻在编辑器中弹出强提醒警告窗口。
3. 校验 `PrefabRegistryBuilder` 扫描：在关卡配置内写入子弹/特效字符串，运行一键生成后，验证其同样能在 Addressables 系统中被自动查找并打上标签。

---
**本计划已根据您的要求添加了编辑器支持并保存在 Runtime 目录下。请检查该计划，若确认无误，我将立即开始进行全面重构。**
