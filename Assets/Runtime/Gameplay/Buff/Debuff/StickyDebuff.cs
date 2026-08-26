using System.Collections.Generic;
using UnityEngine;

public class StickyDebuff : BuffBase
{
    [Min(0.1f)]
    public float duration = 5f;

    public override float buffSustainTime => duration;
    public override bool isDeBuff => true;

    protected override bool ApplyBuffInternal(GameObjectProperty prop)
    {
        if (prop == null)
            return false;

        StickyState state =
            prop.GetComponent<StickyState>();

        if (state == null)
            state =
                prop.gameObject.AddComponent<StickyState>();

        state.AddLayer(this, duration);
        return true;
    }

    public override bool CancelBuff(GameObjectProperty prop)
    {
        if (prop == null)
            return false;

        StickyState state =
            prop.GetComponent<StickyState>();

        return state != null &&
               state.RemoveLayer(this);
    }
}

class StickyState : MonoBehaviour
{
    private class StickyLayer
    {
        public StickyDebuff source;
        public float expireTime;
    }

    private const int MaximumEffectiveLayers = 10;

    private static readonly Color AmberColor =
        new Color(1f, 0.65f, 0.05f, 1f);

    private readonly List<StickyLayer> layers =
        new List<StickyLayer>();

    private GameObjectProperty prop;
    private CharacterHealth health;
    private SpriteRenderer[] renderers;
    private Color[] originalColors;

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        health = GetComponent<CharacterHealth>();

        renderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        originalColors =
            new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].color;
    }

    public void AddLayer(
        StickyDebuff source,
        float duration)
    {
        int oldCount = layers.Count;

        layers.Add(new StickyLayer
        {
            source = source,
            expireTime = Time.time + duration
        });

        UpdateState(oldCount);
    }

    public bool RemoveLayer(StickyDebuff source)
    {
        for (int i = 0; i < layers.Count; i++)
        {
            if (layers[i].source != source)
                continue;

            RemoveAt(i);
            return true;
        }

        return false;
    }

    private void Update()
    {
        for (int i = layers.Count - 1; i >= 0; i--)
        {
            if (Time.time >= layers[i].expireTime)
                RemoveAt(i);
        }
    }

    /// <summary>判断该来源是否仍有剩余层（多层同实例时，仅最后一层结束时注销登记）。</summary>
    private bool HasRemainingLayer(StickyDebuff source)
    {
        for (int i = 0; i < layers.Count; i++)
            if (layers[i].source == source)
                return true;
        return false;
    }

    private void RemoveAt(int index)
    {
        int oldCount = layers.Count;
        StickyDebuff source = layers[index].source;

        layers.RemoveAt(index);

        // 仅当该来源不再有剩余层时注销登记（多层同实例不得提前摘除）。
        if (health != null && source != null && !HasRemainingLayer(source))
            health.RemoveBuff(source);

        UpdateState(oldCount);

        if (layers.Count == 0)
            Destroy(this);
    }

    private void UpdateState(int oldCount)
    {
        float oldMultiplier =
            GetSpeedMultiplier(oldCount);

        float newMultiplier =
            GetSpeedMultiplier(layers.Count);

        prop.moveSpeed *=
            newMultiplier / oldMultiplier;

        UpdateColor();
    }

    private float GetSpeedMultiplier(int count)
    {
        int effectiveLayers =
            Mathf.Min(count, MaximumEffectiveLayers);

        /*
         * 减速总值：
         * 10% + 9% + 8%……
         */
        float totalSlow =
            0.1f * effectiveLayers
            - 0.005f
            * effectiveLayers
            * (effectiveLayers - 1);

        return 1f - totalSlow;
    }

    private void UpdateColor()
    {
        float strength =
            Mathf.Clamp01(layers.Count * 0.1f);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            Color color = Color.Lerp(
                originalColors[i],
                AmberColor,
                strength
            );

            color.a = originalColors[i].a;
            renderers[i].color = color;
        }
    }

    private void OnDisable()
    {
        RestoreEverything();
    }

    private void RestoreEverything()
    {
        if (prop != null && layers.Count > 0)
        {
            prop.moveSpeed /=
                GetSpeedMultiplier(layers.Count);
        }

        layers.Clear();

        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].color =
                    originalColors[i];
        }
    }
}