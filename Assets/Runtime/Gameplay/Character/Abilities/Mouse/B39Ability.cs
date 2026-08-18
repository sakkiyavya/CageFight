using UnityEngine;

/// <summary>
/// B-39 机制：每次受到减益（debuff）时，convertChance（默认 50%）概率把该减益
/// 转化为自身“护甲”（ArmorBuff）armorDuration（默认 3）秒——原减益不再施加。
/// 实现 IDebuffConverter，由 CharacterHealth.OnCollide 在施加减益前询问并接管
/// （与 Invincible 转化愤怒为同一接缝）；掷骰失败时返回 false，减益照常生效。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
[RequireComponent(typeof(GameObjectProperty))]
public class B39Ability : MonoBehaviour, IDebuffConverter
{
    [Header("转化配置")]
    [SerializeField, Range(0f, 1f)]
    private float convertChance = 0.5f;      // 每次受到减益时的转化概率（50%）。
    [SerializeField, Min(0.1f)]
    private float armorDuration = 3f;        // 转化获得的护甲持续秒数。

    private GameObjectProperty _prop;
    private ArmorBuff _armor;                // 运行时创建的护甲实例（仅作配置载体）。

    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();

        // 运行时创建并配置护甲实例，避免预制体额外挂载组件。
        _armor = gameObject.AddComponent<ArmorBuff>();
        _armor.SetDuration(armorDuration);
    }

    /// <summary>
    /// 尝试转化一个即将施加的减益：掷骰成功则改为对自身叠加一层护甲，
    /// 原减益不再施加；掷骰失败返回 false，减益照常生效。
    /// </summary>
    /// <param name="debuff">被转化的减益实例（本实现不区分类型，统一概率转化）。</param>
    /// <returns>转化成功时返回 <see langword="true"/>。</returns>
    public bool ConvertDebuff(BuffBase debuff)
    {
        if (_prop == null || _armor == null || _prop.isDead)
            return false;

        // 50% 概率判定：失败则放行原减益。
        if (Random.value >= convertChance)
            return false;

        // 护甲施加失败时放行原减益，避免吞掉减益却没有转化收益。
        if (!_armor.ApplyBuff(_prop))
            return false;

        // 登记 currentBuff（单条登记，避免重复条目累积）。
        if (!_prop.currentBuff.Contains(_armor))
            _prop.currentBuff.Add(_armor);

        return true;
    }
}
