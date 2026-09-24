using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>种族的静态选择数据；运行时资源必须通过 ResourceManager 的资源键取得。</summary>
[CreateAssetMenu(fileName = "NewRace", menuName = "Player Loadout/Race")]
public sealed class RaceDefinition : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [ResourceKey(typeof(Sprite))]
    [SerializeField] private string iconKey;
    [SerializeField, TextArea] private string description;
    [ResourceKey(typeof(GameObject))]
    [SerializeField] private string runtimeEffectPrefabKey;

    [Header("种族建筑表（种族→建筑→兵种链）")]
    [Tooltip("该种族各类型建筑使用的预制体资源键（按建筑类型 ID 匹配）；未配置的类型回落建造按钮的默认建筑")]
    [SerializeField] private List<RaceBuildingEntry> buildings = new List<RaceBuildingEntry>();

#if UNITY_EDITOR
    [FormerlySerializedAs("icon")]
    [SerializeField] private Sprite editorIcon;
    [FormerlySerializedAs("runtimeEffectPrefab")]
    [SerializeField] private GameObject editorRuntimeEffectPrefab;
#endif

    public string Id => id;
    public string DisplayName => displayName;
    public string IconKey => iconKey;
    public string Description => description;
    public string RuntimeEffectPrefabKey => runtimeEffectPrefabKey;

    /// <summary>该种族各类型建筑的预制体映射（只读）。</summary>
    public IReadOnlyList<RaceBuildingEntry> Buildings => buildings;

#if UNITY_EDITOR
    public Sprite EditorIcon => editorIcon;
    public GameObject EditorRuntimeEffectPrefab => editorRuntimeEffectPrefab;

    /// <summary>供注册表构建器把旧 Inspector 引用迁移为资源键。</summary>
    public void MigrateEditorReferences() => OnValidate();

    private void OnValidate()
    {
        id = id?.Trim();
        displayName = displayName?.Trim();
        if (string.IsNullOrWhiteSpace(iconKey) && editorIcon) iconKey = editorIcon.name;
        if (string.IsNullOrWhiteSpace(runtimeEffectPrefabKey) && editorRuntimeEffectPrefab)
            runtimeEffectPrefabKey = editorRuntimeEffectPrefab.name;
    }
#endif
}

/// <summary>
/// 种族建筑条目：把“建筑类型”映射到该种族使用的建筑预制体资源键。
/// 例如 buildingType=Barracks 对应本种族兵营的 prefabKey。
/// </summary>
[Serializable]
public class RaceBuildingEntry
{
    [Tooltip("建筑类型（下拉选择，与建造按钮 BuildingButton.buildingType 同枚举）")]
    public BuildingType buildingType = BuildingType.None;

    [ResourceKey(typeof(GameObject))]
    [Tooltip("该种族此类型建筑使用的预制体资源键（须登记到 PrefabRegistry 并按关卡预载）")]
    public string prefabKey = string.Empty;
}

/// <summary>挂在种族效果预制体根节点，用于接收本局的工程师实例。</summary>
public interface IRaceRuntimeEffect
{
    void Initialize(RaceDefinition race, EngineerController engineer);
}
