using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
[RequireComponent(typeof(CharacterHealth))]
public class CatMix : MonoBehaviour
{
    [SerializeField] private float range = 2f;
    [SerializeField, Range(0f, 1f)]
    private float hpLoss = 0.2f;

    private static readonly Collider2D[] hits =
        new Collider2D[32];

    private GameObjectProperty prop;
    private bool triggered;

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
    }

    private void OnEnable()
    {
        triggered = false;
    }

    private void Update()
    {
        if (triggered || !prop.isDead)
            return;

        triggered = true;
        TriggerNearby();
    }

    private void TriggerNearby()
    {
        int count = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            range,
            hits
        );

        for (int i = 0; i < count; i++)
        {
            CatMix target =
                hits[i].GetComponentInParent<CatMix>();

            if (target == null ||
                target == this ||
                target.prop.isDead)
            {
                continue;
            }

            // 防止同一单位的多个碰撞体重复扣血
            bool repeated = false;

            for (int j = 0; j < i; j++)
            {
                if (hits[j] != null &&
                    hits[j].GetComponentInParent<CatMix>()
                    == target)
                {
                    repeated = true;
                    break;
                }
            }

            if (!repeated)
                target.LoseHp();
        }
    }

    private void LoseHp()
    {
        int damageValue =
            Mathf.RoundToInt(prop.maxHp * hpLoss);

        Damage damage = Damage.DefaultDamage;
        damage.initialDamage = damageValue;
        damage.source = gameObject;
        damage.target = gameObject;
        damage.side = prop.side;
        damage.repel = 0f;

        GetComponent<CharacterHealth>()
            .TakeDamage(damage);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            range
        );
    }
}