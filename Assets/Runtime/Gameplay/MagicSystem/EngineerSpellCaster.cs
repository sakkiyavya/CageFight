using System.Collections;
using UnityEngine;

/// <summary>
/// Creates engineer spell instances through the shared object pool and initializes
/// the spell executor placed on the prefab root.
/// </summary>
public static class EngineerSpellCaster
{
    /// <summary>
    /// Casts a spell toward the engineer's current facing direction.
    /// </summary>
    public static bool Cast(SpellDefinition definition, EngineerController caster)
    {
        if (!definition || !caster) return false;
        Vector3 target = caster.SpellPosition +
            (Vector3)caster.FacingDirection * definition.MaxDistance;
        return Cast(definition, caster, target);
    }

    /// <summary>
    /// Casts a spell at the supplied world-space target.
    /// </summary>
    public static bool Cast(
        SpellDefinition definition,
        EngineerController caster,
        Vector3 target)
    {
        if (!definition || !caster || string.IsNullOrWhiteSpace(definition.CastPrefabKey))
            return false;

        GameObjectPool pool = GameObjectPool.Instance;
        ResourceManager resourceManager = ResourceManager.Instance;
        if (!pool || !resourceManager)
        {
            Debug.LogError("[EngineerSpellCaster] ResourceManager 或 GameObjectPool 未就绪，无法释放法术。", caster);
            return false;
        }

        GameObject castPrefab = resourceManager.GetGameObject(definition.CastPrefabKey);
        if (!castPrefab)
        {
            Debug.LogError(
                $"[EngineerSpellCaster] 法术预制体未预加载：{definition.CastPrefabKey}",
                caster);
            return false;
        }

        Vector2 facing = caster.FacingDirection;
        bool fromEngineer = definition.ReleaseMode == SpellReleaseMode.FromEngineer;
        float angle = fromEngineer && definition.FaceEngineerDirection
            ? Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg
            : 0f;

        if (definition.DeliveryType == SpellDeliveryType.DirectSpawn)
        {
            GameObject castAnimation = resourceManager.GetGameObject("Cast spell");
            if (!castAnimation)
            {
                Debug.LogError("[EngineerSpellCaster] Cast spell 前摇预制体未预加载。", caster);
                return false;
            }

            caster.StartCoroutine(CastDirectAfterAnimation(
                castAnimation, castPrefab, caster, definition, target, angle));
            return true;
        }

        return SpawnInstance(pool, castPrefab, caster, definition, target, fromEngineer, angle);
    }

    private static IEnumerator CastDirectAfterAnimation(
        GameObject animationPrefab,
        GameObject spellPrefab,
        EngineerController caster,
        SpellDefinition definition,
        Vector3 target,
        float angle)
    {
        GameObjectPool pool = GameObjectPool.Instance;
        if (!pool) yield break;

        GameObject animation = pool.Get(animationPrefab);
        if (!animation) yield break;
        animation.transform.SetPositionAndRotation(target, Quaternion.Euler(0f, 0f, angle));
        SpriteRenderer castRenderer = animation.GetComponent<SpriteRenderer>();
        SpriteRenderer casterRenderer = caster.GetComponentInChildren<SpriteRenderer>();
        if (castRenderer && casterRenderer)
        {
            castRenderer.sortingLayerID = casterRenderer.sortingLayerID;
            castRenderer.sortingOrder = casterRenderer.sortingOrder + 1;
        }
        Animator animator = animation.GetComponent<Animator>();
        if (animator)
        {
            animator.Rebind();
            animator.Play(0, 0, 0f);
        }

        yield return new WaitForSeconds(definition.DirectCastTime);
        pool.Release(animation);

        if (caster && GameObjectPool.Instance)
            SpawnInstance(GameObjectPool.Instance, spellPrefab, caster, definition, target, true, angle);
    }

    private static bool SpawnInstance(
        GameObjectPool pool,
        GameObject castPrefab,
        EngineerController caster,
        SpellDefinition definition,
        Vector3 target,
        bool fromEngineer,
        float angle)
    {
        GameObject instance = pool.Get(castPrefab);
        if (!instance) return false;

        Vector3 position = definition.DeliveryType == SpellDeliveryType.DirectSpawn
            ? target
            : fromEngineer ? caster.ShootPoint + definition.SpawnOffset : Vector3.zero;
        instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angle));

        if (definition.DeliveryType == SpellDeliveryType.DirectSpawn)
        {
            IEngineerDirectSpellInstance direct = instance.GetComponent(
                typeof(IEngineerDirectSpellInstance)) as IEngineerDirectSpellInstance;
            if (direct != null)
            {
                direct.Initialize(caster, definition, target);
                return true;
            }

            Debug.LogError("[EngineerSpellCaster] 原地法术预制体根节点缺少 EngineerSpellDirectEffect。", instance);
            pool.Release(instance);
            return false;
        }

        IEngineerAimedSpellInstance aimed = instance.GetComponent(
            typeof(IEngineerAimedSpellInstance)) as IEngineerAimedSpellInstance;
        if (aimed != null)
        {
            aimed.Initialize(caster, definition, target);
            return true;
        }

        IEngineerSpellInstance spell = instance.GetComponent(
            typeof(IEngineerSpellInstance)) as IEngineerSpellInstance;
        if (spell != null)
        {
            spell.Initialize(caster, definition);
            return true;
        }

        Debug.LogError("[EngineerSpellCaster] 法术预制体根节点缺少施法执行器。", instance);
        pool.Release(instance);
        return false;
    }
}

/// <summary>
/// Implement on a spell prefab root when the spell does not need a target point.
/// </summary>
public interface IEngineerSpellInstance
{
    void Initialize(EngineerController caster, SpellDefinition definition);
}

/// <summary>
/// Implement on a spell prefab root when the spell needs a target point.
/// </summary>
public interface IEngineerAimedSpellInstance
{
    void Initialize(EngineerController caster, SpellDefinition definition, Vector3 target);
}

/// <summary>Implement on a direct-spawn spell prefab root.</summary>
public interface IEngineerDirectSpellInstance
{
    void Initialize(EngineerController caster, SpellDefinition definition, Vector3 target);
}
