using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightning Cat（闪电猫）机制：攻击动画事件 CastLightning 触发——
/// 对主目标与攻击范围内最近的第二个敌方单位各结算一次电击伤害（经框架
/// ICollide.OnCollide 统一入口），并在发射点与目标之间各展示一条电柱视觉。
///
/// 重写要点（电柱不显示问题的最终修复）：
/// 1. 电柱展示改为本脚本协程驱动：从对象池取电柱预制体后，直接完成
///    定位/旋转/拉伸/定时回收，不依赖预制体上序列化挂载的运行时组件，
///    杜绝“预制体反序列化出错误组件”一类挂载问题；
/// 2. 电柱预制体已登记进 Addressables 分组（Remote Cat bullet）并纳入
///    公共预载清单，进入对局即就绪；
/// 3. 记录在展示中的电柱，单位死亡/回收时统一归还，防止悬挂视觉。
/// </summary>
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
    private ParalysisDebuff paralysisBuff;
    private BuffBase[] _paralysisBuffs;          // 预构建的麻痹 Buff 数组（避免每次攻击分配）。
    private GameObject _lightningPrefab;         // 经 ResourceManager 解析的电柱预制体缓存。
    private readonly HashSet<GameObject> nearbyUnits =
        new HashSet<GameObject>();
    private readonly List<GameObject> _activeBeams =
        new List<GameObject>();                  // 仍在展示中的电柱（死亡/回收时统一归还）。

    /// <summary>经 CharacterAI 调度初始化：依赖已在 Awake 缓存，此处仅兜底补齐。</summary>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (this.prop == null)
            this.prop = prop;
    }

    /// <summary>闪电由攻击动画事件驱动，无每帧行为；返回 false 放行后续 AI。</summary>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        return false;
    }

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        paralysisBuff = GetComponent<ParalysisDebuff>();

        if (paralysisBuff != null)
            _paralysisBuffs = new BuffBase[] { paralysisBuff };

        if (shootPoint == null)
            shootPoint = transform.Find("ShootPoint");
    }

    private void OnDisable()
    {
        // 单位死亡/回收：归还仍在展示的电柱，防止悬挂在场景中。
        for (int i = _activeBeams.Count - 1; i >= 0; i--)
        {
            if (_activeBeams[i] != null)
                GameObjectPool.Instance.Release(_activeBeams[i]);
        }
        _activeBeams.Clear();
    }

    /// <summary>
    /// 攻击动画事件入口：对主目标与范围内第二目标各结算一次电击，
    /// 并各展示一条电柱；结束后发布攻击事件（供被动技能响应）。
    /// </summary>
    public void CastLightning()
    {
        if (prop == null || prop.isDead || prop.target == null)
            return;

        GameObjectProperty firstTarget =
            prop.target.GetComponent<GameObjectProperty>();

        if (!IsValidEnemy(firstTarget))
            return;

        AttackTarget(firstTarget);
        AttackTarget(FindSecondTarget(firstTarget));

        prop.OnAtt?.Invoke();
    }

    /// <summary>
    /// 对单个目标结算电击伤害（经框架 ICollide 统一入口）并展示电柱视觉。
    /// </summary>
    private void AttackTarget(GameObjectProperty target)
    {
        if (!IsValidEnemy(target))
            return;

        CharacterHealth targetHealth =
            target.GetComponent<CharacterHealth>();

        if (targetHealth == null)
            return;

        Damage damage = Damage.DefaultDamage;

        damage.side = prop.side;
        damage.source = gameObject;
        damage.target = target.gameObject;
        damage.initialDamage = prop.atk;
        damage.repel = prop.repel;
        damage.type = DamageType.normal;
        damage.buffs = _paralysisBuffs;

        targetHealth.OnCollide(damage);

        Vector3 start = shootPoint != null
            ? shootPoint.position
            : transform.position;

        ShowLightningBeam(start, target.transform.position);
    }

    /// <summary>
    /// 在范围内查找除主目标外最近的合法敌方单位作为第二目标。
    /// </summary>
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

    /// <summary>
    /// 展示电柱：按资源键解析池化预制体，从对象池取实例，再由协程完成
    /// 定位、拉伸与定时回收（不依赖预制体上挂载的运行时组件）。
    /// </summary>
    private void ShowLightningBeam(Vector3 start, Vector3 end)
    {
        if (ResourceManager.Instance == null)
            return;

        // 延迟补齐：键已进公共预载清单，正常对局首次攻击时即已就绪。
        if (_lightningPrefab == null && !string.IsNullOrEmpty(lightningPrefabKey))
            _lightningPrefab = ResourceManager.Instance.GetGameObject(lightningPrefabKey);

        if (_lightningPrefab == null)
            return;

        GameObject beam =
            GameObjectPool.Instance.Get(
                _lightningPrefab
            );

        if (beam == null)
            return;

        _activeBeams.Add(beam);
        StartCoroutine(BeamLifetime(beam, start, end));
    }

    /// <summary>
    /// 电柱生命周期：按两点定位旋转，X 轴按“距离 ÷ 精灵固有长度”拉伸，
    /// 持续 lightningDuration 秒后归还对象池。
    /// </summary>
    private IEnumerator BeamLifetime(GameObject beam, Vector3 start, Vector3 end)
    {
        Vector3 direction = end - start;
        float distance = direction.magnitude;

        if (distance <= 0.0001f)
        {
            ReleaseBeam(beam);
            yield break;
        }

        // 精灵固有包围盒长度与当前缩放无关，是拉伸换算的可靠基准。
        float spriteLength = 1f;
        SpriteRenderer renderer = beam.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null && renderer.sprite != null)
            spriteLength = Mathf.Max(0.01f, renderer.sprite.bounds.size.x);

        beam.transform.position = (start + end) * 0.5f;
        beam.transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        beam.transform.localScale =
            new Vector3(distance / spriteLength, 1f, 1f);

        if (renderer != null)
        {
            renderer.sortingLayerID = 495858691;    // OnMap 层。
            renderer.sortingOrder = 1;              // 盖在单位身体（order 0）之上。
        }

        yield return new WaitForSeconds(Mathf.Max(0.01f, lightningDuration));
        ReleaseBeam(beam);
    }

    /// <summary>归还一条电柱并从在途列表移除。</summary>
    private void ReleaseBeam(GameObject beam)
    {
        _activeBeams.Remove(beam);
        if (beam != null)
            GameObjectPool.Instance.Release(beam);
    }
}
