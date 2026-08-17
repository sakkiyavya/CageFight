using UnityEngine;

/// <summary>
/// General Cat（猫将军）：每次受到弹幕攻击（投射物命中）时获得一层“护甲”，
/// 每层持续 armorDuration 秒（默认 3 秒）。
/// 弹幕命中由 DamageSource / BOOMBroProjectile / CatGrassSmokeProjectile 统一回调
/// IProjectileImpactHandler 通知被命中的目标，本脚本实现该接口接收回调。
/// 护甲（ArmorBuff）：每层提供免伤点数（默认 10 点，随创造者单位等级成长），
/// 可无限叠加、蓝色呼吸光效；施加时以自身为创造者（等级接缝，当前等级系统未建成时为 10 点/层）。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
public class GeneralCat : MonoBehaviour, IProjectileImpactHandler
{
    [Header("护甲")]
    [SerializeField, Min(0.1f)]
    private float armorDuration = 3f;       // 每层护甲持续秒（3 秒）。

    private GameObjectProperty _prop;
    private ArmorBuff _armor;
    private Damage _selfDamage;             // 以自身为创造者的施放伤害数据（复用，避免构造）。

    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _armor = gameObject.AddComponent<ArmorBuff>();
        _armor.SetDuration(armorDuration);
        _selfDamage = Damage.DefaultDamage;
    }

    private void OnEnable()
    {
        _selfDamage.source = gameObject;
    }

    /// <summary>
    /// 受到弹幕攻击：获得一层护甲（以自身为创造者，供等级接缝读取）。
    /// </summary>
    public void OnProjectileDamageTriggered(Vector3 impactPosition)
    {
        if (_prop == null || _prop.isDead)
            return;

        _armor.ApplyBuff(_prop, _selfDamage);
    }
}
