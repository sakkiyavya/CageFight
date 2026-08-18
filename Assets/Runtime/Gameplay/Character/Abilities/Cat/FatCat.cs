using UnityEngine;

/// <summary>
/// Fat Cat（肥猫）机制：每次攻击对自身施加一层“虚假力量”（FalsePowerBuff），
/// 每层持续 layerDuration 秒（默认 4 秒）。
/// 虚假力量：每层 +1 击退（随攻击魔法等级成长）、可无限叠加、层独立计时、
/// 无视觉表现。
/// 通过 GameObjectProperty.OnAtt 事件接入，仅新增本脚本即可生效。
/// </summary>
public class FatCat : MonoBehaviour
{
    [Header("虚假力量")]
    [SerializeField, Min(0.1f)]
    private float layerDuration = 4f;       // 每层持续秒（4 秒）。

    private GameObjectProperty _prop;
    private FalsePowerBuff _falsePower;

    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _falsePower = gameObject.AddComponent<FalsePowerBuff>();
        _falsePower.SetDuration(layerDuration);
    }

    private void OnEnable()
    {
        if (_prop != null)
            _prop.OnAtt += HandleAttack;
    }

    private void OnDisable()
    {
        if (_prop != null)
            _prop.OnAtt -= HandleAttack;
    }

    /// <summary>
    /// 每次攻击：对自身施加一层虚假力量。
    /// </summary>
    private void HandleAttack()
    {
        if (_prop == null || _prop.isDead)
            return;

        _falsePower.ApplyBuff(_prop);
    }
}
