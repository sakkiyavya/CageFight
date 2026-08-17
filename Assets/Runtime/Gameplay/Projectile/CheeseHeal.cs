using System.Collections;
using UnityEngine;

/// <summary>
/// 奶酪范围治疗：在当前位置持续 lifetime 秒，每隔 healInterval 秒对半径内的友方单位
/// 恢复固定治疗量（施放时由死亡单位最大生命 × healPercent 注入）。
/// 被治疗单位每次被治疗时触发绿光闪烁 + 跳动表现（HealPulseVisual）。
/// 使用 NonAlloc 复用缓冲；到期后归还对象池。
/// </summary>
public class CheeseHeal : MonoBehaviour
{
    [Header("奶酪治疗")]
    [SerializeField, Min(0.1f)]
    private float lifetime = 5f;             // 存在持续秒。
    [SerializeField, Min(0.1f)]
    private float healInterval = 1f;         // 每次治疗的间隔秒。
    [SerializeField, Min(0.1f)]
    private float radius = 2f;               // 治疗半径（格）。
    [SerializeField, Range(0f, 1f)]
    private float healPercent = 0.05f;       // 治疗量 = 死亡单位最大生命 × 该比例。
    [SerializeField]
    private LayerMask allyMask = ~0;         // 参与治疗判定的层。

    private static readonly Collider2D[] hits = new Collider2D[64];    // 复用的治疗扫描缓冲。

    private int side;                        // 友方阵营，施放时注入。
    private int healAmount;                  // 每次治疗量，施放时注入。
    private float elapsed;                   // 距上次治疗的计时。
    private float lifetimeElapsed;           // 已存活时长。

    #region 生命周期与回调
    /// <summary>
    /// 注入阵营与每次治疗量（由死亡单位最大生命换算）。
    /// </summary>
    public void Init(int side, int maxHpSnapshot)
    {
        this.side = side;
        healAmount = Mathf.Max(1, Mathf.RoundToInt(maxHpSnapshot * healPercent));
    }

    private void OnEnable()
    {
        elapsed = 0f;
        lifetimeElapsed = 0f;
    }

    private void Update()
    {
        lifetimeElapsed += Time.deltaTime;
        if (lifetimeElapsed >= lifetime)
        {
            if (GameObjectPool.Instance != null)
                GameObjectPool.Instance.Release(gameObject);
            return;
        }

        elapsed += Time.deltaTime;
        if (elapsed < healInterval)
            return;

        elapsed = 0f;
        HealAllies();
    }
    #endregion

    #region 内部实现
    /// <summary>
    /// 扫描半径内的友方存活单位，逐一治疗并触发绿光跳动表现。
    /// </summary>
    private void HealAllies()
    {
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, radius, hits, allyMask);
        for (int i = 0; i < count; i++)
        {
            if (hits[i] == null)
                continue;

            GameObjectProperty prop = hits[i].GetComponent<GameObjectProperty>();
            if (prop == null || prop.isDead || prop.side != side)
                continue;

            CharacterHealth health = hits[i].GetComponent<CharacterHealth>();
            if (health == null)
                continue;

            health.Heal(healAmount);

            HealPulseVisual pulse = hits[i].GetComponent<HealPulseVisual>();
            if (pulse == null)
                pulse = hits[i].gameObject.AddComponent<HealPulseVisual>();
            pulse.Play();
        }
    }
    #endregion
}

/// <summary>
/// 被治疗时的绿光闪烁 + 跳动表现；由 CheeseHeal 在治疗时触发。
/// </summary>
internal class HealPulseVisual : MonoBehaviour
{
    private const float Duration = 0.3f;                        // 表现持续秒。
    private static readonly Color HealGreen = new Color(0.2f, 1f, 0.3f, 1f);

    private SpriteRenderer[] renderers;
    private Color[] originalColors;
    private Vector3 baseScale;
    private Coroutine routine;

    private void Awake()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].color;
        baseScale = transform.localScale;
    }

    /// <summary>触发一次绿光闪烁与跳动；重复触发会重启表现。</summary>
    public void Play()
    {
        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(PulseRoutine());
    }

    private IEnumerator PulseRoutine()
    {
        float elapsed = 0f;
        while (elapsed < Duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / Duration;
            float strength = (1f - t) * 0.6f;
            float wave = Mathf.Sin(t * Mathf.PI * 2.5f) * (1f - t);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                Color color = Color.Lerp(originalColors[i], HealGreen, strength);
                color.a = originalColors[i].a;
                renderers[i].color = color;
            }

            transform.localScale = baseScale * (1f + wave * 0.15f);
            yield return null;
        }

        Restore();
        routine = null;
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        Restore();
    }

    private void Restore()
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].color = originalColors[i];
        }

        if (baseScale != Vector3.zero)
            transform.localScale = baseScale;
    }
}
