using System.Collections.Generic;
using UnityEngine;

public class DamageTextPool : MonoBehaviour
{
    private static DamageTextPool _instance;
    public static DamageTextPool Instance => _instance;

    [Header("对象池预制体")]
    [SerializeField] private GameObject damageTextPrefab;
    [SerializeField] private int initialSize = 50;
    [SerializeField] Color damageColor = Color.red;
    [SerializeField] Color healColor = Color.green;



    private readonly Queue<GameObject> _pool = new Queue<GameObject>();

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 初始化预先载入对象
        if (damageTextPrefab != null)
        {
            for (int i = 0; i < initialSize; i++)
            {
                CreateNewInstance();
            }
        }
    }

    private GameObject CreateNewInstance()
    {
        GameObject obj = Instantiate(damageTextPrefab, transform);
        obj.SetActive(false);
        _pool.Enqueue(obj);
        return obj;
    }

    /// <summary>
    /// 全局调用展示伤害数值。
    /// </summary>
    public void ShowDamage(Damage damage, Vector3 pos)
    {
        if (damageTextPrefab == null) return;

        GameObject obj = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(damageTextPrefab, transform);

        obj.transform.position = pos;
        obj.SetActive(true);

        DamageText textComp = obj.GetComponent<DamageText>();
        if (textComp != null)
        {
            textComp.Init(damage.finalDamage, damageColor, this);
        }
    }

    public void ShowHeal(int value, Vector3 pos)
    {
        if (damageTextPrefab == null) return;

        GameObject obj = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(damageTextPrefab, transform);

        obj.transform.position = pos;
        obj.SetActive(true);

        DamageText textComp = obj.GetComponent<DamageText>();
        if (textComp != null)
        {
            textComp.Init(value, healColor, this);
        }
    }

    /// <summary>
    /// 归还对象至队列。
    /// </summary>
    public void ReturnToPool(GameObject obj)
    {
        obj.SetActive(false);
        _pool.Enqueue(obj);
    }
}
