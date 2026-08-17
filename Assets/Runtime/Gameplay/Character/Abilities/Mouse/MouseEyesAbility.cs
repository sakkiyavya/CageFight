using UnityEngine;

/// <summary>
/// Mouse eyes（鼠目）机制：每次攻击对自身施加一层精准（PreciseBuff），
/// 提升攻速 5%（随攻击魔法等级成长），每层独立 10 秒，可无限叠加。
/// 通过订阅既有接口 GameObjectProperty.OnAtt 在 ShootProjectile 发射后接入。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
[RequireComponent(typeof(GameObjectProperty))]
public class MouseEyesAbility : MonoBehaviour
{
    [Header("自身 Buff")]
    [SerializeField, Min(0.1f)]
    private float preciseDuration = 10f;     // 每次攻击获得一层精准的持续秒数。

    private GameObjectProperty _prop;
    private PreciseBuff _precise;            // 运行时创建的精准实例（仅作配置载体，状态在目标层管理器）。

    #region 生命周期与回调
    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();

        // 运行时创建并配置 buff 实例，避免预制体额外挂载组件。
        _precise = gameObject.AddComponent<PreciseBuff>();
        _precise.SetDuration(preciseDuration);
    }

    private void OnEnable()
    {
        if (_prop != null)
            _prop.OnAtt += HandleAttacked;
    }

    private void OnDisable()
    {
        if (_prop != null)
            _prop.OnAtt -= HandleAttacked;
    }
    #endregion

    #region 内部实现
    /// <summary>
    /// 响应攻击事件：对自身叠加一层精准。
    /// </summary>
    private void HandleAttacked()
    {
        if (_precise != null)
            _precise.ApplyBuff(_prop);
    }
    #endregion
}
