using System.Collections.Generic;
using UnityEngine;

public class ColdDebuff : BuffBase
{
    [Min(0.1f)]
    public float duration = 5f;

    [Header("获得音效（仅首次施加触发，叠加不触发）")]
    [SerializeField, ResourceKey(typeof(AudioClip))]
    private string buffSoundKey = "Cold dbuff"; // 首次获得寒冷时播放的音频资源键。
    [SerializeField, Range(0f, 1f)]
    private float buffSoundVolume = 1f;         // 获得音效音量。
    [SerializeField, Range(0, 256)]
    private int buffSoundPriority = 32;         // 获得音效优先级（越小越高）。

    public override float buffSustainTime => duration;
    public override bool isDeBuff => true;

    /// <summary>获得音效资源键，供层管理器读取。</summary>
    public string BuffSoundKey => buffSoundKey;
    /// <summary>获得音效音量，供层管理器读取。</summary>
    public float BuffSoundVolume => buffSoundVolume;
    /// <summary>获得音效优先级，供层管理器读取。</summary>
    public int BuffSoundPriority => buffSoundPriority;

    protected override bool ApplyBuffInternal(GameObjectProperty prop)
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
    private const int FreezeLayers = 10;    // 冻结所需寒冷层数（10 层触发）。

    private static readonly Color ColdColor =
        new Color(0.1f, 0.85f, 1f, 1f);

    private readonly List<ColdLayer> layers =
        new List<ColdLayer>();

    private GameObjectProperty prop;
    private CharacterHealth health;
    private Animator animator;

    private SpriteRenderer[] renderers;
    private Color[] originalColors;

    private bool frozen;
    private AudioSource soundAudio;            // 获得音效音频源（首层施加时解析）。
    private string soundKey = "Cold dbuff";
    private float soundVolume = 1f;
    private int soundPriority = 32;
    private bool warnedMissingSound;           // 是否已输出过获得音效缺失警告（一次性）。

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        health = GetComponent<CharacterHealth>();
        animator = GetComponent<Animator>();

        renderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        originalColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].color;
    }

    /// <summary>
    /// 解析获得音效音频源：优先复用对象上的 AudioSource，没有则新建一个。
    /// </summary>
    private void ResolveSoundAudio()
    {
        if (soundAudio != null)
            return;

        soundAudio = GetComponent<AudioSource>();
        if (soundAudio == null)
        {
            soundAudio = gameObject.AddComponent<AudioSource>();
            soundAudio.playOnAwake = false;
            soundAudio.spatialBlend = 0f;
        }
    }

    public void AddLayer(
        ColdDebuff source,
        float duration)
    {
        int oldCount = layers.Count;

        bool isFirstLayer = oldCount == 0;
        if (isFirstLayer)
        {
            soundKey = source.BuffSoundKey;
            soundVolume = source.BuffSoundVolume;
            soundPriority = source.BuffSoundPriority;
            ResolveSoundAudio();
        }

        layers.Add(new ColdLayer
        {
            source = source,
            expireTime = Time.time + duration
        });

        UpdateState(oldCount);

        if (isFirstLayer)
            PlayBuffSound();
    }

    /// <summary>
    /// 播放首次获得寒冷音效；资源键或片段缺失时输出一次性警告，避免静默失败。
    /// </summary>
    private void PlayBuffSound()
    {
        if (soundAudio == null || prop == null ||
            AudioManager.Instance == null || ResourceManager.Instance == null ||
            string.IsNullOrEmpty(soundKey))
            return;

        AudioClip clip = ResourceManager.Instance.GetAudio(soundKey);
        if (clip == null)
        {
            if (!warnedMissingSound)
            {
                warnedMissingSound = true;
                Debug.LogWarning($"[ColdDebuff] 音频资源 {soundKey} 未加载，获得音效无法播放。", this);
            }
            return;
        }

        soundAudio.clip = clip;
        soundAudio.volume = soundVolume;
        soundAudio.priority = soundPriority;
        AudioManager.Instance.PlayEffectAt(
            soundAudio,
            (uint)soundPriority,
            prop.transform);
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
    }

    /// <summary>判断该来源是否仍有剩余层（多层同实例时，仅最后一层结束时注销登记）。</summary>
    private bool HasRemainingLayer(ColdDebuff source)
    {
        for (int i = 0; i < layers.Count; i++)
            if (layers[i].source == source)
                return true;
        return false;
    }

    private void RemoveAt(int index)
    {
        int oldCount = layers.Count;
        ColdDebuff source = layers[index].source;

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

        if (animator != null)
            animator.SetBool("IsAtt", false);

        // 经共享控制锁冻结 AI/动画：与其他硬控（麻痹）引用计数协调，避免互相覆盖恢复值。
        GetFreezeLock().Lock();
    }

    private void Unfreeze()
    {
        if (!frozen)
            return;

        frozen = false;
        GetFreezeLock().Unlock();
    }

    /// <summary>取得单位上的共享控制锁；缺失时按 Buff 状态组件模式补挂。</summary>
    private UnitFreezeLock GetFreezeLock()
    {
        UnitFreezeLock freezeLock = GetComponent<UnitFreezeLock>();
        if (freezeLock == null)
            freezeLock = gameObject.AddComponent<UnitFreezeLock>();
        return freezeLock;
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