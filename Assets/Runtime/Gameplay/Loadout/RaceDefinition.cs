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

/// <summary>挂在种族效果预制体根节点，用于接收本局的工程师实例。</summary>
public interface IRaceRuntimeEffect
{
    void Initialize(RaceDefinition race, EngineerController engineer);
}
