using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
[RequireComponent(typeof(CharacterHealth))]
public class AttackShieldUnit : BehaviourBase
{
    [Header("护盾属性")]
    [SerializeField, Range(0.01f, 1f)]
    private float shieldHpPercent = 0.1f;

    [SerializeField, Min(0.1f)]
    private float shieldDuration = 10f;

    [Header("护盾外观")]
    [SerializeField, ResourceKey(typeof(GameObject))]
    private string shieldVisualPrefabKey = "UnitVisualFollower";  // 护盾视觉预制体资源键（池化生成）。
    [SerializeField, ResourceKey(typeof(Sprite))]
    private string shieldSpriteKey = "Bullet3 AP_0";              // 护盾贴图资源键（黄色调色显示）。
    [SerializeField, Min(0.1f)]
    private float shieldSize = 1.5f;

    [SerializeField]
    private Vector3 shieldOffset = Vector3.zero;

    private GameObjectProperty prop;
    private CharacterHealth health;
    private UnitVisualFollower shieldFollower;

    private int shieldHp;
    private float shieldExpireTime;
    private bool wasAttacking;

    private readonly Dictionary<GameObjectProperty, Action<Damage>>
        targetListeners =
            new Dictionary<GameObjectProperty, Action<Damage>>();

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        health = GetComponent<CharacterHealth>();
    }

    private void OnEnable()
    {
        prop.OnAtt += OnAttack;

        shieldHp = 0;
        wasAttacking = prop.isAttack;

        SetShieldVisible(false);

        // 经生命框架统一扩展点登记护盾吸收修正器（OnDisable 对称注销）。
        if (health != null)
            health.RegisterDamageModifier(AbsorbDamage);
    }

    private void OnDisable()
    {
        prop.OnAtt -= OnAttack;

        if (health != null)
            health.UnregisterDamageModifier(AbsorbDamage);

        foreach (var pair in targetListeners)
        {
            if (pair.Key != null)
                pair.Key.OnHitted -= pair.Value;
        }

        targetListeners.Clear();

        shieldHp = 0;
        SetShieldVisible(false);
    }

    /// <summary>经 CharacterAI 调度初始化：依赖已在 Awake 缓存，此处仅兜底补齐。</summary>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (this.prop == null)
            this.prop = prop;
        if (this.health == null)
            this.health = health;
    }

    /// <summary>逐帧检测攻击状态切换与护盾到期；被动不阻止后续 AI 行为。</summary>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        // 即使攻击动画没有调用 OnAtt，也能检测攻击状态。
        if (this.prop.isAttack && !wasAttacking)
            OnAttack();

        wasAttacking = this.prop.isAttack;

        if (shieldHp > 0 &&
            Time.time >= shieldExpireTime)
        {
            BreakShield();
        }

        return false;
    }

    private void LateUpdate()
    {
        if (shieldFollower == null ||
            !shieldFollower.IsActive)
        {
            return;
        }

        // 轻微呼吸效果。
        float pulse =
            1f + Mathf.Sin(Time.time * 4f) * 0.05f;

        shieldFollower.transform.localScale =
            Vector3.one * shieldSize * pulse;
    }

    private void OnAttack()
    {
        RefreshShield();
        ListenForTargetHit(prop.target);
    }

    private void RefreshShield()
    {
        shieldHp = Mathf.Max(
            1,
            Mathf.RoundToInt(
                prop.maxHp * shieldHpPercent
            )
        );

        shieldExpireTime =
            Time.time + shieldDuration;

        SetShieldVisible(true);
    }

    /// <summary>
    /// 入伤修正器（经 CharacterHealth 统一扩展点登记）：护盾在正式扣血前吸收本次已结算伤害，
    /// 返回剩余伤害；不重复计算、不预先回血抵消。
    /// </summary>
    private int AbsorbDamage(Damage damage)
    {
        if (shieldHp <= 0 || damage.finalDamage <= 0)
            return damage.finalDamage;

        if (Time.time >= shieldExpireTime)
        {
            BreakShield();
            return damage.finalDamage;
        }

        int absorbed = Mathf.Min(shieldHp, damage.finalDamage);
        shieldHp -= absorbed;

        if (shieldHp <= 0)
            BreakShield();

        return damage.finalDamage - absorbed;
    }

    private void BreakShield()
    {
        shieldHp = 0;
        SetShieldVisible(false);
    }

    private void ListenForTargetHit(
        GameObject targetObject)
    {
        if (targetObject == null)
            return;

        GameObjectProperty targetProp =
            targetObject.GetComponent<GameObjectProperty>();

        if (targetProp == null ||
            targetListeners.ContainsKey(targetProp))
        {
            return;
        }

        Action<Damage> listener = null;

        listener = damage =>
        {
            if (damage.source != gameObject)
                return;

            // 确认攻击命中后，让敌人锁定本单位。
            if (!targetProp.isDead)
                targetProp.target = gameObject;

            targetProp.OnHitted -= listener;
            targetListeners.Remove(targetProp);
        };

        targetListeners.Add(targetProp, listener);
        targetProp.OnHitted += listener;
    }

    private void SetShieldVisible(bool visible)
    {
        if (!visible)
        {
            if (shieldFollower != null)
            {
                shieldFollower.Finish();
                shieldFollower = null;
            }
            return;
        }

        if (shieldFollower != null)
        {
            if (shieldFollower.IsActive)
                return;
            shieldFollower = null;
        }

        if (ResourceManager.Instance == null)
            return;

        // 延迟补齐：资源未就绪时本次跳过，下次施放时重试。
        GameObject prefab = ResourceManager.Instance.GetGameObject(shieldVisualPrefabKey);
        if (prefab == null)
            return;

        Sprite sprite = ResourceManager.Instance.GetSprite(shieldSpriteKey);

        GameObject go = GameObjectPool.Instance.Get(prefab);
        if (go == null)
            return;

        UnitVisualFollower follower = go.GetComponent<UnitVisualFollower>();
        if (follower == null)
        {
            // 预制体已预配置 UnitVisualFollower（正式池化表现模块）；缺失时归还并安全失败。
            GameObjectPool.Instance.Release(go);
            return;
        }

        SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            if (sprite != null)
                renderer.sprite = sprite;

            SpriteRenderer reference = FindReferenceRenderer();
            if (reference != null)
            {
                renderer.sortingLayerID = reference.sortingLayerID;
                renderer.sortingOrder = reference.sortingOrder + 100;
            }
            else
            {
                renderer.sortingOrder = 100;
            }

            renderer.color = new Color(1f, 1f, 1f, 1f);
        }

        // 不变色（纯白）+ 恒定半透明：呼吸参数上下限都设为 0.5，
        // 避免 UnitVisualFollower.Init 把透明度重置回 1（不呼吸时取上限值）。
        follower.Init(gameObject, shieldOffset, 0f, 0.5f, 0.5f);
        shieldFollower = follower;
    }

    private SpriteRenderer FindReferenceRenderer()
    {
        SpriteRenderer[] renderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        SpriteRenderer result = null;

        foreach (SpriteRenderer renderer in renderers)
        {
            if (result == null ||
                renderer.sortingOrder >
                result.sortingOrder)
            {
                result = renderer;
            }
        }

        return result;
    }
}
