using System.Collections.Generic;
using UnityEngine;

public sealed class HundredYearsIce : MonoBehaviour, IEngineerDirectSpellInstance
{
    [SerializeField] private ColdDebuff coldDebuff;
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private SpriteRenderer warningCircle;
    [SerializeField, Min(.1f)] private float radius = 2f;
    [SerializeField, Min(.1f)] private float duration = 5f;
    [SerializeField, Min(.05f)] private float tickInterval = .3f;
    [SerializeField] private LayerMask targetLayers = ~0;
    [SerializeField, Min(0f)] private float shakeDistance = .05f;
    [SerializeField, Min(0f)] private float shakeTime = .1f;
    [SerializeField, Min(.05f)] private float shakeCycle = .3f;
    [SerializeField, Min(.05f)] private float fadeFallTime = .45f;
    [SerializeField] private float fallDistance = 1f;

    readonly Collider2D[] hitBuffer = new Collider2D[32];
    readonly HashSet<GameObjectProperty> hitTargets = new HashSet<GameObjectProperty>();

    Color originalColor;
    Vector3 warningScale;
    Sprite defaultWarningSprite;
    Vector3 startPosition;
    int side;
    float startedAt, nextTick, endingAt;
    bool active, ending;

    void Awake()
    {
        if (!coldDebuff) coldDebuff = GetComponent<ColdDebuff>();
        if (!body) body = GetComponent<SpriteRenderer>();
        if (body) originalColor = body.color;
        if (warningCircle)
        {
            warningScale = warningCircle.transform.localScale;
            defaultWarningSprite = warningCircle.sprite;
        }
    }

    void OnEnable()
    {
        active = false;
        ending = false;
        if (body) body.color = originalColor;
        if (warningCircle) warningCircle.enabled = false;
    }

    public void Initialize(EngineerController caster, SpellDefinition definition, Vector3 target)
    {
        if (!caster || !coldDebuff || !IsPooled())
        {
            gameObject.SetActive(false);
            return;
        }

        transform.position = target;
        startPosition = target;
        side = caster.Side;
        startedAt = Time.time;
        nextTick = startedAt + tickInterval;
        if (warningCircle)
        {
            Sprite sprite = definition.ShowWarningCircle && ResourceManager.Instance
                ? ResourceManager.Instance.GetSprite(definition.WarningCircleKey) : null;
            warningCircle.sprite = sprite ? sprite : defaultWarningSprite;
            warningCircle.transform.localScale = warningScale * definition.WarningCircleScale;
            warningCircle.enabled = definition.ShowWarningCircle && warningCircle.sprite;
        }
        active = true;
    }

    void Update()
    {
        if (!active) return;

        float elapsed = Time.time - startedAt;
        if (!ending && elapsed >= duration)
        {
            ending = true;
            endingAt = Time.time;
        }

        if (ending)
        {
            EndVisual();
            return;
        }

        if (Time.time >= nextTick)
        {
            nextTick += tickInterval;
            ApplyCold();
        }

        float phase = Mathf.Repeat(elapsed, shakeCycle);
        transform.position = phase < shakeTime
            ? startPosition + Vector3.right * Mathf.Sin(elapsed * 80f) * shakeDistance
            : startPosition;
    }

    void ApplyCold()
    {
        int count = Physics2D.OverlapCircleNonAlloc(
            transform.position, radius, hitBuffer, targetLayers);
        hitTargets.Clear();

        for (int i = 0; i < count; i++)
        {
            GameObjectProperty target = hitBuffer[i].GetComponentInParent<GameObjectProperty>();
            if (!target || target.isDead || target.side == side || !hitTargets.Add(target)) continue;

            // 经生命框架统一入口施加冷霜（ApplyBuff + 登记 currentDebuff），禁止直加状态列表。
            CharacterHealth targetHealth = target.GetComponent<CharacterHealth>();
            if (targetHealth == null || !targetHealth.ApplyBuff(coldDebuff)) continue;
        }
    }

    void EndVisual()
    {
        float t = Mathf.Clamp01((Time.time - endingAt) / fadeFallTime);
        transform.position = startPosition + Vector3.down * (fallDistance * t);
        if (body)
        {
            Color color = originalColor;
            color.a *= 1f - t;
            body.color = color;
        }

        if (t >= 1f) GameObjectPool.Instance.Release(gameObject);
    }

    bool IsPooled() => GameObjectPool.Instance && GameObjectPool.Instance.GetPrefab(gameObject);
}
