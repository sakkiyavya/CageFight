using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
[RequireComponent(typeof(ParalysisDebuff))]
public class LightningCatScript : BehaviourBase
{
    [Header("电柱素材")]
    [SerializeField, ResourceKey(typeof(GameObject))]
    private string lightningPrefabKey = "LightningBeam";

    [SerializeField, Min(0.01f)]
    private float lightningDuration = 0.2f;

    [Header("第二目标")]
    [SerializeField, Min(0f)]
    private float secondTargetRange = 10f;

    [SerializeField] private Transform shootPoint;

    private GameObjectProperty prop;
    private CharacterHealth health;
    private ParalysisDebuff paralysisBuff;
    private GameObject _lightningPrefab;    // 经 ResourceManager 解析的电柱预制体缓存。
    private readonly HashSet<GameObject> nearbyUnits =
        new HashSet<GameObject>();

    /// <summary>经 CharacterAI 调度初始化：依赖已在 Awake 缓存，此处仅兜底补齐。</summary>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (this.prop == null)
            this.prop = prop;
        if (this.health == null)
            this.health = health;
    }

    /// <summary>闪电由攻击动画事件驱动，无每帧行为；返回 false 放行后续 AI。</summary>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        return false;
    }

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        health = GetComponent<CharacterHealth>();
        paralysisBuff = GetComponent<ParalysisDebuff>();

        if (shootPoint == null)
            shootPoint = transform.Find("ShootPoint");
    }

    // 供攻击动画事件调用。
    public void CastLightning()
    {
        if (prop.target == null || prop.isDead)
            return;

        GameObjectProperty firstTarget =
            prop.target.GetComponent<GameObjectProperty>();

        if (!IsValidEnemy(firstTarget))
            return;

        GameObjectProperty secondTarget =
            FindSecondTarget(firstTarget);

        AttackTarget(firstTarget);

        if (secondTarget != null)
            AttackTarget(secondTarget);

        prop.OnAtt?.Invoke();
    }

    private void AttackTarget(
        GameObjectProperty target)
    {
        if (!IsValidEnemy(target))
            return;

        CharacterHealth health =
            target.GetComponent<CharacterHealth>();

        if (health == null)
            return;

        Damage damage = Damage.DefaultDamage;

        damage.side = prop.side;
        damage.source = gameObject;
        damage.target = target.gameObject;
        damage.initialDamage = prop.atk;
        damage.repel = prop.repel;
        damage.type = DamageType.normal;
        damage.buffs = new BuffBase[]
        {
            paralysisBuff
        };

        health.OnCollide(damage);

        Vector3 start = shootPoint != null
            ? shootPoint.position
            : transform.position;

        CreateLightning(
            start,
            target.transform.position
        );
    }

    private GameObjectProperty FindSecondTarget(
        GameObjectProperty firstTarget)
    {
        GameObjectProperty closest = null;

        float closestDistance =
            secondTargetRange * secondTargetRange;

        MapCells map = MapCells.Instance;
        if (map == null)
            return null;

        int minX = Mathf.FloorToInt(
            transform.position.x - secondTargetRange);
        int minY = Mathf.FloorToInt(
            transform.position.y - secondTargetRange);
        int maxX = Mathf.CeilToInt(
            transform.position.x + secondTargetRange);
        int maxY = Mathf.CeilToInt(
            transform.position.y + secondTargetRange);

        nearbyUnits.Clear();
        map.CollectOccupiersInBounds(
            new Vector2Int(minX, minY),
            new Vector2Int(maxX, maxY),
            nearbyUnits);

        foreach (GameObject unit in nearbyUnits)
        {
            GameObjectProperty candidate =
                unit.GetComponent<GameObjectProperty>();
            if (candidate == null ||
                candidate == prop ||
                candidate == firstTarget)
            {
                continue;
            }

            if (!IsValidEnemy(candidate))
                continue;

            // 只选择具有角色生命组件的单位。
            if (candidate.GetComponent<CharacterHealth>() == null)
                continue;

            float distance =
                (candidate.transform.position -
                 transform.position).sqrMagnitude;

            if (distance > closestDistance)
                continue;

            closestDistance = distance;
            closest = candidate;
        }

        return closest;
    }

    private bool IsValidEnemy(
        GameObjectProperty target)
    {
        if (target == null)
            return false;

        if (target.side == prop.side)
            return false;

        CharacterHealth health =
            target.GetComponent<CharacterHealth>();

        return health != null && !health.IsDead();
    }

    private void CreateLightning(
        Vector3 start,
        Vector3 end)
    {
        // 电柱预制体按资源键经 ResourceManager 解析（规范禁止 Inspector 直引作为运行时兜底）。
        if (_lightningPrefab == null && ResourceManager.Instance != null)
            _lightningPrefab = ResourceManager.Instance.GetGameObject(lightningPrefabKey);

        if (_lightningPrefab == null)
            return;

        GameObject lightning =
            GameObjectPool.Instance.Get(
                _lightningPrefab
            );

        if (lightning == null)
            return;

        LightningBeamRuntime runtime =
            lightning.GetComponent<LightningBeamRuntime>();

        // 预制体已预配置 LightningBeamRuntime；缺失时归还对象池并安全失败。
        if (runtime == null)
        {
            GameObjectPool.Instance.Release(lightning);
            return;
        }

        runtime.Show(
            start,
            end,
            lightningDuration
        );
    }
}

class LightningBeamRuntime : MonoBehaviour
{
    private Vector3 originalScale;
    private float originalLength = 1f;
    private float releaseTime;
    private bool initialized;
    private bool waitingForRelease;

    private void Initialize()
    {
        if (initialized)
            return;

        initialized = true;
        originalScale = transform.localScale;

        SpriteRenderer renderer =
            GetComponentInChildren<SpriteRenderer>();

        if (renderer != null)
        {
            originalLength =
                Mathf.Max(
                    0.01f,
                    renderer.bounds.size.x
                );
        }
    }

    public void Show(
        Vector3 start,
        Vector3 end,
        float duration)
    {
        Initialize();

        Vector3 direction = end - start;
        float distance = direction.magnitude;

        transform.position =
            (start + end) * 0.5f;

        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x
            ) * Mathf.Rad2Deg;

        transform.rotation =
            Quaternion.Euler(0f, 0f, angle);

        transform.localScale =
            new Vector3(
                originalScale.x *
                distance / originalLength,

                originalScale.y,
                originalScale.z
            );

        releaseTime =
            Time.time + duration;

        waitingForRelease = true;
    }

    private void Update()
    {
        if (!waitingForRelease ||
            Time.time < releaseTime)
        {
            return;
        }

        waitingForRelease = false;
        GameObjectPool.Instance.Release(gameObject);
    }

    private void OnDisable()
    {
        waitingForRelease = false;
    }
}