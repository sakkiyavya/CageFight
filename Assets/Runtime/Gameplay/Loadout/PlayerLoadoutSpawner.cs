using System.Collections;
using UnityEngine;

/// <summary>
/// 由 GameplayState 明确调用的工程师生成点。所有可复用实例均经 GameObjectPool 获取和归还。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerLoadoutSpawner : MonoBehaviour
{
    [SerializeField] private PlayerLoadoutManager loadout;
    [SerializeField] private Transform engineerSpawnPoint;
    [SerializeField] private bool doNotSpawnIfEngineerExists = true;

    private GameObject spawnedEngineer;
    private GameObject spawnedRaceEffect;

    /// <summary>等待存档准备完成后生成已选择的工程师；仅由 GameplayState 在入局时调用。</summary>
    public IEnumerator SpawnSelectedEngineerRoutine()
    {
        if (!loadout)
        {
            Debug.LogError("[PlayerLoadoutSpawner] 未配置 PlayerLoadoutManager。", this);
            yield break;
        }

        while (!loadout.IsReady) yield return null;
        SpawnSelectedEngineer();
    }

    /// <summary>生成当前选择；资源未预载或配置无效时返回 false。</summary>
    public bool SpawnSelectedEngineer()
    {
        if (spawnedEngineer) return true;
        if (doNotSpawnIfEngineerExists && EngineerController.Active) return true;
        if (!loadout || !loadout.TryGetSelectedEngineer(out EngineerDefinition engineer))
        {
            Debug.LogError("[PlayerLoadoutSpawner] 未能解析当前工程师。", this);
            return false;
        }

        ResourceManager resourceManager = ResourceManager.Instance;
        GameObjectPool pool = GameObjectPool.Instance;
        GameObject prefab = resourceManager ? resourceManager.GetGameObject(engineer.PrefabKey) : null;
        if (!pool || !prefab)
        {
            Debug.LogError("[PlayerLoadoutSpawner] 工程师预制体未预载或对象池未就绪。", this);
            return false;
        }

        spawnedEngineer = pool.Get(prefab);
        Transform spawnTransform = engineerSpawnPoint ? engineerSpawnPoint : transform;
        spawnedEngineer.transform.SetPositionAndRotation(
            spawnTransform.position,
            spawnTransform.rotation);

        EngineerController controller = spawnedEngineer.GetComponent<EngineerController>();
        if (!controller)
        {
            Debug.LogError("[PlayerLoadoutSpawner] 工程师预制体缺少 EngineerController。", spawnedEngineer);
            pool.Release(spawnedEngineer);
            spawnedEngineer = null;
            return false;
        }

        SpawnRaceEffect(controller, resourceManager, pool);
        return true;
    }

    /// <summary>在退出局内状态时归还工程师和可选的种族效果。</summary>
    public void ReleaseSpawnedEngineer()
    {
        GameObjectPool pool = GameObjectPool.Instance;
        if (pool && spawnedRaceEffect) pool.Release(spawnedRaceEffect);
        if (pool && spawnedEngineer) pool.Release(spawnedEngineer);
        spawnedRaceEffect = null;
        spawnedEngineer = null;
    }

    private void OnDisable() => ReleaseSpawnedEngineer();

    private void SpawnRaceEffect(
        EngineerController engineer,
        ResourceManager resourceManager,
        GameObjectPool pool)
    {
        if (!loadout.TryGetSelectedRace(out RaceDefinition race) ||
            string.IsNullOrWhiteSpace(race.RuntimeEffectPrefabKey))
            return;

        GameObject effectPrefab = resourceManager.GetGameObject(race.RuntimeEffectPrefabKey);
        if (!effectPrefab)
        {
            Debug.LogWarning("[PlayerLoadoutSpawner] 种族效果预制体未预载。", this);
            return;
        }

        spawnedRaceEffect = pool.Get(effectPrefab);
        IRaceRuntimeEffect effect = spawnedRaceEffect.GetComponent(
            typeof(IRaceRuntimeEffect)) as IRaceRuntimeEffect;
        if (effect != null)
        {
            effect.Initialize(race, engineer);
            return;
        }

        Debug.LogError("[PlayerLoadoutSpawner] 种族效果根节点缺少 IRaceRuntimeEffect。", spawnedRaceEffect);
        pool.Release(spawnedRaceEffect);
        spawnedRaceEffect = null;
    }
}
