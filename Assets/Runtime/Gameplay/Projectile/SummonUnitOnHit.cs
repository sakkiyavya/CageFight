using UnityEngine;

[RequireComponent(typeof(DamageSource))]
public class SummonUnitOnHit : MonoBehaviour
{
    public GameObject[] unitPrefabs;

    [Tooltip("勾选后，从列表中随机召唤一个兵种")]
    public bool randomSummon;

    [Range(0.01f, 1f)]
    public float summonedHpPercent = 1f;

    private DamageSource damageSource;
    private bool hasSummoned;

    private void Awake()
    {
        damageSource = GetComponent<DamageSource>();
    }

    private void OnEnable()
    {
        hasSummoned = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasSummoned || unitPrefabs == null || unitPrefabs.Length == 0)
            return;

        CharacterHealth targetHealth =
            other.GetComponent<CharacterHealth>();

        if (targetHealth == null)
            return;

        if (targetHealth.IsFriendly(damageSource.damage))
            return;

        hasSummoned = true;

        if (randomSummon)
        {
            int index = Random.Range(0, unitPrefabs.Length);
            Summon(unitPrefabs[index], other.transform.position);
        }
        else
        {
            foreach (GameObject prefab in unitPrefabs)
                Summon(prefab, other.transform.position);
        }
    }

    private void Summon(GameObject prefab, Vector3 position)
    {
        if (prefab == null)
            return;

        GameObject unit = GameObjectPool.Instance.Get(prefab);

        if (unit == null)
            return;

        unit.transform.position = position;

        GameObjectProperty unitProp =
            unit.GetComponent<GameObjectProperty>();

        if (unitProp != null)
            unitProp.side = damageSource.damage.side;

        CharacterHealth health =
            unit.GetComponent<CharacterHealth>();

        if (health != null)
            health.SetPercentHp(summonedHpPercent);
    }
}
