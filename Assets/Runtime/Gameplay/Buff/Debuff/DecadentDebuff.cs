using System.Collections.Generic;
using UnityEngine;

public class DecadentDebuff : BuffBase
{
    [Min(0.1f)]
    public float duration = 5f;

    public override float buffSustainTime => duration;
    public override bool isDeBuff => true;

    public override bool ApplyBuff(GameObjectProperty prop)
    {
        if (prop == null)
            return false;

        DecadentState state =
            prop.GetComponent<DecadentState>();

        if (state == null)
        {
            state =
                prop.gameObject.AddComponent<DecadentState>();
        }

        state.AddLayer(this, duration);
        return true;
    }

    public override bool CancelBuff(GameObjectProperty prop)
    {
        if (prop == null)
            return false;

        DecadentState state =
            prop.GetComponent<DecadentState>();

        return state != null &&
               state.RemoveLayer(this);
    }
}

class DecadentState : MonoBehaviour
{
    private class DecadentLayer
    {
        public DecadentDebuff source;
        public float expireTime;
    }

    private const int MaximumEffectiveLayers = 8;

    private readonly List<DecadentLayer> layers =
        new List<DecadentLayer>();

    private GameObjectProperty prop;

    private SpriteRenderer[] renderers;
    private Color[] originalColors;

    private int baseAttack;
    private int appliedAttackReduction;

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        baseAttack = prop.atk;

        renderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        originalColors =
            new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].color;
    }

    public void AddLayer(
        DecadentDebuff source,
        float duration)
    {
        layers.Add(new DecadentLayer
        {
            source = source,
            expireTime = Time.time + duration
        });

        UpdateState();
    }

    public bool RemoveLayer(
        DecadentDebuff source)
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

    private void RemoveAt(int index)
    {
        DecadentDebuff source =
            layers[index].source;

        layers.RemoveAt(index);

        if (prop != null && source != null)
            prop.currentDebuff.Remove(source);

        UpdateState();

        if (layers.Count == 0)
            Destroy(this);
    }

    private void UpdateState()
    {
        UpdateAttack();
        UpdateColor();
    }

    private void UpdateAttack()
    {
        float totalReduction =
            GetAttackReduction(layers.Count);

        int newReduction =
            Mathf.RoundToInt(
                baseAttack * totalReduction
            );

        /*
         * 先还原上一次由本Debuff扣除的攻击力，
         * 再应用当前层数对应的削弱。
         */
        prop.atk += appliedAttackReduction;
        prop.atk -= newReduction;

        prop.atk = Mathf.Max(0, prop.atk);
        appliedAttackReduction = newReduction;
    }

    private float GetAttackReduction(int count)
    {
        int effectiveLayers =
            Mathf.Min(
                count,
                MaximumEffectiveLayers
            );

        /*
         * 8% + 7% + 6%……
         * 8层时总计36%。
         */
        return
            0.08f * effectiveLayers
            - 0.005f
            * effectiveLayers
            * (effectiveLayers - 1);
    }

    private void UpdateColor()
    {
        int effectiveLayers =
            Mathf.Min(
                layers.Count,
                MaximumEffectiveLayers
            );

        float darkness =
            effectiveLayers * 0.1f;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            Color color = Color.Lerp(
                originalColors[i],
                Color.black,
                darkness
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
        if (prop != null)
        {
            prop.atk += appliedAttackReduction;
            appliedAttackReduction = 0;
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