using System;
using UnityEngine;

/// <summary>
/// BOSS 攻击行为（行为树选择器，单组件）：挂入 CharacterAI.Behaviours 首位（替换普通平砍位置）。
/// 内含一组攻击槽（顺序即优先级；命名沿用 A/B/C）。每帧按“冷却 → 血量窗口 → 黑板标志 → 目标在射程内”
/// 选择第一个满足条件的攻击执行：经动画器 Bool（IsA/IsB/IsC）释放对应动画并接管本帧（返回 true 阻断后续行为）；
/// 攻击按槽位时长收尾（attackDuration 为 0 时自动读取动画片段实际长度），收尾后清除本槽 Bool 并放行，
/// 由下一个满足条件的攻击槽或索敌/移动继续 —— 即 A 冷却中则 B 接、A/B 冷却中则 C 接。
/// 弹幕：动画事件经根物体转发器（BossBarrageRelay）调用 FireBarrage，使用当前激活槽的
/// 弹幕资源键与伤害百分比（伤害 = 自身 atk × damagePercent%）。
/// </summary>
[DisallowMultipleComponent]
public sealed class BossAttackBehaviour : BehaviourBase
{
    [Serializable]
    public sealed class AttackSlot
    {
        [Tooltip("攻击动画状态名（沿用 A/B/C 命名）")]
        public string attackName = "A";
        [Min(0f), Tooltip("冷却（秒）")]
        public float cooldown = 3f;
        [Min(0), Tooltip("伤害 = 自身 atk × percent%")]
        public int damagePercent = 100;
        [Range(0, 100), Tooltip("仅当血量低于该百分比时可触发；0 = 不限")]
        public int hpBelowPercent = 0;
        [Range(0, 100), Tooltip("仅当血量高于该百分比时可触发；0 = 不限")]
        public int hpAbovePercent = 0;
        [Tooltip("黑板标志名（非空时要求该标志为 true 才能触发）")]
        public string requiresFlag = "";
        [Tooltip("触发后清除该标志（一次性触发用）")]
        public bool clearFlagOnFire = false;
        [Min(0f), Tooltip("本攻击动画时长（秒）；0 = 自动按片段实际长度收尾")]
        public float attackDuration = 0f;
        [ResourceKey(typeof(GameObject)), Tooltip("弹幕预制体资源键；为空时回退 GameObjectProperty.atkObj")]
        public string barragePrefabKey = "";
        [NonSerialized] public float lastCastTime = float.NegativeInfinity;
        [NonSerialized] public string resolvedStateName;
    }

    [Header("攻击槽（顺序即优先级；命名沿用 A/B/C）")]
    [SerializeField] private AttackSlot[] slots = new AttackSlot[]
    {
        new AttackSlot(),
        new AttackSlot(),
        new AttackSlot()
    };
    [SerializeField, Tooltip("诊断日志开关（仅排查时打开）")]
    private bool debugLog = false;

    private GameObjectProperty _prop;
    private Animator _animator;
    private Transform _shootPoint;
    private BossBlackboard _blackboard;
    private AttackSlot _activeSlot;
    private string _lastFailReason;

    private void OnEnable()
    {
        // 池化复用：清除激活攻击槽与全部冷却记录。
        _activeSlot = null;
        _lastFailReason = null;
        if (slots != null)
        {
            foreach (AttackSlot slot in slots)
            {
                if (slot != null)
                    slot.lastCastTime = float.NegativeInfinity;
            }
        }
    }

    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        _prop = prop;
        _animator = self.GetComponent<Animator>();
        _blackboard = self.GetComponent<BossBlackboard>();
        _shootPoint = self.transform.Find("ShootPoint");
        if (!_shootPoint)
            _shootPoint = self.transform.Find("Shoot Point");
        if (!_shootPoint)
            _shootPoint = self.transform;

        if (slots == null)
            return;

        foreach (AttackSlot slot in slots)
        {
            if (slot == null)
                continue;
            slot.resolvedStateName = ResolveStateName(self, slot.attackName);
            if (slot.attackDuration <= 0f)
                slot.attackDuration = ReadClipLength(slot);
        }
    }

    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        // 攻击进行中：到时长收尾前一直接管（无打断规则）。
        if (_activeSlot != null)
        {
            if (_activeSlot.attackDuration > 0f &&
                Time.time >= _activeSlot.lastCastTime + _activeSlot.attackDuration)
            {
                EndAttack();
            }
            else
            {
                return true;
            }
        }

        // 非攻击期：同步移动动画参数（有目标即移动，无目标待机）。
        if (_animator != null)
            _animator.SetBool("IsMoving", prop != null && prop.target != null);

        AttackSlot slot = SelectSlot();
        if (slot == null)
        {
            if (debugLog)
                LogFailReason();
            return false;
        }

        StartAttack(slot, prop);
        return true;
    }

    /// <summary>按优先级选择第一个条件满足的攻击槽；无满足返回 null。</summary>
    private AttackSlot SelectSlot()
    {
        if (_prop == null || _prop.target == null || slots == null)
            return null;

        foreach (AttackSlot slot in slots)
        {
            if (slot == null || string.IsNullOrEmpty(slot.attackName))
                continue;
            if (Time.time < slot.lastCastTime + slot.cooldown)
                continue;
            if (slot.hpBelowPercent > 0 && _prop.currentHp * 100f > _prop.maxHp * slot.hpBelowPercent)
                continue;
            if (slot.hpAbovePercent > 0 && _prop.currentHp * 100f < _prop.maxHp * slot.hpAbovePercent)
                continue;
            if (!string.IsNullOrEmpty(slot.requiresFlag) &&
                (_blackboard == null || !_blackboard.GetFlag(slot.requiresFlag)))
                continue;

            // 目标须位于攻击范围内（CharacterBase 每帧维护的 atkRangeMin/Max 世界网格矩形），
            // 否则先移动接近，不远程抢先开火。
            GameObject target = _prop.target;
            int targetX = (int)target.transform.position.x;
            int targetY = (int)target.transform.position.y;
            if (targetX < _prop.atkRangeMin.x || targetX > _prop.atkRangeMax.x ||
                targetY < _prop.atkRangeMin.y || targetY > _prop.atkRangeMax.y)
            {
                continue;
            }

            return slot;
        }
        return null;
    }

    /// <summary>开始指定槽位的攻击：置位对应动画 Bool（IsA/IsB/IsC）、关闭移动参数并记录开火时间。</summary>
    private void StartAttack(AttackSlot slot, GameObjectProperty prop)
    {
        _activeSlot = slot;
        slot.lastCastTime = Time.time;
        if (slot.clearFlagOnFire && _blackboard != null && !string.IsNullOrEmpty(slot.requiresFlag))
            _blackboard.SetFlag(slot.requiresFlag, false);

        if (prop != null)
            prop.isAttack = true;

        if (_animator != null)
        {
            _animator.SetBool("IsMoving", false);
            _animator.SetBool("IsA", slot.attackName == "A");
            _animator.SetBool("IsB", slot.attackName == "B");
            _animator.SetBool("IsC", slot.attackName == "C");
        }

        if (debugLog)
            Debug.Log($"[BossAttack:{slot.attackName}] 攻击开始 t={Time.time:F2}", this);
    }

    /// <summary>结束当前攻击：清除对应动画 Bool 与攻击标记，放行下一行为。</summary>
    private void EndAttack()
    {
        AttackSlot slot = _activeSlot;
        _activeSlot = null;
        if (slot == null)
            return;

        if (_prop != null)
            _prop.isAttack = false;
        if (_animator != null)
            _animator.SetBool("Is" + slot.attackName, false);

        if (debugLog)
            Debug.Log($"[BossAttack:{slot.attackName}] 攻击结束 t={Time.time:F2}", this);
    }

    /// <summary>诊断：未就绪原因变化时打印一次（避免逐帧刷屏）。</summary>
    private void LogFailReason()
    {
        string reason;
        if (_prop == null || _prop.target == null)
        {
            reason = "无目标";
        }
        else
        {
            reason = "冷却/条件/目标在攻击范围外";
        }

        if (reason == _lastFailReason)
            return;
        _lastFailReason = reason;
        Debug.Log($"[BossAttack] 未就绪 - {reason}", this);
    }

    /// <summary>解析实际可播放的动画状态名：优先攻击名本身（如 A），其次“角色名-攻击名”（如 BabaDoctor Y-7-A）。</summary>
    private string ResolveStateName(GameObject self, string name)
    {
        if (string.IsNullOrEmpty(name) || _animator == null)
            return name;

        string[] candidates = new string[]
        {
            name,
            self.name + "-" + name
        };
        foreach (string candidate in candidates)
        {
            if (_animator.HasState(0, Animator.StringToHash(candidate)))
                return candidate;
        }
        return name;
    }

    /// <summary>从动画控制器读取本攻击片段的实际长度（匹配状态名或攻击名）；读不到时回退 1 秒。</summary>
    private float ReadClipLength(AttackSlot slot)
    {
        if (_animator == null || _animator.runtimeAnimatorController == null)
            return 1f;

        foreach (AnimationClip clip in _animator.runtimeAnimatorController.animationClips)
        {
            if (clip == null)
                continue;
            if (clip.name == slot.resolvedStateName || clip.name == slot.attackName ||
                clip.name.EndsWith("-" + slot.attackName))
            {
                return clip.length;
            }
        }
        return 1f;
    }

    /// <summary>动画事件调用入口（转发器转发）：以当前激活攻击槽发射一枚弹幕。</summary>
    public void FireBarrage()
    {
        FireBarrage(_activeSlot);
    }

    /// <summary>以指定下标攻击槽发射一枚弹幕（0=A、1=B、2=C）。</summary>
    public void FireBarrage(int slotIndex)
    {
        AttackSlot slot = slotIndex >= 0 && slots != null && slotIndex < slots.Length
            ? slots[slotIndex]
            : null;
        FireBarrage(slot);
    }

    /// <summary>以指定攻击槽发射一枚弹幕（伤害 = 自身 atk × damagePercent%，弹幕取槽位资源键）。</summary>
    private void FireBarrage(AttackSlot slot)
    {
        if (_prop == null || slot == null)
            return;

        string key = string.IsNullOrEmpty(slot.barragePrefabKey) ? _prop.atkObj : slot.barragePrefabKey;
        GameObject prefab = ResourceManager.Instance != null
            ? ResourceManager.Instance.GetGameObject(key)
            : null;
        if (!prefab || GameObjectPool.Instance == null)
            return;

        GameObject projectile = GameObjectPool.Instance.Get(prefab);
        if (!projectile)
            return;

        projectile.transform.position = _shootPoint != null ? _shootPoint.position : transform.position;
        projectile.transform.right = _prop.isFacingLeft ? Vector3.left : Vector3.right;

        DamageSource ds = projectile.GetComponent<DamageSource>();
        if (ds != null)
        {
            ds.damage.initialDamage = Mathf.RoundToInt(_prop.atk * slot.damagePercent / 100f);
            ds.damage.source = gameObject;
            ds.damage.side = _prop.side;
            ds.damage.repel = _prop.repel;
            ds.target = _prop.target;
            ds.damage.type = DamageType.normal;
        }

        ISummonedUnit summoned = projectile.GetComponent<ISummonedUnit>();
        if (summoned != null)
            summoned.SetCreator(gameObject);

        _prop.OnAtt?.Invoke();
    }
}
