using UnityEngine;
using UnityEngine.Serialization;

/// <summary>工程师的静态选择数据；运行时资源必须通过 ResourceManager 的资源键取得。</summary>
[CreateAssetMenu(fileName = "NewEngineer", menuName = "Player Loadout/Engineer")]
public sealed class EngineerDefinition : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [ResourceKey(typeof(Sprite))]
    [SerializeField] private string iconKey;
    [ResourceKey(typeof(GameObject))]
    [SerializeField] private string prefabKey;
    [SerializeField] private string innateSpellId;
    [ResourceKey(typeof(Sprite))]
    [SerializeField] private string portraitFrameKey;
    [ResourceKey(typeof(Sprite))]
    [SerializeField] private string[] idlePortraitFrameKeys;
    [SerializeField, Range(1f, 20f)] private float idlePortraitFrameRate = 6f;

#if UNITY_EDITOR
    [FormerlySerializedAs("icon")]
    [SerializeField] private Sprite editorIcon;
    [FormerlySerializedAs("prefab")]
    [SerializeField] private GameObject editorPrefab;
    [FormerlySerializedAs("innateSpell")]
    [SerializeField] private SpellDefinition editorInnateSpell;
    [SerializeField] private Sprite[] editorIdlePortraitFrames;
    [SerializeField] private Sprite editorPortraitFrame;
#endif

    public string Id => id;
    public string DisplayName => displayName;
    public string IconKey => iconKey;
    public string PrefabKey => prefabKey;
    public string InnateSpellId => innateSpellId;
    public string PortraitFrameKey => portraitFrameKey;
    public string[] IdlePortraitFrameKeys => idlePortraitFrameKeys ?? System.Array.Empty<string>();
    public float IdlePortraitFrameRate => idlePortraitFrameRate;

#if UNITY_EDITOR
    public Sprite EditorIcon => editorIcon;
    public Sprite EditorPortraitFrame => editorPortraitFrame;
    public GameObject EditorPrefab => editorPrefab;

    /// <summary>供注册表构建器把旧 Inspector 引用迁移为资源键。</summary>
    public void MigrateEditorReferences() => OnValidate();

    private void OnValidate()
    {
        id = id?.Trim();
        displayName = displayName?.Trim();
        if (string.IsNullOrWhiteSpace(iconKey) && editorIcon) iconKey = editorIcon.name;
        if (string.IsNullOrWhiteSpace(prefabKey) && editorPrefab) prefabKey = editorPrefab.name;
        if (string.IsNullOrWhiteSpace(innateSpellId) && editorInnateSpell)
            innateSpellId = editorInnateSpell.Id;
        if (editorPortraitFrame) portraitFrameKey = editorPortraitFrame.name;
        if (editorIdlePortraitFrames == null || editorIdlePortraitFrames.Length == 0) return;
        idlePortraitFrameKeys = new string[editorIdlePortraitFrames.Length];
        for (int i = 0; i < editorIdlePortraitFrames.Length; i++)
            idlePortraitFrameKeys[i] = editorIdlePortraitFrames[i] ? editorIdlePortraitFrames[i].name : string.Empty;
    }
#endif
}
