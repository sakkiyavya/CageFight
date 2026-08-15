using UnityEngine;

[RequireComponent(typeof(DamageSource))]
public sealed class EngineerSpellDirectEffect : MonoBehaviour, IEngineerDirectSpellInstance
{
    [SerializeField] private DamageSource source;
    [SerializeField] private Animator spawnAnimator;
    [SerializeField] private SpriteRenderer warningCircle;
    [SerializeField, Min(0)] private int damage = 20;
    [SerializeField] private float repel;
    [SerializeField] private DamageType damageType = DamageType.magic;

    Vector3 warningScale;
    bool initialized;

    void Awake()
    {
        if (!source) source = GetComponent<DamageSource>();
        if (warningCircle) warningScale = warningCircle.transform.localScale;
    }

    void OnEnable()
    {
        initialized = false;
        if (source) source.enabled = false;
        if (warningCircle) warningCircle.enabled = false;
    }

    public void Initialize(EngineerController caster, SpellDefinition definition, Vector3 target)
    {
        if (!caster || !definition || !source || !IsPooled())
        {
            gameObject.SetActive(false);
            return;
        }

        transform.position = target;
        source.damage.side = caster.Side;
        source.damage.source = caster.gameObject;
        source.damage.initialDamage = damage;
        source.damage.finalDamage = 0;
        source.damage.target = null;
        source.damage.type = damageType;
        source.damage.repel = repel;
        source.target = null;

        if (warningCircle)
        {
            Sprite sprite = definition.ShowWarningCircle && ResourceManager.Instance
                ? ResourceManager.Instance.GetSprite(definition.WarningCircleKey) : null;
            warningCircle.sprite = sprite;
            warningCircle.transform.localScale = warningScale * definition.WarningCircleScale;
            warningCircle.enabled = sprite;
        }

        if (spawnAnimator)
        {
            spawnAnimator.Rebind();
            spawnAnimator.Update(0f);
        }
        initialized = true;
    }

    // 在“生成动画结束、法术效果开始”的动画事件帧调用。
    public void ActivateEffect()
    {
        if (!initialized) return;
        initialized = false;
        if (warningCircle) warningCircle.enabled = false;
        source.enabled = true;
        source.Init();
    }

    bool IsPooled() => GameObjectPool.Instance && GameObjectPool.Instance.GetPrefab(gameObject);
}
