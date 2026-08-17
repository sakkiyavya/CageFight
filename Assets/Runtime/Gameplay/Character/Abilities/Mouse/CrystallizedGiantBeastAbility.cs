using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Crystallized giant beast 机制：
/// 1. 每击杀一个单位：最大生命上限 +1%（按基础上限比例，平铺累加），
///    最多累计 +13% 基础上限，复活不清零。
/// 2. 每次生命归零：扣除 20% 基础上限（100-20-20-20-20-20=0），
///    随后“晶化”自身 4 秒（无敌且不能动），前 3 秒回血到满（血条逐帧增长），
///    第 4 秒恢复行动；上限被扣到 0 时播放死亡特效后彻底离场（销毁，不再复活）。
/// 3. 每次攻击：自身获得 1 层精准 + 1 层创伤（自我创伤推动下一次晶化）。
/// 4. 免疫配置的指定 Buff（如未来的“妄业之力”，把其实例拖入 immuneBuff 即可）。
/// 晶化期间显示水晶覆盖层视觉（呼吸效果，需配置 crystalSprite）。
/// 实现 IDeathReviver 与 IBuffImmunity，由 CharacterHealth 的既有管线询问。
/// </summary>
[RequireComponent(typeof(GameObjectProperty))]
[RequireComponent(typeof(CharacterHealth))]
public class CrystallizedGiantBeastAbility : MonoBehaviour, IDeathReviver, IBuffImmunity
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
    [SerializeField, Tooltip("水晶覆盖层贴图（可复用 CrystallizationDebuff 的 crystalTexture）")]
    private Sprite crystalSprite;
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
    private const float FinalDeathDuration = 1f;          // 彻底离场时的死亡特效秒。
    private const float FinalDeathFlySpeed = 4f;          // 死亡抛飞水平速度。
    private const float FinalDeathFlyUp = 20f;            // 死亡抛飞纵向初速。
    private const float FinalDeathGravity = -50f;         // 死亡抛飞纵向加速度。
    private const float FinalDeathSpin = 1440f;           // 死亡旋转角速度（度/秒）。

    private GameObjectProperty _prop;
    private CharacterHealth _health;
    private PreciseBuff _precise;
    private TraumaDebuff _trauma;
    private SpriteRenderer[] renderers;
    private Color[] originalColors;
    private SpriteRenderer crystalOverlay;      // 晶化覆盖层。
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
    }

    private void OnDisable()
    {
        if (_prop != null)
            _prop.OnAtt -= HandleAttacked;

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
    private void Update()
    {
        if (_prop == null || _prop.isDead || crystallizing)
            return;

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
        _prop.maxHp = Mathf.Max(0, baseMaxHp + totalKillBonus - totalDeathDeduct);
    }
    #endregion

    #region 死亡接管（IDeathReviver）
    /// <summary>
    /// 接管致命伤害：扣除 20% 基础上限；上限归零则播放死亡特效后彻底离场，
    /// 否则进入晶化复活（无敌、不能动、前 3 秒回满）。
    /// </summary>
    public bool TryRevive(GameObject unit, Damage lethalDamage)
    {
        if (_prop == null || crystallizing)
            return false;

        SnapshotBaseMaxHp();
        totalDeathDeduct += Mathf.Max(1, Mathf.RoundToInt(baseMaxHp * deathDeductPercent));
        RefreshMaxHp();

        if (_prop.maxHp <= 0)
        {
            // 上限归零：播放死亡特效后彻底离场（不进入晶化复活）。
            StartFinalDeath(lethalDamage);
            return true;
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
        _prop.isDead = true;
        _prop.currentHp = 0;
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

            // 前 healDuration 秒回血到满，并逐帧刷新血条。
            if (elapsed < healDuration)
            {
                _prop.currentHp = Mathf.RoundToInt(_prop.maxHp * Mathf.Clamp01(elapsed / healDuration));
                if (_health != null)
                    _health.SetHpbar();
            }

            UpdateCrystalBreathing();
            yield return null;
        }

        _prop.currentHp = _prop.maxHp;
        if (_health != null)
            _health.SetHpbar();

        RemoveCrystalOverlay();
        RestoreColors();
        _prop.isDead = false;
        crystallizing = false;
        crystalRoutine = null;
    }

    /// <summary>
    /// 播放死亡抛飞特效（与常规死亡一致的抛物线 + 旋转），结束后彻底销毁离场。
    /// </summary>
    private void StartFinalDeath(Damage lethalDamage)
    {
        _prop.isDead = true;
        _prop.isAttack = false;
        _prop.target = null;

        int direction = 1;
        if (lethalDamage.source != null)
        {
            float delta = transform.position.x - lethalDamage.source.transform.position.x;
            if (Mathf.Abs(delta) > 0.001f)
                direction = delta > 0f ? 1 : -1;
        }
        else if (lethalDamage.collideDir != 0)
        {
            direction = lethalDamage.collideDir > 0 ? 1 : -1;
        }

        StartCoroutine(FinalDeathRoutine(direction));
    }

    private IEnumerator FinalDeathRoutine(int direction)
    {
        Vector3 start = transform.position;
        Quaternion startRotation = transform.rotation;
        float elapsed = 0f;

        while (elapsed < FinalDeathDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed;

            transform.position = start + new Vector3(
                direction * FinalDeathFlySpeed * t,
                FinalDeathFlyUp * t + 0.5f * FinalDeathGravity * t * t,
                0f);
            transform.Rotate(Vector3.forward, FinalDeathSpin * Time.deltaTime);
            yield return null;
        }

        transform.position = start;
        transform.rotation = startRotation;
        Destroy(gameObject);
    }
    #endregion

    #region 攻击自我增益
    /// <summary>
    /// 每次攻击对自身施加一层精准与一层创伤。
    /// </summary>
    private void HandleAttacked()
    {
        if (_prop == null || _prop.isDead || crystallizing)
            return;

        _precise.ApplyBuff(_prop);
        _trauma.ApplyBuff(_prop);
    }
    #endregion

    #region Buff 免疫（IBuffImmunity）
    /// <summary>
    /// 免疫配置的 Buff 类型（如未来的“妄业之力”）。
    /// </summary>
    public bool IsImmuneTo(BuffBase buff)
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
    /// 创建水晶覆盖层（子级 SpriteRenderer），配置贴图、缩放、偏移与排序。
    /// </summary>
    private void CreateCrystalOverlay()
    {
        RemoveCrystalOverlay();

        if (crystalSprite == null || renderers == null || renderers.Length == 0)
            return;

        GameObject child = new GameObject("Crystal");
        child.transform.SetParent(transform, false);
        child.transform.localScale = Vector3.one * crystalScale;
        child.transform.localPosition = new Vector3(crystalOffset.x, crystalOffset.y, 0f);

        crystalOverlay = child.AddComponent<SpriteRenderer>();
        crystalOverlay.sprite = crystalSprite;
        crystalOverlay.sortingLayerID = renderers[0].sortingLayerID;
        crystalOverlay.sortingOrder = renderers[0].sortingOrder + 1;
    }

    /// <summary>
    /// 驱动水晶覆盖层的呼吸透明度。
    /// </summary>
    private void UpdateCrystalBreathing()
    {
        if (crystalOverlay == null)
            return;

        Color color = new Color(0.6f, 0.85f, 1f, 1f);
        color.a = 0.4f + Mathf.Sin(Time.time * 3f) * 0.15f;
        crystalOverlay.color = color;
    }

    private void RemoveCrystalOverlay()
    {
        if (crystalOverlay != null)
        {
            Destroy(crystalOverlay.gameObject);
            crystalOverlay = null;
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
