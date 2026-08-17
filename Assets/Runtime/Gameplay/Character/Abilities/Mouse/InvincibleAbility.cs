using UnityEngine;

/// <summary>
/// Invincible 机制：受到的减益（debuff）全部转化为自身的愤怒（AngerBuff）3 秒。
/// 实现 IDebuffConverter，由 CharacterHealth.OnCollide 在施加减益前询问并接管：
/// 原减益不再生效，改为自身叠加一层愤怒（增伤/受伤/暴击 + 橙红呼吸）。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
[RequireComponent(typeof(GameObjectProperty))]
public class InvincibleAbility : MonoBehaviour, IDebuffConverter
{
    [Header("转化配置")]
    [SerializeField, Min(0.1f)]
    private float angerDuration = 3f;        // 每个被转化的减益提供的愤怒持续秒数。

    private GameObjectProperty _prop;
    private AngerBuff _anger;                // 运行时创建的愤怒实例（仅作配置载体）。

    #region 生命周期与回调
    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();

        // 运行时创建并配置愤怒实例，避免预制体额外挂载组件。
        _anger = gameObject.AddComponent<AngerBuff>();
        _anger.SetDuration(angerDuration);
    }
    #endregion

    #region 减益转化
    /// <summary>
    /// 接管一个即将施加的减益：不再施加原减益，改为对自身叠加一层愤怒。
    /// </summary>
    /// <param name="debuff">被转化的减益实例（本实现不区分类型，统一转化为愤怒）。</param>
    /// <returns>转化成功时返回 <see langword="true"/>。</returns>
    public bool ConvertDebuff(BuffBase debuff)
    {
        if (_prop == null || _anger == null || _prop.isDead)
            return false;

        _anger.ApplyBuff(_prop);
        return true;
    }
    #endregion
}
