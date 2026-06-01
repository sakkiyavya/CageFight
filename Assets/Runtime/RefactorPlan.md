# GameObjectProperty 解耦重构计划

本计划详细列出了将 `GameObjectPropertyData` 和 `GameObjectProperty` 中硬编码的 `GameObject` 引用（如 `atkObj`, `buildAnime`）彻底转换为 `string`，并接驳至新版 `ResourceManager` 的改动方案，同时包含为了体验优化的定制编辑器开发。

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
      GameObject atkPrefab = Runtime.ResourcesSystem.ResourceManager.Instance.GetGameObject(_prop.atkObj);
      if (atkPrefab != null) {
          GameObject projectile = GameObjectPool.Instance.Get(atkPrefab);
          // ... 赋值伤害逻辑不变
      }
      ```

- #### [MODIFY] `Runtime\Gameplay\Character\CommonFunc\Attack.cs`
    - 定位：其中被注释掉的备用 `ShootProjectile()` 逻辑。
    - 变更：同样重构为使用 `ResourceManager.Instance.GetGameObject(_prop.atkObj)`，保持代码整洁规范。

- #### [MODIFY] `Runtime\Gameplay\Building\BuildingBase.cs`
    - 定位：`BuildRoutine()` 协程中的特效实例化。
    - 变更：将原来的 `Instantiate(_prop.buildAnime)` 替换为从资源管理器提取的实例化逻辑：
      ```csharp
      if (!string.IsNullOrEmpty(_prop.buildAnime)) {
          GameObject animePrefab = Runtime.ResourcesSystem.ResourceManager.Instance.GetGameObject(_prop.buildAnime);
          if (animePrefab != null) {
              buildAnimeInstance = Instantiate(animePrefab, transform.position, transform.rotation);
          }
      }
      ```

### 3. 【新增】编辑器工具重构 (Editor/Gameplay)

- #### [NEW] `Editor\Gameplay\GameObjectPropertyEditor.cs`
    - 目标：为 `GameObjectProperty` 编写 `[CustomEditor(typeof(GameObjectProperty))]` 的专属检查器。
    - 核心功能：在 Inspector 脚本末尾渲染两个拖拽槽位（对象类型限定为 `GameObject`）。当您拖入任何物体或者 Prefab 时，拦截拖拽事件，读取其 `name` 属性，并自动赋值给对应的 `atkObj` 或 `buildAnime` 字符串字段，实现底层字符串和高层拖拽体验的完美解耦。

## ✅ Verification Plan
1. 所有的编译错误必须被彻底修复。
2. 在 Inspector 中测试：随意拖拽一个特效 Prefab 到专属槽位，验证对应的 string 文本框能够立刻自动填充正确的资源名称，且数据得以保存。

---
**本计划已根据您的要求添加了编辑器支持并保存在 Runtime 目录下。请检查该计划，若确认无误，我将立即开始进行全面重构。**
