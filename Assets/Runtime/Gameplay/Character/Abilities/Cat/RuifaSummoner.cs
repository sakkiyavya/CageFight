using UnityEngine;

/// <summary>
/// Ruifa（芮法）本体能力：每次攻击都会召唤自己的镜像（Ruifa M），
/// 同时扣除自身 1% 最大生命作为召唤代价；扣血后至少保留 1 点生命，不会因召唤自杀。
/// 扣血走 CharacterHealth 的受控入口（不触发受击、击退与死亡特效流程）。
/// </summary>
[RequireComponent(typeof(GameObjectProperty))]
[RequireComponent(typeof(CharacterHealth))]
public class RuifaSummoner : MonoBehaviour
{
    [Header("召唤代价")]
    [SerializeField, Min(0f)]
    private float summonHpPercent = 0.01f;      // 每次召唤扣除的自身最大生命百分比。

    private GameObjectProperty prop;
    private CharacterHealth health;

    #region 生命周期与回调
    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        health = GetComponent<CharacterHealth>();
    }

    private void OnEnable()
    {
        if (prop != null)
            prop.OnAtt += HandleAttacked;
    }

    private void OnDisable()
    {
        if (prop != null)
            prop.OnAtt -= HandleAttacked;
    }
    #endregion

    #region 内部实现
    /// <summary>
    /// 响应攻击事件（镜像已在 CharacterAI.ShootProjectile 中生成），
    /// 扣除最大生命对应百分比的当前生命，并保证至少剩余 1 点生命。
    /// </summary>
    private void HandleAttacked()
    {
        if (prop == null || health == null || prop.isDead)
            return;

        int cost = Mathf.Max(1, Mathf.RoundToInt(prop.maxHp * summonHpPercent));
        int remain = Mathf.Max(1, prop.currentHp - cost);

        if (remain != prop.currentHp)
            health.SetHp(remain);
    }
    #endregion
}
