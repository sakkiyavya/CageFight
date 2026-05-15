using UnityEngine;

public class AttackSoundController : MonoBehaviour
{
    [Header("攻击音效")]
    [Tooltip("主攻击音效（必选）")]
    public AudioClip attackSound1;

    [Tooltip("次要攻击音效（可选）")]
    public AudioClip attackSound2;

    [Tooltip("次要攻击音效（可选）")]
    public AudioClip attackSound3;

    [Header("设置")]
    [Tooltip("是否随机播放两个音效")]
    public bool randomizeSounds = false;

    [Tooltip("随机音高变化范围")]
    [Range(0.8f, 1.2f)] public float pitchVariation = 0.1f;

    private AudioSource audioSource;
    private float basePitch = 1.0f;

    void Start()
    {
        // 自动设置AudioSource
        SetupAudioSource();
    }

    private void SetupAudioSource()
    {
        // 尝试获取现有的AudioSource
        if (!TryGetComponent<AudioSource>(out audioSource))
        {
            // 如果没有，创建一个
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // 保存原始音高
        basePitch = audioSource.pitch;

        // 设置基本参数
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1.0f; // 3D音效

        // 确保至少有一个音效
        if (attackSound1 == null)
        {
            Debug.LogWarning($"{gameObject.name}: 没有设置主攻击音效!");
        }
    }

    /// <summary>
    /// 播放攻击音效（动画事件用）
    /// 在Animation Event的Function中填"PlayAttack"
    /// </summary>
    public void PlayAttack()
    {
        if (!audioSource || attackSound1 == null) return;

        // 决定播放哪个音效
        AudioClip clipToPlay = attackSound1;

        if (randomizeSounds && attackSound2 != null)
        {
            // 50%概率播放第二个音效
            if (Random.value > 0.5f)
            {
                clipToPlay = attackSound2;
            }
        }

        // 应用随机音高
        if (pitchVariation > 0)
        {
            audioSource.pitch = basePitch + Random.Range(-pitchVariation, pitchVariation);
        }

        // 播放音效
        audioSource.PlayOneShot(clipToPlay);

        // 重置音高
        audioSource.pitch = basePitch;
    }

    /// <summary>
    /// 播放特定音效（通过参数）
    /// 在Animation Event的Function中填"PlaySpecificSound"，参数填1或2
    /// </summary>
    public void PlaySpecificSound(int soundNumber)
    {
        if (!audioSource) return;

        AudioClip clip = null;

        switch (soundNumber)
        {
            case 1:
                clip = attackSound1;
                break;
            case 2:
                clip = attackSound2;
                break;
            case 3:
                clip = attackSound3;
                break;
            default:
                Debug.LogWarning($"无效的音效编号: {soundNumber}，使用默认音效");
                clip = attackSound1;
                break;
        }

        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    /// <summary>
    /// 编辑器测试方法
    /// </summary>
    [ContextMenu("测试音效1")]
    public void TestSound1()
    {
        if (attackSound1 != null)
        {
            audioSource.PlayOneShot(attackSound1);
        }
    }

    [ContextMenu("测试音效2")]
    public void TestSound2()
    {
        if (attackSound2 != null)
        {
            audioSource.PlayOneShot(attackSound2);
        }
    }
    [ContextMenu("测试音效3")]
    public void TestSound3()
    {
        if (attackSound1 != null)
        {
            audioSource.PlayOneShot(attackSound3);
        }
    }
}