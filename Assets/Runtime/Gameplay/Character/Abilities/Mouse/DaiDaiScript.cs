using UnityEngine;

/// <summary>
/// 呆呆（Damb ass）死亡残骸机制：
/// 生命归零时经 CharacterHealth 死亡复活器接管——不播死亡抛飞动画，
/// 而是在原地生成残骸单位（Derivative-Two fool，已升级为带生命/碰撞/占地的正式单位），
/// 本体静默归还对象池。
/// 残骸经 GameObjectProperty.CopyPersistentDataTo 继承本体的阵营/生命/占地配置，
/// 敌方会继续攻击残骸；残骸死亡时走常规死亡流程（抛飞 + 回收）。
/// </summary>
[RequireComponent(typeof(GameObjectProperty))]
[RequireComponent(typeof(CharacterHealth))]
public class DaiDaiScript : BehaviourBase
{
    [SerializeField, ResourceKey(typeof(GameObject)), Tooltip("残骸预制体资源键（生命归零时原地生成）")]
    private string wreckagePrefabKey = "Derivative-Two fool";

    private GameObjectProperty prop;
    private CharacterHealth health;
    private GameObject _wreckagePrefab;      // 经 ResourceManager 解析的残骸预制体缓存。
    private bool triggered;

    /// <summary>经 CharacterAI 调度初始化：依赖已在 Awake 缓存，此处仅兜底补齐。</summary>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (this.prop == null)
            this.prop = prop;
        if (this.health == null)
            this.health = health;
    }

    /// <summary>残骸生成由死亡复活器驱动，无每帧行为；返回 false 放行后续 AI。</summary>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        return false;
    }

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        health = GetComponent<CharacterHealth>();
    }

    private void OnEnable()
    {
        triggered = false;
        // 经生命框架统一扩展点登记死亡复活器（OnDisable 对称注销）。
        if (health != null)
            health.RegisterDeathReviver(TryRevive);
    }

    private void OnDisable()
    {
        if (health != null)
            health.UnregisterDeathReviver(TryRevive);
    }

    /// <summary>
    /// 死亡接管：原地生成残骸并静默回收本体（不播死亡动画）；
    /// 残骸生成失败时放行常规死亡流程。
    /// </summary>
    /// <param name="lethalDamage">导致生命归零的伤害数据。</param>
    /// <returns>残骸生成成功时返回 <see langword="true"/>（跳过常规死亡流程）。</returns>
    private bool TryRevive(Damage lethalDamage)
    {
        if (triggered)
            return false;

        triggered = true;

        if (!SpawnWreckage())
            return false;

        // 本体静默离场：跳过死亡抛飞动画，直接归还对象池，残骸接管战场表现。
        GameObjectPool.Instance.Release(gameObject);
        return true;
    }

    /// <summary>
    /// 在当前位置生成残骸：经 ResourceManager 解析池化预制体，从对象池取实例，
    /// 并经框架接口 CopyPersistentDataTo 继承本体配置（阵营、生命、占地等），
    /// 残骸以本体最大生命登场，可被敌方攻击，死亡走常规流程。
    /// </summary>
    /// <returns>残骸是否成功生成。</returns>
    private bool SpawnWreckage()
    {
        if (ResourceManager.Instance == null || prop == null)
            return false;

        // 延迟补齐：资源键已进公共预载清单，正常对局即已就绪。
        if (_wreckagePrefab == null && !string.IsNullOrEmpty(wreckagePrefabKey))
            _wreckagePrefab = ResourceManager.Instance.GetGameObject(wreckagePrefabKey);

        if (_wreckagePrefab == null)
            return false;

        GameObject wreck = GameObjectPool.Instance.Get(_wreckagePrefab);
        if (wreck == null)
            return false;

        GameObjectProperty wreckProp = wreck.GetComponent<GameObjectProperty>();
        if (wreckProp == null)
        {
            // 预制体已预配置完整单位组件；缺失时归还并安全失败。
            GameObjectPool.Instance.Release(wreck);
            return false;
        }

        wreck.transform.position = transform.position;
        prop.CopyPersistentDataTo(wreckProp);
        return true;
    }
}
