using UnityEngine;

/// <summary>
/// Ruifa M（芮法镜像）能力：由 Ruifa 本体通过 ISummonedUnit 注入创造者引用，
/// 镜像每次攻击（发射弹幕）都会为创造者恢复 1% 最大生命。
/// 创造者已死亡时治疗无效（CharacterHealth.Heal 内部会忽略死亡目标）。
/// </summary>
[RequireComponent(typeof(GameObjectProperty))]
public class RuifaMirror : BehaviourBase, ISummonedUnit
{
    [Header("回血配置")]
    [SerializeField, Min(0f)]
    private float healHpPercent = 0.01f;        // 每次攻击为创造者恢复的最大生命百分比。

    private GameObjectProperty prop;
    private GameObject creator;
    private CharacterHealth creatorHealth;
    private GameObjectProperty creatorProp;

    /// <summary>经 CharacterAI 调度初始化：依赖已在 Awake 缓存，此处仅兜底补齐。</summary>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (this.prop == null)
            this.prop = prop;
    }

    /// <summary>回血由镜像攻击事件驱动，无每帧行为；返回 false 放行后续 AI。</summary>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        return false;
    }

    #region 生命周期与回调
    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
    }

    private void OnEnable()
    {
        if (prop != null)
            prop.OnAtt += HandleAttacked;
    }

    private void OnDisable()
    {
        if (prop != null)
            prop.OnAtt -= HandleAttacked;

        // 池化回收时清空创造者引用，避免跨轮次残留。
        creator = null;
        creatorHealth = null;
        creatorProp = null;
    }
    #endregion

    #region 公开接口
    /// <summary>
    /// 注入创造者引用，由 CharacterAI.ShootProjectile 在每次生成时调用；
    /// 池化复用的镜像会随每次生成重新注入。
    /// </summary>
    public void SetCreator(GameObject creator)
    {
        this.creator = creator;
        creatorHealth = creator != null ? creator.GetComponent<CharacterHealth>() : null;
        creatorProp = creator != null ? creator.GetComponent<GameObjectProperty>() : null;
    }
    #endregion

    #region 内部实现
    /// <summary>
    /// 响应镜像自身攻击事件，为存活的创造者恢复最大生命对应百分比的生命值。
    /// </summary>
    private void HandleAttacked()
    {
        if (creatorHealth == null || creatorProp == null || creatorProp.isDead)
            return;

        int heal = Mathf.Max(1, Mathf.RoundToInt(creatorProp.maxHp * healHpPercent));
        creatorHealth.Heal(heal);
    }
    #endregion
}
