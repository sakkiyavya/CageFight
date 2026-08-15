using UnityEngine;
using UnityEngine.Serialization;

public enum SpellReleaseMode
{
    Custom,
    FromEngineer
}

public enum SpellDeliveryType
{
    Projectile,
    DirectSpawn
}

[CreateAssetMenu(fileName = "NewSpell", menuName = "Player Loadout/Spell")]
public sealed class SpellDefinition : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [ResourceKey(typeof(Sprite))]
    [SerializeField] private string iconKey;
    [SerializeField, Min(0f)] private float cooldown = 5f;
    [ResourceKey(typeof(GameObject))]
    [SerializeField] private string castPrefabKey;
    [Header("投递")]
    [SerializeField] private SpellDeliveryType deliveryType;
    [SerializeField] private SpellReleaseMode releaseMode;
    [SerializeField] private Vector3 spawnOffset;
    [SerializeField] private bool faceEngineerDirection = true;
    [Header("拖拽瞄准")]
    [SerializeField] private bool dragAim;
    [SerializeField, Min(.1f)] private float maxDistance = 8f;
    [SerializeField, Min(0f)] private float arcHeight = 2f;
    [SerializeField, Min(.05f)] private float flightTime = .6f;
    [Header("警示圈")]
    [SerializeField] private bool showWarningCircle;
    [ResourceKey(typeof(Sprite))]
    [SerializeField] private string warningCircleKey;
    [SerializeField, Min(.01f)] private float warningCircleScale = 1f;

#if UNITY_EDITOR
    [FormerlySerializedAs("icon")]
    [SerializeField] private Sprite editorIcon;
    [FormerlySerializedAs("castPrefab")]
    [SerializeField] private GameObject editorCastPrefab;
    [SerializeField] private Sprite editorWarningCircle;
#endif

    public string Id => id;
    public string DisplayName => displayName;
    public string IconKey => iconKey;
    public float Cooldown => cooldown;
    public string CastPrefabKey => castPrefabKey;
    public SpellDeliveryType DeliveryType => deliveryType;
    public SpellReleaseMode ReleaseMode => releaseMode;
    public Vector3 SpawnOffset => spawnOffset;
    public bool FaceEngineerDirection => faceEngineerDirection;
    public bool DragAim => dragAim;
    public float MaxDistance => maxDistance;
    public float ArcHeight => arcHeight;
    public float FlightTime => flightTime;
    public bool ShowWarningCircle => showWarningCircle;
    public string WarningCircleKey => warningCircleKey;
    public float WarningCircleScale => warningCircleScale;

#if UNITY_EDITOR
    public Sprite EditorIcon => editorIcon;
    public GameObject EditorCastPrefab => editorCastPrefab;
    public Sprite EditorWarningCircle => editorWarningCircle;

    /// <summary>供注册表构建器把旧 Inspector 引用迁移为资源键。</summary>
    public void MigrateEditorReferences() => OnValidate();

    private void OnValidate()
    {
        id = id?.Trim();
        displayName = displayName?.Trim();
        if (string.IsNullOrWhiteSpace(iconKey) && editorIcon) iconKey = editorIcon.name;
        if (string.IsNullOrWhiteSpace(castPrefabKey) && editorCastPrefab)
            castPrefabKey = editorCastPrefab.name;
        if (string.IsNullOrWhiteSpace(warningCircleKey) && editorWarningCircle)
            warningCircleKey = editorWarningCircle.name;
    }
#endif
}
