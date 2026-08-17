using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class AttackShieldUnit : MonoBehaviour
{
    [Header("护盾属性")]
    [Range(0.01f, 1f)]
    public float shieldHpPercent = 0.1f;

    [Min(0.1f)]
    public float shieldDuration = 10f;

    [Header("护盾外观")]
    [Min(0.1f)]
    public float shieldSize = 1.5f;

    public Vector3 shieldOffset = Vector3.zero;

    private GameObjectProperty prop;
    private GameObject shieldObject;

    private int shieldHp;
    private float shieldExpireTime;
    private bool wasAttacking;

    private readonly Dictionary<GameObjectProperty, Action<Damage>>
        targetListeners =
            new Dictionary<GameObjectProperty, Action<Damage>>();

    private static Sprite generatedShieldSprite;

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        CreateShieldVisual();
    }

    private void OnEnable()
    {
        prop.OnAtt += OnAttack;
        prop.OnHitted += OnHitted;

        shieldHp = 0;
        wasAttacking = prop.isAttack;

        SetShieldVisible(false);
    }

    private void OnDisable()
    {
        prop.OnAtt -= OnAttack;
        prop.OnHitted -= OnHitted;

        foreach (var pair in targetListeners)
        {
            if (pair.Key != null)
                pair.Key.OnHitted -= pair.Value;
        }

        targetListeners.Clear();

        shieldHp = 0;
        SetShieldVisible(false);
    }

    private void Update()
    {
        // 即使攻击动画没有调用OnAtt，也能检测攻击状态。
        if (prop.isAttack && !wasAttacking)
            OnAttack();

        wasAttacking = prop.isAttack;

        if (shieldHp > 0 &&
            Time.time >= shieldExpireTime)
        {
            BreakShield();
        }
    }

    private void LateUpdate()
    {
        if (shieldObject == null ||
            !shieldObject.activeSelf)
        {
            return;
        }

        // 轻微呼吸效果。
        float pulse =
            1f + Mathf.Sin(Time.time * 4f) * 0.05f;

        shieldObject.transform.localScale =
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

    private void OnHitted(Damage damage)
    {
        if (shieldHp <= 0)
            return;

        if (Time.time >= shieldExpireTime)
        {
            BreakShield();
            return;
        }

        Damage calculated =
            DamageComputor.DamageCompute(damage);

        int absorbedDamage =
            Mathf.Min(
                shieldHp,
                Mathf.Max(0, calculated.finalDamage)
            );

        if (absorbedDamage <= 0)
            return;

        shieldHp -= absorbedDamage;

        /*
         * OnHitted在正式扣血之前执行。
         * 提前增加即将被护盾吸收的生命值，
         * 随后的TakeDamage会将其扣除。
         */
        prop.currentHp += absorbedDamage;

        if (shieldHp <= 0)
            BreakShield();
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

    private void CreateShieldVisual()
    {
        shieldObject =
            new GameObject("GeneratedYellowShield");

        shieldObject.transform.SetParent(
            transform,
            false
        );

        shieldObject.transform.localPosition =
            shieldOffset;

        shieldObject.transform.localScale =
            Vector3.one * shieldSize;

        SpriteRenderer shieldRenderer =
            shieldObject.AddComponent<SpriteRenderer>();

        shieldRenderer.sprite =
            GetGeneratedShieldSprite();

        SpriteRenderer referenceRenderer =
            FindReferenceRenderer();

        if (referenceRenderer != null)
        {
            shieldRenderer.sortingLayerID =
                referenceRenderer.sortingLayerID;

            shieldRenderer.sortingOrder =
                referenceRenderer.sortingOrder + 100;
        }
        else
        {
            shieldRenderer.sortingOrder = 100;
        }

        shieldRenderer.color = Color.white;
        shieldObject.SetActive(false);
    }

    private SpriteRenderer FindReferenceRenderer()
    {
        SpriteRenderer[] renderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        SpriteRenderer result = null;

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer.gameObject == shieldObject)
                continue;

            if (result == null ||
                renderer.sortingOrder >
                result.sortingOrder)
            {
                result = renderer;
            }
        }

        return result;
    }

    private void SetShieldVisible(bool visible)
    {
        if (shieldObject != null)
            shieldObject.SetActive(visible);
    }

    private static Sprite GetGeneratedShieldSprite()
    {
        if (generatedShieldSprite != null)
            return generatedShieldSprite;

        const int size = 128;

        Texture2D texture =
            new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false
            );

        texture.name = "GeneratedYellowShield";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center =
            new Vector2(
                (size - 1) * 0.5f,
                (size - 1) * 0.5f
            );

        float radius = size * 0.47f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance =
                    Vector2.Distance(
                        new Vector2(x, y),
                        center
                    ) / radius;

                float alpha = 0f;

                if (distance <= 1f)
                {
                    // 内部透明，边缘明亮。
                    alpha = distance >= 0.82f
                        ? 0.65f
                        : 0.13f;
                }

                texture.SetPixel(
                    x,
                    y,
                    new Color(
                        1f,
                        0.82f,
                        0.05f,
                        alpha
                    )
                );
            }
        }

        texture.Apply();

        generatedShieldSprite = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );

        generatedShieldSprite.name =
            "GeneratedYellowShieldSprite";

        return generatedShieldSprite;
    }
}