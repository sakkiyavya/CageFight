using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(DamageSource))]
public class SquashOnHitProjectile : MonoBehaviour
{
    [Header("触发条件")]
    [Min(0f)]
    public float repelLimit = 4f;

    [Range(0f, 1f)]
    public float triggerChance = 0.5f;

    [Header("压扁效果")]
    [Range(0f, 0.9f)]
    public float squashPercent = 0.2f;

    [Min(0.1f)]
    public float duration = 3f;

    private DamageSource damageSource;

    private readonly HashSet<GameObjectProperty> hitTargets =
        new HashSet<GameObjectProperty>();

    private void Awake()
    {
        damageSource = GetComponent<DamageSource>();
    }

    private void OnEnable()
    {
        hitTargets.Clear();

        if (damageSource == null)
            damageSource = GetComponent<DamageSource>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        GameObjectProperty target =
            other.GetComponent<GameObjectProperty>();

        CharacterHealth health =
            other.GetComponent<CharacterHealth>();

        if (target == null || health == null)
            return;

        if (health.IsDead())
            return;

        if (target.side == damageSource.damage.side)
            return;

        // 同一颗弹幕对同一目标只判定一次。
        if (!hitTargets.Add(target))
            return;

        // 必须严格低于限制值。
        if (target.repel >= repelLimit)
            return;

        if (Random.value > triggerChance)
            return;

        SquashedVisualState state =
            target.GetComponent<SquashedVisualState>();

        if (state == null)
        {
            state = target.gameObject
                .AddComponent<SquashedVisualState>();
        }

        state.AddLayer(
            duration,
            1f - squashPercent
        );
    }
}

class SquashedVisualState : MonoBehaviour
{
    private class SquashLayer
    {
        public float expireTime;
        public float multiplier;
    }

    private readonly List<SquashLayer> layers =
        new List<SquashLayer>();

    private readonly List<Transform> visualTransforms =
        new List<Transform>();

    private readonly List<float> originalScaleY =
        new List<float>();

    private void Awake()
    {
        HashSet<Transform> added =
            new HashSet<Transform>();

        SpriteRenderer[] renderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            Transform visual = renderer.transform;

            if (!added.Add(visual))
                continue;

            visualTransforms.Add(visual);
            originalScaleY.Add(visual.localScale.y);
        }
    }

    public void AddLayer(
        float duration,
        float multiplier)
    {
        layers.Add(new SquashLayer
        {
            expireTime = Time.time + duration,
            multiplier = Mathf.Clamp(
                multiplier,
                0.01f,
                1f
            )
        });
    }

    private void Update()
    {
        for (int i = layers.Count - 1; i >= 0; i--)
        {
            if (Time.time >= layers[i].expireTime)
                layers.RemoveAt(i);
        }

        if (layers.Count == 0)
        {
            RestoreVisual();
            Destroy(this);
        }
    }

    private void LateUpdate()
    {
        if (layers.Count == 0)
            return;

        float totalMultiplier = 1f;

        foreach (SquashLayer layer in layers)
            totalMultiplier *= layer.multiplier;

        for (int i = 0; i < visualTransforms.Count; i++)
        {
            Transform visual = visualTransforms[i];

            if (visual == null)
                continue;

            Vector3 scale = visual.localScale;

            scale.y =
                originalScaleY[i] * totalMultiplier;

            visual.localScale = scale;
        }
    }

    private void OnDisable()
    {
        RestoreVisual();
    }

    private void RestoreVisual()
    {
        for (int i = 0; i < visualTransforms.Count; i++)
        {
            Transform visual = visualTransforms[i];

            if (visual == null)
                continue;

            Vector3 scale = visual.localScale;
            scale.y = originalScaleY[i];
            visual.localScale = scale;
        }

        layers.Clear();
    }
}