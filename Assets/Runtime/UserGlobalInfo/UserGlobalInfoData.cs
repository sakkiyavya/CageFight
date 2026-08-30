using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 玩家全局信息的可序列化快照。
/// 该类型只描述 JSON 数据结构，不负责本地、网络或平台存储。
/// </summary>
[Serializable]
public sealed class UserGlobalInfoData
{
    public const int CurrentSchemaVersion = 5;

    [HideInInspector]
    public int schemaVersion = CurrentSchemaVersion;

    [Header("玩法信息")]
    [Min(0)] public int defenseMagicLevel;
    [Min(0)] public int attackMagicLevel;
    [Min(0)] public int barracksLevel;
    [Min(0)] public int darkBarracksLevel;
    [Min(0)] public int sentryTowerLevel;
    [Min(0)] public int diamondCount;
    [FormerlySerializedAs("coinCount")]
    [Min(0)] public int goldBarCount;

    [Min(1)] public uint unlockedStage = 1;

    [Header("玩家设置")]
    [Range(0f, 1f)] public float volume = 1f;
    public bool showDamage = true;

    [Header("出战选择（稳定 ID）")]
    public string selectedEngineerId = string.Empty;
    public string selectedRaceId = string.Empty;
    public string selectedSpellSlot1Id = string.Empty;
    public string selectedSpellSlot2Id = string.Empty;

    [Header("种族大本营（选装界面配置的预制体 Key）")]
    public string selectedRaceMainBasePrefabKey = string.Empty;
}
