using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Messenger of order（秩序信使）机制：
/// 攻击时（攻击状态电平上升沿）在攻击目标位置生成两片惩罚领域：
/// 1. attack punish（默认 5 秒）：领域内敌方每次发起攻击，受到一次信使攻击力伤害，
///    并获得一层“寒冷”（ColdDebuff，默认 7 秒）；
/// 2. move punish（默认 3 秒）：领域内敌方一旦移动，立即受到一次“麻痹”（ParalysisDebuff）。
///
/// 实现要点（规范内）：
/// - 领域为纯表现预制体（经 ResourceManager 资源键 + GameObjectPool 生成），
///   惩罚判定由本脚本协程驱动，领域预制体无需挂任何运行时组件；
/// - 判定经 Physics2D.OverlapCircleNonAlloc 定时扫描 + 攻击电平/位移差检测；
/// - 伤害经框架 ICollide.OnCollide 统一入口，状态经 CharacterHealth.ApplyBuff 统一入口；
/// - 单位死亡/回收时统一归还仍在场的领域，防止悬挂视觉。
/// </summary>
[RequireComponent(typeof(GameObjectProperty))]
public class MessengerOfOrder : BehaviourBase
{
    [Header("攻击惩罚领域")]
    [SerializeField, ResourceKey(typeof(GameObject))]
    private string attackPunishPrefabKey = "attack punish";
    [SerializeField, Min(0.1f)]
    private float attackPunishDuration = 5f;

    [Header("移动惩罚领域")]
    [SerializeField, ResourceKey(typeof(GameObject))]
    private string movePunishPrefabKey = "move punish";
    [SerializeField, Min(0.1f)]
    private float movePunishDuration = 3f;
    [SerializeField]
    private Vector3 moveZoneOffset = new Vector3(0f, 1.2f, 0f);   // 移动领域相对攻击领域的偏移（避免完全重叠）。

    [Header("判定参数")]
    [SerializeField, Min(0.1f)]
    private float punishRadius = 3f;            // 领域判定半径。
    [SerializeField, Min(0.05f)]
    private float punishTickInterval = 0.3f;    // 惩罚判定扫描间隔秒。
    [SerializeField, Min(0.1f)]
    private float coldDuration = 7f;            // 寒冷每层持续秒（7 秒）。
    [SerializeField, Min(0.01f)]
    private float moveThreshold = 0.05f;        // 位移超过该值视为一次移动。

    [Header("呼吸灯")]
    [SerializeField, Min(0.1f)]
    private float breathSpeed = 2f;             // 呼吸频率（每秒周期数）。
    [SerializeField, Range(0f, 1f)]
    private float breathMinAlpha = 0.35f;       // 呼吸透明度下限。
    [SerializeField, Range(0f, 1f)]
    private float breathMaxAlpha = 0.9f;        // 呼吸透明度上限。

    private static readonly Collider2D[] Hits = new Collider2D[64];           // 复用的扫描缓冲区。

    private GameObjectProperty _prop;
    private GameObject _attackPunishPrefab;     // 经 ResourceManager 解析的领域预制体缓存。
    private GameObject _movePunishPrefab;
    private ColdDebuff _cold;                   // 寒冷实例（层管理器按层独立计时）。
    private ParalysisDebuff _paralysis;         // 麻痹实例。
    private bool _attacking;                    // 攻击状态电平（上升沿触发领域生成）。
    private readonly List<GameObject> _activeZones = new List<GameObject>();   // 在场领域（回收时统一归还）。
    private readonly List<GameObjectProperty> _scanResults = new List<GameObjectProperty>(); // 扫描结果复用缓存。

    /// <summary>经 CharacterAI 调度初始化：依赖已在 Awake 缓存，此处仅兜底补齐。</summary>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (_prop == null)
            _prop = prop;
    }

    /// <summary>
    /// 攻击状态电平检测：上升沿时生成两片惩罚领域；被动不阻止后续 AI 行为。
    /// </summary>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (_prop == null || _prop.isDead)
            return false;

        if (_prop.isAttack && !_attacking)
        {
            _attacking = true;
            TrySpawnPunishZones();
        }
        else if (!_prop.isAttack)
        {
            _attacking = false;
        }

        return false;
    }

    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _cold = gameObject.AddComponent<ColdDebuff>();
        _cold.duration = coldDuration;
        _paralysis = gameObject.AddComponent<ParalysisDebuff>();
    }

    private void OnDisable()
    {
        // 单位死亡/回收：归还仍在场的领域。
        for (int i = _activeZones.Count - 1; i >= 0; i--)
        {
            if (_activeZones[i] != null)
                GameObjectPool.Instance.Release(_activeZones[i]);
        }
        _activeZones.Clear();
        _attacking = false;
    }

    /// <summary>
    /// 生成两片惩罚领域：攻击惩罚领域置于目标位置，移动惩罚领域按偏移错开；
    /// 排序层运行时补正到 Map 层（地表层，不遮挡兵种与建筑）。
    /// </summary>
    private void TrySpawnPunishZones()
    {
        if (ResourceManager.Instance == null)
            return;

        // 延迟补齐：键已进公共预载清单，正常对局首次攻击时即已就绪。
        if (_attackPunishPrefab == null && !string.IsNullOrEmpty(attackPunishPrefabKey))
            _attackPunishPrefab = ResourceManager.Instance.GetGameObject(attackPunishPrefabKey);
        if (_movePunishPrefab == null && !string.IsNullOrEmpty(movePunishPrefabKey))
            _movePunishPrefab = ResourceManager.Instance.GetGameObject(movePunishPrefabKey);

        Vector3 center = _prop.target != null
            ? _prop.target.transform.position
            : transform.position;

        if (_attackPunishPrefab != null)
        {
            GameObject zone = GameObjectPool.Instance.Get(_attackPunishPrefab);
            if (zone != null)
            {
                zone.transform.position = center;
                SetupZoneRender(zone);
                _activeZones.Add(zone);
                StartCoroutine(AttackPunishRoutine(zone));
            }
        }

        if (_movePunishPrefab != null)
        {
            GameObject zone = GameObjectPool.Instance.Get(_movePunishPrefab);
            if (zone != null)
            {
                zone.transform.position = center + moveZoneOffset;
                SetupZoneRender(zone);
                _activeZones.Add(zone);
                StartCoroutine(MovePunishRoutine(zone));
            }
        }
    }

    /// <summary>
    /// 把领域渲染补正到 Map 层（地表层）：置于兵种与建筑之下不遮挡，
    /// 并以呼吸灯透明度上限初始化颜色。
    /// </summary>
    private void SetupZoneRender(GameObject zone)
    {
        SpriteRenderer renderer = zone.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sortingLayerID = 1748779293;    // Map 层（地表层）。
            renderer.sortingOrder = 0;

            Color color = renderer.color;
            color.a = breathMaxAlpha;
            renderer.color = color;
        }
    }

    /// <summary>呼吸灯：每帧按正弦曲线驱动领域透明度。</summary>
    private void UpdateBreath(SpriteRenderer renderer)
    {
        if (renderer == null)
            return;

        float t = 0.5f + 0.5f * Mathf.Sin(Time.time * breathSpeed * Mathf.PI * 2f);
        Color color = renderer.color;
        color.a = Mathf.Lerp(breathMinAlpha, breathMaxAlpha, t);
        renderer.color = color;
    }

    /// <summary>扫描领域内的敌方单位（有生命组件、敌对阵营、存活）。</summary>
    private void ScanEnemies(Vector3 center, List<GameObjectProperty> results)
    {
        results.Clear();
        int count = Physics2D.OverlapCircleNonAlloc(center, punishRadius, Hits);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = Hits[i];
            if (hit == null)
                continue;

            GameObjectProperty candidate = hit.GetComponentInParent<GameObjectProperty>();
            if (candidate == null || candidate == _prop ||
                candidate.side == _prop.side || candidate.isDead)
                continue;

            if (candidate.GetComponent<CharacterHealth>() == null)
                continue;

            results.Add(candidate);
        }
    }

    /// <summary>
    /// 攻击惩罚领域：领域内敌方每次发起攻击（攻击电平上升沿）
    /// 受到一次信使攻击力伤害，并获得一层寒冷。
    /// 每帧驱动呼吸灯，惩罚判定按 tick 间隔执行。
    /// </summary>
    private IEnumerator AttackPunishRoutine(GameObject zone)
    {
        float deadline = Time.time + attackPunishDuration;
        var attackStates = new Dictionary<GameObject, bool>();   // 领域内单位的上一轮攻击状态。
        SpriteRenderer renderer = zone.GetComponentInChildren<SpriteRenderer>();
        float tickTimer = 0f;

        while (zone != null && Time.time < deadline)
        {
            UpdateBreath(renderer);

            tickTimer += Time.deltaTime;
            if (tickTimer < punishTickInterval)
            {
                yield return null;
                continue;
            }
            tickTimer = 0f;

            ScanEnemies(zone.transform.position, _scanResults);

            var inside = new HashSet<GameObject>();
            for (int i = 0; i < _scanResults.Count; i++)
            {
                GameObjectProperty enemy = _scanResults[i];
                GameObject go = enemy.gameObject;
                inside.Add(go);

                bool wasAttacking;
                attackStates.TryGetValue(go, out wasAttacking);

                if (enemy.isAttack && !wasAttacking)
                    PunishAttacker(enemy);

                attackStates[go] = enemy.isAttack;
            }

            RemoveStaleEntries(attackStates, inside);
            yield return null;
        }

        ReleaseZone(zone);
    }

    /// <summary>
    /// 移动惩罚领域：领域内敌方一旦发生位移，立即受到一次麻痹（每个单位只麻痹一次）。
    /// 每帧驱动呼吸灯，惩罚判定按 tick 间隔执行。
    /// </summary>
    private IEnumerator MovePunishRoutine(GameObject zone)
    {
        float deadline = Time.time + movePunishDuration;
        var lastPositions = new Dictionary<GameObject, Vector3>();
        var paralyzed = new HashSet<GameObject>();
        SpriteRenderer renderer = zone.GetComponentInChildren<SpriteRenderer>();
        float tickTimer = 0f;

        while (zone != null && Time.time < deadline)
        {
            UpdateBreath(renderer);

            tickTimer += Time.deltaTime;
            if (tickTimer < punishTickInterval)
            {
                yield return null;
                continue;
            }
            tickTimer = 0f;

            ScanEnemies(zone.transform.position, _scanResults);

            var inside = new HashSet<GameObject>();
            for (int i = 0; i < _scanResults.Count; i++)
            {
                GameObjectProperty enemy = _scanResults[i];
                GameObject go = enemy.gameObject;
                inside.Add(go);

                Vector3 pos = enemy.transform.position;
                Vector3 last;
                bool known = lastPositions.TryGetValue(go, out last);
                lastPositions[go] = pos;

                if (!known)
                    continue;

                if (!paralyzed.Contains(go) &&
                    Vector3.Distance(pos, last) >= moveThreshold)
                {
                    paralyzed.Add(go);
                    CharacterHealth enemyHealth = enemy.GetComponent<CharacterHealth>();
                    if (enemyHealth != null)
                        enemyHealth.ApplyBuff(_paralysis);
                }
            }

            RemoveStaleEntries(lastPositions, inside);
            yield return null;
        }

        ReleaseZone(zone);
    }

    /// <summary>清理已离开领域的记录，避免字典随战斗无限增长。</summary>
    private static void RemoveStaleEntries<T>(Dictionary<GameObject, T> records, HashSet<GameObject> inside)
    {
        var stale = new List<GameObject>();
        foreach (var key in records.Keys)
        {
            if (!inside.Contains(key))
                stale.Add(key);
        }
        for (int i = 0; i < stale.Count; i++)
            records.Remove(stale[i]);
    }

    /// <summary>
    /// 惩罚攻击者：经框架 ICollide 统一入口结算一次信使攻击力伤害，
    /// 并经生命框架统一入口施加一层寒冷。
    /// </summary>
    private void PunishAttacker(GameObjectProperty enemy)
    {
        CharacterHealth enemyHealth = enemy.GetComponent<CharacterHealth>();
        if (enemyHealth == null)
            return;

        Damage damage = Damage.DefaultDamage;
        damage.side = _prop.side;
        damage.source = gameObject;
        damage.target = enemy.gameObject;
        damage.initialDamage = _prop.atk;
        damage.repel = 0f;
        damage.type = DamageType.normal;

        enemyHealth.OnCollide(damage);
        enemyHealth.ApplyBuff(_cold);
    }

    /// <summary>归还一片领域并从在途列表移除。</summary>
    private void ReleaseZone(GameObject zone)
    {
        _activeZones.Remove(zone);
        if (zone != null)
            GameObjectPool.Instance.Release(zone);
    }
}
