using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;


/// <summary>
/// 关卡全局配置数据
/// </summary>
[Serializable]
public class StageSettings
{
    // 未来可以扩展：例如时间限制、背景音乐、天气环境参数等
}

/// <summary>
/// 关卡配置的根数据结构（ScriptableObject）
/// 这是编辑器和运行时唯一共享的核心数据源
/// </summary>
[CreateAssetMenu(fileName = "NewStageConfig", menuName = "StageSystem/Stage Config")]
public class StageConfig : ScriptableObject
{
    [Tooltip("关卡唯一标识 ID")]
    [FormerlySerializedAs("levelId")]
    public int stageId;                                                    // 用于存档、选关和运行时寻址的关卡唯一编号。

    [Tooltip("Stage icon.")]
    public Sprite icon;                                                    // 选关界面用于展示该关卡的图标。

    [Tooltip("关卡的全局设置")]
    public StageSettings settings;                                         // 该关卡共用的全局规则和环境参数。

    [Tooltip("该关卡内包含的所有物体数据集合")]
    public List<StageObjectData> objects = new List<StageObjectData>();    // 进入关卡时需要实例化的全部对象数据。

    [Tooltip("预制体资源 Key 清单")]
    public List<string> prefabs = new List<string>();                      // 本关卡依赖的预制体资源键集合。

    [Tooltip("音频资源 Key 清单")]
    public List<string> audios = new List<string>();                       // 本关卡依赖的音频资源键集合。

    [Tooltip("纹理资源 Key 清单")]
    public List<string> textures = new List<string>();                     // 本关卡依赖的纹理资源键集合。

    [Tooltip("动画片段资源 Key 清单")]
    public List<string> animationClips = new List<string>();               // 本关卡依赖的动画片段资源键集合。

    [Tooltip("动画控制器资源 Key 清单")]
    public List<string> animatorControllers = new List<string>();          // 本关卡依赖的动画控制器资源键集合。

    [Tooltip("Sprite 资源 Key 清单")]
    public List<string> sprites = new List<string>();                      // 本关卡依赖的精灵资源键集合。
}

