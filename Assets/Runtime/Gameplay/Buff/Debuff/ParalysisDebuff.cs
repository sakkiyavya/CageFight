using System.Collections.Generic;
using UnityEngine;

public class ParalysisDebuff : BuffBase
{
    public override float buffSustainTime => 0.5f;
    public override bool isDeBuff => true;

    public override bool ApplyBuff(GameObjectProperty prop)
    {
        if (prop == null || prop.isDead)
            return false;

        ParalysisState state =
            prop.GetComponent<ParalysisState>();

        if (state == null)
        {
            state =
                prop.gameObject.AddComponent<ParalysisState>();
        }

        state.Apply(this, buffSustainTime);
        return true;
    }

    public override bool CancelBuff(GameObjectProperty prop)
    {
        if (prop == null)
            return false;

        ParalysisState state =
            prop.GetComponent<ParalysisState>();

        if (state == null)
            return false;

        state.EndParalysis();
        return true;
    }
}

class ParalysisState : MonoBehaviour
{
    private const float FlashInterval = 0.06f;

    private readonly List<ParalysisDebuff> sources =
        new List<ParalysisDebuff>();

    private GameObjectProperty prop;
    private CharacterAI characterAI;
    private Animator animator;
    private Rigidbody2D body;

    private SpriteRenderer[] renderers;
    private Color[] originalColors;

    private float endTime;
    private float originalAnimatorSpeed;
    private bool aiWasEnabled;
    private bool active;

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
    }

    public void Apply(
        ParalysisDebuff source,
        float duration)
    {
        if (source != null)
            sources.Add(source);

        endTime = Time.time + duration;

        if (active)
            return;

        active = true;

        if (characterAI != null)
        {
            aiWasEnabled = characterAI.enabled;
            characterAI.enabled = false;
        }

        if (animator != null)
        {
            originalAnimatorSpeed = animator.speed;
            animator.speed = 0f;
        }

        StopBody();
    }

    private void Update()
    {
        if (!active)
            return;

        StopBody();
        UpdateFlash();

        if (Time.time >= endTime)
            EndParalysis();
    }

    private void StopBody()
    {
        if (body == null)
            return;

        body.velocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    private void UpdateFlash()
    {
        bool showWhite =
            Mathf.FloorToInt(
                Time.time / FlashInterval
            ) % 2 == 0;

        Color flashColor =
            showWhite ? Color.white : Color.black;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            flashColor.a = originalColors[i].a;
            renderers[i].color = flashColor;
        }
    }

    public void EndParalysis()
    {
        if (!active)
            return;

        active = false;

        if (characterAI != null && aiWasEnabled)
            characterAI.enabled = true;

        if (animator != null)
            animator.speed = originalAnimatorSpeed;

        RestoreColors();
        RemoveDebuffReferences();

        Destroy(this);
    }

    private void RestoreColors()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].color =
                    originalColors[i];
        }
    }

    private void RemoveDebuffReferences()
    {
        if (prop == null)
            return;

        foreach (ParalysisDebuff source in sources)
        {
            if (source == null)
                continue;

            while (prop.currentDebuff.Remove(source))
            {
            }
        }

        sources.Clear();
    }

    private void OnDisable()
    {
        if (!active)
            return;

        active = false;

        if (characterAI != null && aiWasEnabled)
            characterAI.enabled = true;

        if (animator != null)
            animator.speed = originalAnimatorSpeed;

        RestoreColors();
    }
}