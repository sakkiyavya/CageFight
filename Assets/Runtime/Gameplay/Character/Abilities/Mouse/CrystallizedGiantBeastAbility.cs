using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Crystallized giant beast 机制：
/// 1. 每击杀一个单位：最大生命上限 +1%（按基础上限比例，平铺累加），
///    最多累计 +13% 基础上限，复活不清零。
/// 2. 每次生命归零：扣除 20% 基础上限（100-20-20-20-20-20=0），
///    随后“晶化”自身 4 秒（无敌且不能动），前 3 秒回血到满（血条逐帧增长），
///    第 4 秒恢复行动；上限被扣到 0 时不再晶化复活，走框架常规死亡流程
///    （死亡抛飞 → 对象池回收，技能脚本不直接销毁宿主）。
/// 3. 每次攻击：自身获得 1 层精准 + 1 层创伤（自我创伤推动下一次晶化）。
/// 4. 免疫配置的指定 Buff（如未来的“妄业之力”，把其实例拖入 immuneBuff 即可）。
/// 晶化期间显示水晶覆盖层视觉（呼吸效果，需配置 crystalSprite）。
/// 实现死亡复活器与 Buff 免疫过滤（经 CharacterHealth 统一扩展点登记）。
/// </summary>
[RequireComponent(typeof(GameObjectProperty))]
[RequireComponent(typeof(CharacterHealth))]
public class CrystallizedGiantBeastAbility : BehaviourBase
{
    [Header("击杀成长")]
    [SerializeField, Min(0f)]
    private float killMaxHpPercent = 0.01f;     // 每击杀一个单位 +1% 基础上限。
    [SerializeField, Min(0f)]
    private float maxKillBonusPercent = 0.13f;  // 击杀累积上限：最多 +13% 基础上限，复活不清零，累计始终受此上限约束。

    [Header("晶化复活")]
    [SerializeField, Range(0f, 1f)]
    private float deathDeductPercent = 0.2f;    // 每次归零扣除基础上限的 20%。
    [SerializeField, Min(0.1f)]
    private float crystalDuration = 4f;         // 晶化持续秒（含回血阶段）。
    [SerializeField, Min(0.1f)]
    private float healDuration = 3f;            // 前几秒回血到满。

    [Header("晶化视觉")]
    [SerializeField, ResourceKey(typeof(Sprite)), Tooltip("水晶覆盖层贴图资源键（Buff1 AP_0）")]
    private string crystalSpriteKey = "Buff1 AP_0";
    [SerializeField, ResourceKey(typeof(GameObject)), Tooltip("水晶覆盖层视觉预制体资源键（UnitVisualFollower，池化生成）")]
    private string crystalVisualPrefabKey = "UnitVisualFollower";
    [SerializeField, Min(0.01f)]
    private float crystalScale = 1f;            // 覆盖层缩放。
    [SerializeField]
    private Vector2 crystalOffset = Vector2.zero; // 覆盖层偏移。

    [Header("攻击自我增益")]
    [SerializeField, Min(0.1f)]
    private float preciseDuration = 10f;        // 每层精准持续秒。
    [SerializeField, Min(0.1f)]
    private float traumaDuration = 5f;          // 每层创伤持续秒。

    [Header("Buff 免疫")]
    [SerializeField, Tooltip("免疫的 Buff 实例（如未来的妄业之力），把其实例拖入")]
    private BuffBase immuneBuff;

    private static readonly Color CrystalColor = new Color(0.65f, 0.85f, 1f, 1f);

    private GameObjectProperty _prop;
    private CharacterHealth _health;
    private PreciseBuff _precise;
    private TraumaDebuff _trauma;
    private SpriteRenderer[] renderers;
    private Color[] originalColors;
    private UnitVisualFollower crystalFollower; // 晶化覆盖层视觉（池化跟随对象）。
    private Sprite _crystalSprite;              // 经 ResourceManager 解析的水晶贴图缓存。
    private Coroutine crystalRoutine;

    private int baseMaxHp;                      // 基础上限（击杀加成与死亡扣减的基准）。
    private int totalKillBonus;                 // 累计击杀加成。
    private int totalDeathDeduct;               // 累计死亡扣减。
    private bool baseSnapshotted;               // 是否已快照基础上限。
    private GameObject lastTarget;              // 上一次攻击目标，用于击杀判定。
    private bool crystallizing;                 // 是否正在晶化。

    #region 生命周期与回调
    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _health = GetComponent<CharacterHealth>();
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].color;

        // 运行时创建并配置 Buff 实例，避免预制体额外挂载组件。
        _precise = gameObject.AddComponent<PreciseBuff>();
        _precise.SetDuration(preciseDuration);
        _trauma = gameObject.AddComponent<TraumaDebuff>();
        _trauma.SetDuration(traumaDuration);
    }

    private void OnEnable()
    {
        if (_prop != null)
            _prop.OnAtt += HandleAttacked;

        // 经生命框架统一扩展点登记死亡复活器与状态过滤器（OnDisable 对称注销）。
        if (_health != null)
        {
            _health.RegisterDeathReviver(TryRevive);
            _health.RegisterBuffFilter(IsImmuneTo);
        }
    }

    private void OnDisable()
    {
        if (_prop != null)
            _prop.OnAtt -= HandleAttacked;

        if (_health != null)
        {
            _health.UnregisterDeathReviver(TryRevive);
            _health.UnregisterBuffFilter(IsImmuneTo);
        }

        if (crystalRoutine != null)
        {
            StopCoroutine(crystalRoutine);
            crystalRoutine = null;
        }

        crystallizing = false;
        _prop.isDead = false;
        lastTarget = null;
        RemoveCrystalOverlay();
        RestoreColors();
    }
    #endregion

    #region 帧更新
    /// <summary>经 CharacterAI 调度初始化：依赖已在 Awake 缓存，此处仅兜底补齐。</summary>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (_prop == null)
            _prop = prop;
        if (_health == null)
            _health = health;
    }

    /// <summary>逐帧击杀判定；被动不阻止后续 AI 行为。</summary>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (_prop == null || _prop.isDead || crystallizing)
            return false;

        // 击杀判定：攻击目标发生变化时，若上一目标已死亡则计一次击杀。
        GameObject current = _prop.target;
        if (current != lastTarget)
        {
            if (lastTarget != null)
            {
                GameObjectProperty targetProp = lastTarget.GetComponent<GameObjectProperty>();
                if (targetProp != null && targetProp.isDead)
                    OnKill();
            }

            lastTarget = current;
        }

        return false;
    }
    #endregion

    #region 击杀成长
    /// <summary>
    /// 击杀一个单位：累计 +1% 基础上限的加成（不超过 13% 上限）并刷新最大生命。
    /// </summary>
    private void OnKill()
    {
        SnapshotBaseMaxHp();
        totalKillBonus += Mathf.Max(1, Mathf.RoundToInt(baseMaxHp * killMaxHpPercent));
        totalKillBonus = Mathf.Min(
            totalKillBonus,
            Mathf.Max(1, Mathf.RoundToInt(baseMaxHp * maxKillBonusPercent)));
        RefreshMaxHp();
    }

    private void RefreshMaxHp()
    {
        _health.SetMaxHp(Mathf.Max(0, baseMaxHp + totalKillBonus - totalDeathDeduct));
    }
    #endregion

    #region 死亡接管（CharacterHealth 死亡复活器）
    /// <summary>
    /// 接管致命伤害：扣除 20% 基础上限；上限归零则播放死亡特效后彻底离场，
    /// 否则进入晶化复活（无敌、不能动、前 3 秒回满）。
    /// </summary>
    private bool TryRevive(Damage lethalDamage)
    {
        if (_prop == null || crystallizing)
            return false;

        SnapshotBaseMaxHp();
        totalDeathDeduct += Mathf.Max(1, Mathf.RoundToInt(baseMaxHp * deathDeductPercent));
        RefreshMaxHp();

        if (_prop.maxHp <= 0)
        {
            // 上限归零：不再晶化复活。返回 false 交由 CharacterHealth 走常规死亡流程
            //（死亡抛飞 → 对象池回收，不再复活），技能脚本不直接销毁宿主对象。
            return false;
        }

        StartCrystalRevive();
        return true;
    }

    /// <summary>
    /// 进入晶化复活：标记死亡（无敌 + 敌人忽略 + AI 停止），
    /// 前 healDuration 秒回血到满（血条逐帧刷新），crystalDuration 秒后恢复行动，
    /// 期间显示水晶覆盖层视觉。
    /// </summary>
    private void StartCrystalRevive()
    {
        // 复活保留已累计的击杀加成（不清零），击杀加成始终受 13% 上限约束。
        crystallizing = true;
        _health.SetPercentHp(0f);   // 受控标记死亡（currentHp=0 + isDead=true），单位被敌人忽略。
        _prop.target = null;
        TintCrystal();
        CreateCrystalOverlay();

        if (crystalRoutine != null)
            StopCoroutine(crystalRoutine);

        crystalRoutine = StartCoroutine(CrystalRoutine());
    }

    private IEnumerator CrystalRoutine()
    {
        float elapsed = 0f;
        while (elapsed < crystalDuration)
        {
            elapsed += Time.deltaTime;

            // 前 healDuration 秒回血到满，并逐帧刷新血条（期间保持死亡标记不被翻转）。
            if (elapsed < healDuration)
            {
                if (_health != null)
                {
                    _health.SetHpKeepDeadState(Mathf.RoundToInt(_prop.maxHp * Mathf.Clamp01(elapsed / healDuration)));
                    _health.SetHpbar();
                }
            }

            yield return null;
        }

        if (_health != null)
        {
            _health.RestoreFullHp();   // 受控复活：满血 + 清除死亡标记（框架 API）。
            _health.SetHpbar();
        }

        RemoveCrystalOverlay();
        RestoreColors();
        crystallizing = false;
        crystalRoutine = null;
    }

    /// <summary>
    /// 上限归零的彻底离场走框架死亡流程（见 TryRevive 返回 false 的分支），
    /// 本类不再自定义死亡特效与销毁逻辑。
    /// </summary>
    #endregion

    #region 攻击自我增益
    /// <summary>
    /// 攻击自我增益：每次攻击对自身施加一层精准与一层创伤（统一状态入口登记）。
    /// </summary>
    private void HandleAttacked()
    {
        if (_prop == null || _prop.isDead || crystallizing)
            return;

        _health.ApplyBuff(_precise);
        _health.ApplyBuff(_trauma);
    }
    #endregion

    #region Buff 免疫（CharacterHealth 状态过滤器）
    /// <summary>
    /// 免疫配置的 Buff 类型（如未来的“妄业之力”）。
    /// </summary>
    private bool IsImmuneTo(BuffBase buff)
    {
        return immuneBuff != null && buff != null &&
               buff.GetType() == immuneBuff.GetType();
    }
    #endregion

    #region 内部辅助
    /// <summary>
    /// 首次触发成长/扣减前，以当前最大生命作为基础上限基准。
    /// </summary>
    private void SnapshotBaseMaxHp()
    {
        if (baseSnapshotted || _prop == null)
            return;

        baseMaxHp = _prop.maxHp;
        baseSnapshotted = true;
    }

    /// <summary>
    /// 创建水晶覆盖层：按资源键经对象池生成 UnitVisualFollower 绑定自身，
    /// 跟随、呼吸与回收由该组件统一管理（贴图键 Buff1 AP_0）。
    /// </summary>
    private void CreateCrystalOverlay()
    {
        RemoveCrystalOverlay();

        if (renderers == null || renderers.Length == 0)
            return;

        // 延迟补齐：资源未就绪时本次不显示覆盖层（键已进公共预载列表）。
        if (_crystalSprite == null && ResourceManager.Instance != null &&
            !string.IsNullOrEmpty(crystalSpriteKey))
            _crystalSprite = ResourceManager.Instance.GetSprite(crystalSpriteKey);

        if (_crystalSprite == null)
            return;

        GameObject prefab = ResourceManager.Instance.GetGameObject(crystalVisualPrefabKey);
        if (prefab == null)
            return;

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
            renderer.sprite = _crystalSprite;
            renderer.sortingLayerID = renderers[0].sortingLayerID;
            renderer.sortingOrder = renderers[0].sortingOrder + 1;
            renderer.color = new Color(0.6f, 0.85f, 1f, 1f);
        }

        go.transform.localScale = Vector3.one * crystalScale;
        follower.Init(gameObject, new Vector3(crystalOffset.x, crystalOffset.y, 0f),
            0.48f, 0.25f, 0.55f);
        crystalFollower = follower;
    }

    private void RemoveCrystalOverlay()
    {
        if (crystalFollower != null)
        {
            crystalFollower.Finish();
            crystalFollower = null;
        }
    }

    /// <summary>
    /// 晶化期间给主体叠加淡蓝染色。
    /// </summary>
    private void TintCrystal()
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            Color color = CrystalColor;
            color.a = originalColors[i].a;
            renderers[i].color = color;
        }
    }

    private void RestoreColors()
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].color = originalColors[i];
        }
    }
    #endregion
}
