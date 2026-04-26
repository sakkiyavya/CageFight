using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 关卡全局配置数据
/// </summary>
[Serializable]
public class LevelSettings
{
    // 未来可以扩展：例如时间限制、背景音乐、天气环境参数等
}

/// <summary>
/// 关卡配置的根数据结构（ScriptableObject）
/// 这是编辑器和运行时唯一共享的核心数据源
/// </summary>
[CreateAssetMenu(fileName = "NewLevelConfig", menuName = "StageSystem/Level Config")]
public class LevelConfig : ScriptableObject
{
    [Tooltip("关卡唯一标识 ID")]
    public int levelId;

    [Tooltip("关卡的全局设置")]
    public LevelSettings settings;

    [Tooltip("该关卡内包含的所有物体数据集合")]
    public List<LevelObjectData> objects = new List<LevelObjectData>();
}

