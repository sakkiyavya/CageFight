using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class StageAudioData : ComponentData
{
    [ResourceKey(typeof(AudioClip))]
    public string audioKey;
}

[ExecuteAlways]
[RequireComponent(typeof(AudioSource))]
public class StageAudio : MonoBehaviour, ILevelComponent
{
    [ResourceKey(typeof(AudioClip))]
    [Tooltip("Audio resource key.")]
    public string audioKey;

    private AudioSource _audioSource;

    public Type DataType => typeof(StageAudioData);

    private void Start()
    {
        CacheComponent();

        if (Application.isPlaying)
        {
            ApplyRuntimeResource();
        }
    }

    private void OnEnable()
    {
        CacheComponent();

        if (Application.isPlaying)
        {
            ApplyRuntimeResource();
        }
#if UNITY_EDITOR
        else
        {
            UpdateEditorPreview();
        }
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ClearEditorPreview();
        }
#endif
    }

//     private void OnValidate()
//     {
//         CacheComponent();

// #if UNITY_EDITOR
//         if (!Application.isPlaying)
//         {
//             UpdateEditorPreview();
//         }
// #endif
//     }

    public ComponentData ExtractData()
    {
        return new StageAudioData
        {
            audioKey = audioKey
        };
    }

    public void ApplyData(ComponentData data)
    {
        StageAudioData audioData = data as StageAudioData;
        if (audioData == null)
        {
            return;
        }

        audioKey = audioData.audioKey;

        if (Application.isPlaying)
        {
            ApplyRuntimeResource();
        }
#if UNITY_EDITOR
        else
        {
            UpdateEditorPreview();
        }
#endif
    }

    private void CacheComponent()
    {
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }
    }

    private void ApplyRuntimeResource()
    {
        CacheComponent();

        if (_audioSource == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(audioKey))
        {
            _audioSource.clip = null;
            return;
        }

        AudioClip clip = ResourceManager.Instance != null ? ResourceManager.Instance.GetAudio(audioKey) : null;
        if (clip == null)
        {
            Debug.LogWarning($"[StageAudio] Missing AudioClip resource: {audioKey}", this);
        }

        _audioSource.clip = clip;
    }

#if UNITY_EDITOR
    private void UpdateEditorPreview()
    {
        CacheComponent();

        if (_audioSource == null || string.IsNullOrEmpty(audioKey))
        {
            ClearEditorPreview();
            return;
        }

        AudioRegistry registry = FindRegistry<AudioRegistry>();
        _audioSource.clip = registry != null ? registry.GetAsset(audioKey) : null;
    }

    private void ClearEditorPreview()
    {
        CacheComponent();

        if (_audioSource != null)
        {
            _audioSource.clip = null;
        }
    }

    private static T FindRegistry<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids.Length == 0)
        {
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }
#endif
}
