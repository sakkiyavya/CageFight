using UnityEngine;

[DefaultExecutionOrder(-1000)]
[RequireComponent(typeof(GameObjectProperty))]
[RequireComponent(typeof(CharacterBase))]
public class StealthAfterCombat : MonoBehaviour
{
    [Header("隐身设置")]
    [Min(0f)]
    public float stealthDelay = 2f;

    [Range(0f, 1f)]
    public float stealthAlpha = 0.35f;

    private GameObjectProperty prop;
    private CharacterBase characterBase;

    private SpriteRenderer[] sprites;
    private float[] originalAlphas;

    private float lastCombatTime;
    private bool isStealthed;

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        characterBase = GetComponent<CharacterBase>();

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

        // CharacterBase 隐身时被关闭，因此自行更新攻击范围。
        if (isStealthed)
            UpdateAttackRange();

        if (!prop.isAttack &&
            Time.time - lastCombatTime >= stealthDelay)
        {
            SetStealth(true);
        }
    }

    private void LateUpdate()
    {
        // CharacterAI 进入攻击状态后立即解除隐身。
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
            return;

        isStealthed = value;
        SetAlpha(value);

        if (value)
        {
            characterBase.ClearOccupancy();
            characterBase.enabled = false;

            ClearEnemyTargets();
        }
        else
        {
            characterBase.enabled = true;
            characterBase.RefreshOccupancy();
        }
    }

    private void SetAlpha(bool stealth)
    {
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null)
                continue;

            Color color = sprites[i].color;
            color.a = stealth
                ? stealthAlpha
                : originalAlphas[i];

            sprites[i].color = color;
        }
    }

    private void UpdateAttackRange()
    {
        Vector2Int basePos = new Vector2Int(
            (int)(transform.position.x -
                  prop.occupySpace.x / 2f + 0.5f),

            (int)(transform.position.y -
                  prop.occupySpace.y / 2f + 0.5f)
        );

        int startX = prop.isFacingLeft
            ? basePos.x - prop.atkRange.x + 1
            : basePos.x;

        int startY = basePos.y +
            Mathf.CeilToInt(
                (prop.occupySpace.y -
                 prop.atkRange.y) / 2f
            );

        prop.atkRangeMin =
            new Vector2Int(startX, startY);

        prop.atkRangeMax = new Vector2Int(
            startX + prop.atkRange.x - 1,
            startY + prop.atkRange.y - 1
        );
    }

    private void ClearEnemyTargets()
    {
        GameObjectProperty[] units =
            FindObjectsOfType<GameObjectProperty>();

        foreach (GameObjectProperty unit in units)
        {
            if (unit == null || unit == prop)
                continue;

            if (unit.target == gameObject)
                unit.target = null;

            if (unit.currentScanSession == null)
                continue;

            unit.currentScanSession
                .foundEnemies.Remove(prop);

            unit.currentScanSession
                .processed.Remove(gameObject);
        }
    }
}