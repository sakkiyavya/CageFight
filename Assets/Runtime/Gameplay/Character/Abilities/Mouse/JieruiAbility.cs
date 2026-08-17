using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// jierui 机制：
/// 1. 每次攻击对自身叠加一层强化：最大生命 +10%、当前生命 +10%（不补齐最大生命差值），
///    叠加公式与巨化同理（层管理、无上限、加法叠加、逐层到期），无等级成长。
/// 2. 死亡后在原地生成奶酪（cheesePrefab），由奶酪提供范围治疗。
/// 通过订阅既有接口 GameObjectProperty.OnAtt 与 CharacterHealth.Died 接入。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
[RequireComponent(typeof(GameObjectProperty))]
[RequireComponent(typeof(CharacterHealth))]
public class JieruiAbility : MonoBehaviour
{
    [Header("攻击自我强化")]
    [SerializeField, Min(0f)]
    private float maxHpPercent = 0.10f;     // 每层最大生命/当前生命加成比例（10%）。
    [SerializeField, Min(0.1f)]
    private float layerDuration = 10f;      // 每层持续秒数。

    [Header("死亡奶酪")]
    [SerializeField, Tooltip("死亡时在原地生成的奶酪预制体（如 Huge cheese）")]
    private GameObject cheesePrefab;

    /// <summary>单层强化快照。</summary>
    private class Layer
    {
        public float percent;     // 本层快照的加成比例。
        public int hpGain;        // 本层加入时增加的当前生命，本层消失时扣除。
        public float expireTime;  // 本层到期时间。
    }

    private readonly List<Layer> layers = new List<Layer>();

    private GameObjectProperty _prop;
    private CharacterHealth _health;
    private int baseMaxHp;                   // 首层施加时快照的基础最大生命。
    private bool cheeseSpawned;              // 本次死亡是否已生成奶酪（防止重复）。

    #region 生命周期与回调
    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _health = GetComponent<CharacterHealth>();
    }

    private void OnEnable()
    {
        if (_prop != null)
            _prop.OnAtt += HandleAttacked;
        if (_health != null)
            _health.Died += HandleDied;
    }

    private void OnDisable()
    {
        if (_prop != null)
            _prop.OnAtt -= HandleAttacked;
        if (_health != null)
            _health.Died -= HandleDied;

        cheeseSpawned = false;
        layers.Clear();
    }
    #endregion

    #region 内部实现
    /// <summary>
    /// 响应攻击事件：叠加一层强化，增加最大生命与当前生命（不补齐差值）。
    /// </summary>
    private void HandleAttacked()
    {
        if (_prop == null || _prop.isDead)
            return;

        if (layers.Count == 0)
            baseMaxHp = propMaxHpSafe();

        int hpGain = Mathf.Max(1, Mathf.RoundToInt(_prop.currentHp * maxHpPercent));
        layers.Add(new Layer
        {
            percent = maxHpPercent,
            hpGain = hpGain,
            expireTime = Time.time + layerDuration,
        });

        ApplyMaxHp();
        _prop.currentHp = Mathf.Min(_prop.currentHp + hpGain, _prop.maxHp);
    }

    /// <summary>
    /// 响应死亡事件：在死亡位置生成奶酪。
    /// </summary>
    private void HandleDied(GameObject unit)
    {
        if (cheeseSpawned || cheesePrefab == null || GameObjectPool.Instance == null)
            return;

        cheeseSpawned = true;

        GameObject cheese = GameObjectPool.Instance.Get(cheesePrefab);
        if (cheese == null)
            return;

        cheese.transform.position = transform.position;

        CheeseHeal heal = cheese.GetComponent<CheeseHeal>();
        if (heal == null)
            heal = cheese.gameObject.AddComponent<CheeseHeal>();

        // 治疗基准为死亡单位（含强化层）的最大生命。
        heal.Init(_prop != null ? _prop.side : 0, propMaxHpSafe());
    }

    /// <summary>按当前全部层的比例求和后重算最大生命。</summary>
    private void ApplyMaxHp()
    {
        float total = 0f;
        for (int i = 0; i < layers.Count; i++)
            total += layers[i].percent;

        _prop.maxHp = Mathf.Max(1, Mathf.RoundToInt(baseMaxHp * (1f + total)));
    }

    private int propMaxHpSafe()
    {
        return _prop != null ? _prop.maxHp : 0;
    }

    private void Update()
    {
        // 倒序清理到期层：扣除该层增加的当前生命并重算最大生命。
        for (int i = layers.Count - 1; i >= 0; i--)
        {
            if (Time.time < layers[i].expireTime)
                continue;

            _prop.currentHp = Mathf.Clamp(_prop.currentHp - layers[i].hpGain, 0, _prop.maxHp);
            layers.RemoveAt(i);
            ApplyMaxHp();
        }
    }
    #endregion
}
