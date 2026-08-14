using System.Collections.Generic;
using UnityEngine;

public class ColdDebuff : BuffBase
{
    [Min(0.1f)]
    public float duration = 5f;

    public override float buffSustainTime => duration;
    public override bool isDeBuff => true;

    public override bool ApplyBuff(GameObjectProperty prop)
    {
        if (prop == null)
            return false;

        ColdState state = prop.GetComponent<ColdState>();

        if (state == null)
            state = prop.gameObject.AddComponent<ColdState>();

        state.AddLayer(this, duration);
        return true;
    }

    public override bool CancelBuff(GameObjectProperty prop)
    {
        if (prop == null)
            return false;

        ColdState state = prop.GetComponent<ColdState>();

        return state != null && state.RemoveLayer(this);
    }
}

class ColdState : MonoBehaviour
{
    private class ColdLayer
    {
        public ColdDebuff source;
        public float expireTime;
    }

    private const float SlowPerLayer = 0.07f;
    private const float ColorPerLayer = 0.1f;
    private const float MinimumSpeed = 0.1f;
    private const int FreezeLayers = 6;

    private static readonly Color ColdColor =
        new Color(0.1f, 0.85f, 1f, 1f);

    private readonly List<ColdLayer> layers =
        new List<ColdLayer>();

    private GameObjectProperty prop;
    private CharacterAI characterAI;
    private Animator animator;
    private Rigidbody2D body;

    private SpriteRenderer[] renderers;
    private Color[] originalColors;

    private bool frozen;
    private bool aiWasEnabled;
    private float originalAnimatorSpeed = 1f;

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        characterAI = GetComponent<CharacterAI>();
        animator = GetComponent<Animator>();
        body = GetComponent<Rigidbody2D>();

        renderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        originalColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].color;

        if (animator != null)
            originalAnimatorSpeed = animator.speed;
    }

    public void AddLayer(
        ColdDebuff source,
        float duration)
    {
        int oldCount = layers.Count;

        layers.Add(new ColdLayer
        {
            source = source,
            expireTime = Time.time + duration
        });

        UpdateState(oldCount);
    }

    public bool RemoveLayer(ColdDebuff source)
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

        if (frozen && body != null)
        {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    private void RemoveAt(int index)
    {
        int oldCount = layers.Count;
        ColdDebuff source = layers[index].source;

        layers.RemoveAt(index);

        if (prop != null && source != null)
            prop.currentDebuff.Remove(source);

        UpdateState(oldCount);

        if (layers.Count == 0)
            Destroy(this);
    }

    private void UpdateState(int oldCount)
    {
        UpdateSpeed(oldCount, layers.Count);
        UpdateColor();
        UpdateFreeze();
    }

    private void UpdateSpeed(
        int oldCount,
        int newCount)
    {
        float oldMultiplier = GetMultiplier(oldCount);
        float newMultiplier = GetMultiplier(newCount);

        float change =
            newMultiplier / oldMultiplier;

        prop.moveSpeed *= change;
        prop.atkRate *= change;
    }

    private float GetMultiplier(int count)
    {
        return Mathf.Max(
            MinimumSpeed,
            1f - SlowPerLayer * count
        );
    }

    private void UpdateColor()
    {
        float strength =
            Mathf.Clamp01(
                layers.Count * ColorPerLayer
            );

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            Color color = Color.Lerp(
                originalColors[i],
                ColdColor,
                strength
            );

            color.a = originalColors[i].a;
            renderers[i].color = color;
        }
    }

    private void UpdateFreeze()
    {
        if (layers.Count >= FreezeLayers)
            Freeze();
        else
            Unfreeze();
    }

    private void Freeze()
    {
        if (frozen)
            return;

        frozen = true;
        prop.isAttack = false;

        if (characterAI != null)
        {
            aiWasEnabled = characterAI.enabled;
            characterAI.enabled = false;
        }

        if (animator != null)
        {
            originalAnimatorSpeed = animator.speed;
            animator.SetBool("IsAtt", false);
            animator.speed = 0f;
        }

        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    private void Unfreeze()
    {
        if (!frozen)
            return;

        frozen = false;

        if (characterAI != null && aiWasEnabled)
            characterAI.enabled = true;

        if (animator != null)
            animator.speed = originalAnimatorSpeed;
    }

    private void OnDisable()
    {
        RestoreEverything();
    }

    private void RestoreEverything()
    {
        Unfreeze();

        if (prop != null && layers.Count > 0)
        {
            float multiplier =
                GetMultiplier(layers.Count);

            prop.moveSpeed /= multiplier;
            prop.atkRate /= multiplier;
        }

        layers.Clear();

        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].color = originalColors[i];
        }
    }
}