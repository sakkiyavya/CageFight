using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class StealthAfterCombat : MonoBehaviour
{
    [Header("隐身设置")]
    [SerializeField, Min(0f)] private float stealthDelay = 2f;
    [SerializeField, Range(0f, 1f)] private float stealthAlpha = 0.35f;

    private GameObjectProperty prop;
    private SpriteRenderer[] sprites;
    private float[] originalAlphas;
    private float lastCombatTime;
    private bool isStealthed;

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        sprites = GetComponentsInChildren<SpriteRenderer>(true);
        originalAlphas = new float[sprites.Length];

        for (int i = 0; i < sprites.Length; i++)
            originalAlphas[i] = sprites[i].color.a;
    }

    private void OnEnable()
    {
        prop.OnAtt += EnterCombat;
        prop.OnHitted += OnHitted;
        lastCombatTime = Time.time;
        SetStealth(false);
    }

    private void OnDisable()
    {
        prop.OnAtt -= EnterCombat;
        prop.OnHitted -= OnHitted;
        SetStealth(false);
    }

    private void Update()
    {
        if (prop.isDead)
        {
            SetStealth(false);
            return;
        }

        if (!prop.isAttack &&
            Time.time - lastCombatTime >= stealthDelay)
        {
            SetStealth(true);
        }
    }

    private void LateUpdate()
    {
        if (prop.isAttack)
            EnterCombat();
    }

    private void EnterCombat()
    {
        lastCombatTime = Time.time;
        SetStealth(false);
    }

    private void OnHitted(Damage damage)
    {
        EnterCombat();
    }

    private void SetStealth(bool value)
    {
        if (isStealthed == value)
        {
            prop.isUntargetable = value;
            return;
        }

        isStealthed = value;
        prop.isUntargetable = value;

        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer sprite = sprites[i];
            if (sprite == null)
                continue;

            Color color = sprite.color;
            color.a = value ? stealthAlpha : originalAlphas[i];
            sprite.color = color;
        }
    }
}
