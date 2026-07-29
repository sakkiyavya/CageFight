using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class StageAudioData : ComponentData
{
    [ResourceKey(typeof(AudioClip))] public string audioKey1;                // 按播放顺序保存的第 1 个音频资源键。
    [ResourceKey(typeof(AudioClip))] public string audioKey2;                // 按播放顺序保存的第 2 个音频资源键。
    [ResourceKey(typeof(AudioClip))] public string audioKey3;                // 按播放顺序保存的第 3 个音频资源键。
    [ResourceKey(typeof(AudioClip))] public string audioKey4;                // 按播放顺序保存的第 4 个音频资源键。
    [ResourceKey(typeof(AudioClip))] public string audioKey5;                // 按播放顺序保存的第 5 个音频资源键。
    [ResourceKey(typeof(AudioClip))] public string audioKey6;                // 按播放顺序保存的第 6 个音频资源键。
    [ResourceKey(typeof(AudioClip))] public string audioKey7;                // 按播放顺序保存的第 7 个音频资源键。
    [ResourceKey(typeof(AudioClip))] public string audioKey8;                // 按播放顺序保存的第 8 个音频资源键。
}

[ExecuteAlways]
public class StageAudio : MonoBehaviour, IStageComponent
{
    [Header("音频资源 Key（留空则忽略）")]
    [ResourceKey(typeof(AudioClip))] public string audioKey1;                // 注入属性组件音频列表的第 1 个资源键。
    [ResourceKey(typeof(AudioClip))] public string audioKey2;                // 注入属性组件音频列表的第 2 个资源键。
    [ResourceKey(typeof(AudioClip))] public string audioKey3;                // 注入属性组件音频列表的第 3 个资源键。
    [ResourceKey(typeof(AudioClip))] public string audioKey4;                // 注入属性组件音频列表的第 4 个资源键。
    [ResourceKey(typeof(AudioClip))] public string audioKey5;                // 注入属性组件音频列表的第 5 个资源键。
    [ResourceKey(typeof(AudioClip))] public string audioKey6;                // 注入属性组件音频列表的第 6 个资源键。
    [ResourceKey(typeof(AudioClip))] public string audioKey7;                // 注入属性组件音频列表的第 7 个资源键。
    [ResourceKey(typeof(AudioClip))] public string audioKey8;                // 注入属性组件音频列表的第 8 个资源键。

    public Type DataType => typeof(StageAudioData);                          // 该组件在关卡配置中对应的数据类型。

    #region 生命周期与回调
    /// <summary>
    /// 组件在运行时启用后，从资源管理器重新注入当前配置的全部音频片段。
    /// </summary>
    private void OnEnable()
    {
        if (Application.isPlaying)
            ApplyRuntimeResource();
    }
    #endregion

    // ─── IStageComponent ─────────────────────────────────────

    #region 关卡数据转换
    /// <summary>
    /// 将八个音频资源键导出为可写入关卡配置的组件数据。
    /// </summary>
    /// <returns>包含当前音频键顺序的 <see cref="StageAudioData"/>。</returns>
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

    /// <summary>
    /// 从音频组件数据恢复八个资源键，并在运行时立即刷新对象上的音频片段列表。
    /// </summary>
    /// <param name="data">期望为 <see cref="StageAudioData"/> 的关卡组件数据；类型不匹配时忽略。</param>
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
    #endregion

    // ─── 运行时资源注入 ───────────────────────────────────────

    #region 运行时资源注入
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

            AudioClip clip = ResourceManager.Instance.GetAudio(key);         // 当前资源键对应的已加载音频片段。
            if (clip != null)
                prop.audioClips.Add(clip);
            else
                Debug.LogWarning($"[StageAudio] 未找到音频资源 Key: {key}", this);
        }
    }

    /// <summary>
    /// 按 Inspector 字段顺序枚举八个音频资源键，包含空键以保持固定顺序定义。
    /// </summary>
    /// <returns>依次产生 audioKey1 到 audioKey8 的可枚举序列。</returns>
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
    #endregion

#if UNITY_EDITOR
    #region 编辑器资源查询
    /// <summary>
    /// 在编辑器资产数据库中查找指定类型的第一个资源注册表。
    /// </summary>
    /// <typeparam name="T">需要查找的注册表 ScriptableObject 类型。</typeparam>
    /// <returns>找到的第一个注册表资产；没有匹配资产时返回 <see langword="null"/>。</returns>
    private static T FindRegistry<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");    // 匹配指定类型的资产 GUID。
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
    #endregion
#endif
}
