using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>所有可选工程师共用的局内移动、生命、受击、建筑治疗与法术锚点。</summary>
[RequireComponent(typeof(SpriteRenderer), typeof(Collider2D), typeof(GameObjectProperty))]
public class EngineerController : MonoBehaviour, ICollide
{
    public static EngineerController Active { get; private set; }

    [Header("工程师属性")]
    [Min(0.01f)] public float moveSpeed = 3f;
    [Min(1)] public int maxHp = 100;
    [Min(0.1f)] public float antiRepel = 1f;
    [Min(0.1f)] public float cameraViewSize = 5f;

    [Header("濒死击飞")]
    [Min(0f)] public float defeatFlyDistance = 5f;
    [Min(0.01f)] public float defeatFlyTime = .35f;
    [Min(0f)] public float defeatStunTime = 3f;

    [Header("修复建筑")]
    [Min(0f)] public float buildingHealRadius = 2f;
    [Min(0f)] public float buildingHealPercentPerSecond = .02f;
    [Min(0.05f)] public float buildingHealTick = .25f;
    public LayerMask buildingLayer = ~0;
    public Color buildingHealColor = Color.green;
    [Range(0f, 1f)] public float buildingHealGlow = .5f;
    [Min(0f)] public float buildingHealGlowSpeed = 4f;
    public bool buildingHealParticles = true;
    [Min(0.01f)] public float healParticleSize = .12f;
    [Min(0f)] public float healParticleRiseSpeed = .6f;

    [Header("动画参数")]
    public string moveParameter = "IsMoving";

    [Header("法术接口")]
    public Transform spellAnchor;

    [Header("受击表现")]
    public Color hitFlashColor = Color.white;
    [Min(0.01f)] public float hitEffectTime = .18f;
    [Min(0f)] public float hitBounce = .15f;

    public int CurrentHp { get; private set; }
    public int Side => property ? property.side : 0;
    public Vector2 FacingDirection { get; private set; } = Vector2.right;
    public Vector3 SpawnPoint { get; private set; }
    public Vector3 SpellPosition
    {
        get
        {
            if (!spellAnchor) return transform.position;
            if (!sprite || !sprite.flipX) return spellAnchor.position;
            Vector3 offset = spellAnchor.position - transform.position;
            offset.x = -offset.x;
            return transform.position + offset;
        }
    }

    private Transform shootPointCache;
    /// <summary>
    /// 工程师的射击点（"Shoot point" 子节点）；缺失时回退到施法锚点。
    /// 用于抛射法术的发射起点。
    /// </summary>
    public Vector3 ShootPoint
    {
        get
        {
            if (shootPointCache == null)
                shootPointCache = transform.Find("Shoot point");
            if (shootPointCache != null)
            {
                if (!sprite || !sprite.flipX) return shootPointCache.position;
                Vector3 offset = shootPointCache.position - transform.position;
                offset.x = -offset.x;
                return transform.position + offset;
            }
            return SpellPosition;
        }
    }
    public bool IsStunned => Time.time < stunnedUntil;

    public event Action<int, int> OnHealthChanged;
    public event Action<Damage> OnHitted;
    public event Action<Vector3, Vector2> OnSpellCast;

    static readonly Collider2D[] healHits = new Collider2D[64];
    readonly HashSet<BuildingHealth> healedThisTick = new HashSet<BuildingHealth>();
    readonly HashSet<SpriteRenderer> glowingThisTick = new HashSet<SpriteRenderer>();
    readonly Dictionary<SpriteRenderer, Color> glowingBuildings = new Dictionary<SpriteRenderer, Color>();
    readonly List<SpriteRenderer> stoppedGlowing = new List<SpriteRenderer>();

    SpriteRenderer sprite;
    Animator animator;
    GameObjectProperty property;
    ParticleSystem healParticles;
    GameObject healParticlesHost;                                  // 治疗粒子宿主（池化对象，随工程师回收归还）。
    const string HealParticlesPrefabKey = "EngineerHealParticles"; // 治疗粒子宿主预制体资源键。
    Vector3 baseScale;
    Color baseColor;
    float nextHealTime, stunnedUntil;
    bool moving, defeated;
    int moveParameterHash;
    Coroutine hitRoutine, forcedMoveRoutine;

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        property = GetComponent<GameObjectProperty>();
        moveParameterHash = Animator.StringToHash(moveParameter);
        baseScale = transform.localScale;
        baseColor = sprite.color;
        if (!spellAnchor) spellAnchor = transform;
        CurrentHp = maxHp;
        CreateHealParticles();
    }

    void OnEnable()
    {
        Active = this;
        SpawnPoint = transform.position;
        CurrentHp = maxHp;
        defeated = false;
        OnHealthChanged?.Invoke(CurrentHp, maxHp);
    }

    void OnDisable()
    {
        if (Active == this) Active = null;
        StopAllBuildingGlow();

        // 归还治疗粒子宿主到对象池，避免每次启用重复创建。
        if (healParticlesHost != null && GameObjectPool.Instance != null)
            GameObjectPool.Instance.Release(healParticlesHost);
        healParticlesHost = null;
        healParticles = null;
    }

    void Update()
    {
        Vector2 input = !IsStunned && JoyStick.Instance
            ? JoyStick.Instance.InputDir : Vector2.zero;

        if (forcedMoveRoutine == null)
            Move(input);
        UpdateAnimation(input);

        if (Time.time >= nextHealTime)
        {
            nextHealTime = Time.time + buildingHealTick;
            HealNearbyBuildings();
        }
        UpdateBuildingHealGlow();
    }

    void Move(Vector2 input)
    {
        if (input.sqrMagnitude <= .001f) return;
        FacingDirection = input.normalized;
        if (Mathf.Abs(input.x) > .01f) sprite.flipX = input.x < 0f;

        Vector3 next = transform.position + (Vector3)input * (moveSpeed * Time.deltaTime);
        transform.position = ClampToMap(next);
    }

    void UpdateAnimation(Vector2 input)
    {
        bool shouldMove = !IsStunned && input.sqrMagnitude > .001f;
        if (!animator || shouldMove == moving) return;
        moving = shouldMove;
        animator.SetBool(moveParameterHash, moving);
    }

    public bool IsFriendly(Damage damage) => damage.side == Side;

    public Damage OnCollide(Damage damage)
    {
        if (defeated || IsFriendly(damage)) return damage;
        damage = DamageComputor.DamageCompute(damage);
        CurrentHp = Mathf.Max(0, CurrentHp - Mathf.Max(0, damage.finalDamage));
        OnHitted?.Invoke(damage);
        OnHealthChanged?.Invoke(CurrentHp, maxHp);

        if (hitRoutine != null) StopCoroutine(hitRoutine);
        hitRoutine = StartCoroutine(HitVisual());

        if (CurrentHp <= 0) Defeat();
        else if (damage.repel > 0f)
        {
            Vector3 push = Vector3.right * damage.collideDir * (damage.repel / antiRepel);
            StartForcedMove(ClampToMap(transform.position + push), .12f);
        }
        return damage;
    }

    void Defeat()
    {
        defeated = true;
        CurrentHp = maxHp;
        OnHealthChanged?.Invoke(CurrentHp, maxHp);
        stunnedUntil = Time.time + defeatStunTime;

        Vector3 towardSpawn = SpawnPoint - transform.position;
        if (towardSpawn.sqrMagnitude < .001f) towardSpawn = Vector3.left;
        Vector3 destination = ClampToMap(
            transform.position + towardSpawn.normalized * defeatFlyDistance);
        StartForcedMove(destination, defeatFlyTime, () => defeated = false);
    }

    void StartForcedMove(Vector3 destination, float duration, Action done = null)
    {
        if (forcedMoveRoutine != null) StopCoroutine(forcedMoveRoutine);
        forcedMoveRoutine = StartCoroutine(ForcedMove(destination, duration, done));
    }

    IEnumerator ForcedMove(Vector3 destination, float duration, Action done)
    {
        Vector3 start = transform.position;
        float time = 0f;
        duration = Mathf.Max(.01f, duration);
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);
            transform.position = Vector3.Lerp(start, destination, 1f - (1f - t) * (1f - t));
            yield return null;
        }
        transform.position = destination;
        forcedMoveRoutine = null;
        done?.Invoke();
    }

    IEnumerator HitVisual()
    {
        float time = 0f;
        while (time < hitEffectTime)
        {
            time += Time.deltaTime;
            float wave = Mathf.Sin(time / hitEffectTime * Mathf.PI);
            sprite.color = Color.Lerp(baseColor, hitFlashColor, wave);
            transform.localScale = baseScale * (1f + wave * hitBounce);
            yield return null;
        }
        sprite.color = baseColor;
        transform.localScale = baseScale;
        hitRoutine = null;
    }

    void HealNearbyBuildings()
    {
        glowingThisTick.Clear();
        if (buildingHealPercentPerSecond <= 0f || buildingHealRadius <= 0f)
        {
            SyncBuildingGlow();
            return;
        }
        int count = Physics2D.OverlapCircleNonAlloc(
            transform.position, buildingHealRadius, healHits, buildingLayer);
        healedThisTick.Clear();

        for (int i = 0; i < count; i++)
        {
            BuildingHealth health = healHits[i].GetComponentInParent<BuildingHealth>();
            if (!health || !healedThisTick.Add(health)) continue;
            GameObjectProperty building = health.GetComponent<GameObjectProperty>();
            if (!building || building.side != Side || health.HP >= building.maxHp) continue;

            int amount = Mathf.Max(1, Mathf.RoundToInt(
                building.maxHp * buildingHealPercentPerSecond * buildingHealTick));
            float percent = (float)Mathf.Min(building.maxHp, health.HP + amount) / building.maxHp;
            health.SetPercentHp(percent);

            SpriteRenderer renderer = health.GetComponentInChildren<SpriteRenderer>();
            if (renderer)
            {
                glowingThisTick.Add(renderer);
                if (!glowingBuildings.ContainsKey(renderer))
                    glowingBuildings.Add(renderer, renderer.color);
                EmitHealParticle(renderer);
            }
        }
        SyncBuildingGlow();
    }

    void UpdateBuildingHealGlow()
    {
        float glow = (.5f + .5f * Mathf.Sin(Time.time * buildingHealGlowSpeed)) * buildingHealGlow;
        foreach (var item in glowingBuildings)
        {
            if (!item.Key) continue;
            Color color = Color.Lerp(item.Value, buildingHealColor, glow);
            color.a = item.Value.a;
            item.Key.color = color;
        }
    }

    void SyncBuildingGlow()
    {
        stoppedGlowing.Clear();
        foreach (var item in glowingBuildings)
            if (!item.Key || !glowingThisTick.Contains(item.Key)) stoppedGlowing.Add(item.Key);
        for (int i = 0; i < stoppedGlowing.Count; i++)
        {
            SpriteRenderer renderer = stoppedGlowing[i];
            if (renderer) renderer.color = glowingBuildings[renderer];
            glowingBuildings.Remove(renderer);
        }
    }

    void StopAllBuildingGlow()
    {
        foreach (var item in glowingBuildings)
            if (item.Key) item.Key.color = item.Value;
        glowingBuildings.Clear();
    }

    void CreateHealParticles()
    {
        // 治疗粒子宿主经对象池生成（预制体：EngineerHealParticles），不再运行时 new GameObject。
        GameObject host = null;
        GameObject prefab = ResourceManager.Instance != null
            ? ResourceManager.Instance.GetGameObject(HealParticlesPrefabKey)
            : null;
        if (prefab != null && GameObjectPool.Instance != null)
            host = GameObjectPool.Instance.Get(prefab);
        if (host == null)
            return;

        healParticlesHost = host;
        healParticles = host.GetComponent<ParticleSystem>();
        if (healParticles == null)
            healParticles = host.AddComponent<ParticleSystem>();

        var main = healParticles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 32;
        var emission = healParticles.emission;
        emission.enabled = false;
        var fade = healParticles.colorOverLifetime;
        fade.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        host.GetComponent<ParticleSystemRenderer>().sortingOrder = sprite.sortingOrder + 10;
        healParticles.Play();
    }

    void EmitHealParticle(SpriteRenderer renderer)
    {
        if (!buildingHealParticles || !healParticles) return;
        var emit = new ParticleSystem.EmitParams
        {
            position = renderer.bounds.center + Vector3.up * (renderer.bounds.extents.y + .05f),
            velocity = new Vector3(UnityEngine.Random.Range(-.15f, .15f), healParticleRiseSpeed),
            startColor = buildingHealColor,
            startLifetime = .7f,
            startSize = healParticleSize
        };
        healParticles.Emit(emit, 1);
    }

    Vector3 ClampToMap(Vector3 position)
    {
        if (!MapCells.Instance) return position;
        position.x = Mathf.Clamp(position.x, 0f, MapCells.Instance.width);
        position.y = Mathf.Clamp(position.y, 0f, MapCells.Instance.height);
        return position;
    }

    /// <summary>法术按钮调用；具体法术组件可订阅 OnSpellCast。</summary>
    public void CastSpell()
    {
        if (IsStunned) return;
        OnSpellCast?.Invoke(SpellPosition, FacingDirection);
    }
}
