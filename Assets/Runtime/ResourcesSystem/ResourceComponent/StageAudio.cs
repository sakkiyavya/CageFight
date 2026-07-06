using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class StageAudioData : ComponentData
{
    [ResourceKey(typeof(AudioClip))] public string audioKey1;
    [ResourceKey(typeof(AudioClip))] public string audioKey2;
    [ResourceKey(typeof(AudioClip))] public string audioKey3;
    [ResourceKey(typeof(AudioClip))] public string audioKey4;
    [ResourceKey(typeof(AudioClip))] public string audioKey5;
    [ResourceKey(typeof(AudioClip))] public string audioKey6;
    [ResourceKey(typeof(AudioClip))] public string audioKey7;
    [ResourceKey(typeof(AudioClip))] public string audioKey8;
}

[ExecuteAlways]
public class StageAudio : MonoBehaviour, ILevelComponent
{
    [Header("音频资源 Key（留空则忽略）")]
    [ResourceKey(typeof(AudioClip))] public string audioKey1;
    [ResourceKey(typeof(AudioClip))] public string audioKey2;
    [ResourceKey(typeof(AudioClip))] public string audioKey3;
    [ResourceKey(typeof(AudioClip))] public string audioKey4;
    [ResourceKey(typeof(AudioClip))] public string audioKey5;
    [ResourceKey(typeof(AudioClip))] public string audioKey6;
    [ResourceKey(typeof(AudioClip))] public string audioKey7;
    [ResourceKey(typeof(AudioClip))] public string audioKey8;

    public Type DataType => typeof(StageAudioData);

    private void OnEnable()
    {
        if (Application.isPlaying)
            ApplyRuntimeResource();
    }

    // ─── ILevelComponent ─────────────────────────────────────

    public ComponentData ExtractData() => new StageAudioData
    {
        audioKey1 = audioKey1,
        audioKey2 = audioKey2,
        audioKey3 = audioKey3,
        audioKey4 = audioKey4,
        audioKey5 = audioKey5,
        audioKey6 = audioKey6,
        audioKey7 = audioKey7,
        audioKey8 = audioKey8,
    };

    public void ApplyData(ComponentData data)
    {
        if (data is not StageAudioData d) return;

        audioKey1 = d.audioKey1;
        audioKey2 = d.audioKey2;
        audioKey3 = d.audioKey3;
        audioKey4 = d.audioKey4;
        audioKey5 = d.audioKey5;
        audioKey6 = d.audioKey6;
        audioKey7 = d.audioKey7;
        audioKey8 = d.audioKey8;

        if (Application.isPlaying)
            ApplyRuntimeResource();
    }

    // ─── 运行时资源注入 ───────────────────────────────────────

    /// <summary>
    /// 从 ResourceManager 获取所有非空 Key 对应的 AudioClip，
    /// 写入同级 GameObjectProperty.audioClips 列表。
    /// </summary>
    private void ApplyRuntimeResource()
    {
        var prop = GetComponent<GameObjectProperty>();
        if (prop == null)
        {
            Debug.LogWarning("[StageAudio] 未找到同级 GameObjectProperty，无法写入音频列表。", this);
            return;
        }

        if (ResourceManager.Instance == null)
        {
            Debug.LogWarning("[StageAudio] ResourceManager 未就绪。", this);
            return;
        }

        prop.audioClips.Clear();

        foreach (var key in AllKeys())
        {
            if (string.IsNullOrEmpty(key)) continue;

            AudioClip clip = ResourceManager.Instance.GetAudio(key);
            if (clip != null)
                prop.audioClips.Add(clip);
            else
                Debug.LogWarning($"[StageAudio] 未找到音频资源 Key: {key}", this);
        }
    }

    private IEnumerable<string> AllKeys()
    {
        yield return audioKey1;
        yield return audioKey2;
        yield return audioKey3;
        yield return audioKey4;
        yield return audioKey5;
        yield return audioKey6;
        yield return audioKey7;
        yield return audioKey8;
    }

#if UNITY_EDITOR
    private static T FindRegistry<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
#endif
}
