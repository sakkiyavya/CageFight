// using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class BuildingHealth : MonoBehaviour, ICollide
{
    private int hp; public int HP => hp;                                      // 当前建筑生命值及其只读访问器。
    private float hideTime = -1f;                                             // 血条自动隐藏的游戏时间，负数表示未计时。

    private GameObjectProperty _prop;                                         // 提供阵营、最大生命和血条持续时间的建筑属性。
    public GameObject HpBarUp;                                                // 通过横向缩放显示剩余生命的前景条。
    public GameObject HpBarBottom;                                            // 血条背景对象。

    // ICollide implementation retained
    #region 阵营判定
    /// <summary>
    /// 比较伤害来源阵营与建筑阵营，判断是否应忽略友方碰撞。
    /// </summary>
    /// <param name="damage">包含来源阵营的伤害数据。</param>
    /// <returns>伤害阵营与建筑阵营相同时返回 <see langword="true"/>。</returns>
    public bool IsFriendly(Damage damage)
    {
        GameObjectProperty prop = EnsureProp();
        return prop != null && damage.side == prop.side;
    }
    #endregion
    #region 碰撞与生命周期回调
    /// <summary>
    /// 接收敌方碰撞伤害，记录来源名称并将伤害交给建筑伤害计算入口。
    /// </summary>
    /// <param name="damage">碰撞源携带的伤害数据。</param>
    /// <returns>建筑伤害计算后的结果。</returns>
    public Damage OnCollide(Damage damage)
    {
        return TakeDamage(damage);
    }
    
    /// <summary>
    /// 缓存同一对象上的建筑属性组件；同对象缺失时向上级物体兜底查找
    /// （防御预制体把 BuildingHealth 挂在子物体、GameObjectProperty 在根物体的配置）。
    /// </summary>
    private void Awake()
    {
        EnsureProp();
    }

    /// <summary>
    /// 懒解析 GameObjectProperty：优先同物体，其次父级物体。
    /// 同时解决 Awake 执行顺序不确定导致的空引用（BuildUP.Awake 可能先于本组件 Awake 调用 SetMaxHp）。
    /// </summary>
    private GameObjectProperty EnsureProp()
    {
        if (_prop == null)
        {
            _prop = GetComponent<GameObjectProperty>();
            if (_prop == null)
                _prop = GetComponentInParent<GameObjectProperty>();
        }
        return _prop;
    }


    /// <summary>
    /// 初始化血条缩放，并在建筑开始时隐藏血条。
    /// </summary>
    private void Start()
    {
        ApplyBarVisual();
        SetBarActive(false);
    }

    /// <summary>
    /// 达到预定隐藏时间时关闭血条并清除计时状态。
    /// </summary>
    private void Update()
    {
        if (hideTime >= 0f && Time.time >= hideTime)
        {
            SetBarActive(false);
            hideTime = -1f;
        }
    }
    #endregion

    #region 血条控制
    /// <summary>
    /// 将百分比限制在 0 到 1 后换算为建筑生命值，并刷新和临时显示血条。
    /// </summary>
    /// <param name="percent">相对于最大生命值的目标比例。</param>
    public void SetPercentHp(float percent)
    {
        GameObjectProperty prop = EnsureProp();
        if (prop == null)
            return;

        hp = Mathf.RoundToInt(prop.maxHp * Mathf.Clamp01(percent));
        ApplyBarVisual();
        ShowBarTemporarily();
    }

    /// <summary>
    /// 按当前生命值刷新血条，并重新开始自动隐藏计时。
    /// </summary>
    public void SetHpbar()
    {
        ApplyBarVisual();
        ShowBarTemporarily();
    }
    #endregion

    #region 伤害与治疗
    /// <summary>
    /// 调用全局伤害计算器生成伤害结果，并把最终伤害扣除到建筑生命值，
    /// 刷新血条并在生命归零时标记建筑死亡。
    /// </summary>
    /// <param name="damage">需要计算的原始伤害数据。</param>
    /// <returns>写入最终伤害值后的伤害结果。</returns>
    public Damage TakeDamage(Damage damage)
    {
        Damage d = DamageComputor.DamageCompute(damage);
        hp = Mathf.Max(0, hp - d.finalDamage);
        ApplyBarVisual();
        ShowBarTemporarily();
        if (_prop != null)
            _prop.isDead = hp <= 0;

        // 未命中（目盲等）同样弹出 miss 跳字。
        if (d.missed)
            DamageTextPool.Instance.ShowMiss(transform.position + Vector3.up);

        return d;
    }

    /// <summary>
    /// 建筑治疗入口；当前尚未实现，调用时会抛出 <see cref="System.NotImplementedException"/>。
    /// </summary>
    /// <param name="amount">计划恢复的生命值。</param>
    public void Heal(int amount)
    {
        // TODO: Implement heal logic.
        throw new System.NotImplementedException();
    }
    #endregion

    #region 生命值快捷操作
    /// <summary>
    /// 恢复满生命入口；当前尚未实现，调用时会抛出 <see cref="System.NotImplementedException"/>。
    /// </summary>
    public void RestoreFullHp()
    {
        // TODO: Implement full HP restore logic.
        throw new System.NotImplementedException();
    }

    /// <summary>
    /// 将生命降为零的入口；当前尚未实现，调用时会抛出 <see cref="System.NotImplementedException"/>。
    /// </summary>
    public void ReduceToZero()
    {
        // TODO: Implement HP depletion logic.
        throw new System.NotImplementedException();
    }
    #endregion

    #region 死亡与复活
    /// <summary>
    /// 建筑死亡入口；当前尚未实现，调用时会抛出 <see cref="System.NotImplementedException"/>。
    /// </summary>
    public void Die()
    {
        // TODO: Implement death logic.
        throw new System.NotImplementedException();
    }

    /// <summary>
    /// 建筑复活入口；当前尚未实现，调用时会抛出 <see cref="System.NotImplementedException"/>。
    /// </summary>
    public void Revive()
    {
        // TODO: Implement revive logic.
        throw new System.NotImplementedException();
    }
    #endregion

    #region 生命状态查询与设置
    /// <summary>
    /// 查询建筑是否死亡（生命值归零）。
    /// </summary>
    /// <returns>生命值小于等于 0 时返回 <see langword="true"/>。</returns>
    public bool IsDead()
    {
        return hp <= 0;
    }

    /// <summary>
    /// 查询当前生命比例；当前尚未实现，调用时会抛出 <see cref="System.NotImplementedException"/>。
    /// </summary>
    /// <returns>实现后应返回当前生命值除以最大生命值的比例。</returns>
    public float GetHpPercent()
    {
        // TODO: Implement HP percent query.
        throw new System.NotImplementedException();
    }

    /// <summary>
    /// 直接设置建筑生命值，限制在有效范围内并刷新血条。
    /// </summary>
    /// <param name="value">计划设置的生命值。</param>
    public void SetHp(int value)
    {
        GameObjectProperty prop = EnsureProp();
        if (prop == null)
            return;

        hp = Mathf.Clamp(value, 0, prop.maxHp);
        ApplyBarVisual();
    }

    /// <summary>
    /// 设置建筑最大生命并保持当前生命不超过新上限（建筑升级等受控系统专用；
    /// 业务脚本不得直接写 maxHp）。
    /// </summary>
    /// <param name="value">新的最大生命值。</param>
    public void SetMaxHp(int value)
    {
        GameObjectProperty prop = EnsureProp();
        if (prop == null)
        {
            Debug.LogError("[BuildingHealth] 未找到 GameObjectProperty（请把本组件与 GameObjectProperty 放在同一物体或根物体上），无法设置最大生命。", this);
            return;
        }

        prop.maxHp = Mathf.Max(1, value);
        hp = Mathf.Clamp(hp, 0, prop.maxHp);
        ApplyBarVisual();
    }
    #endregion

    #region 血条显示辅助
    /// <summary>
    /// 根据当前生命值占最大生命值的比例更新前景血条横向缩放。
    /// </summary>
    private void ApplyBarVisual()
    {
        if (HpBarUp == null)
            return;

        GameObjectProperty prop = EnsureProp();
        if (prop == null)
            return;

        float scaleX = prop.maxHp > 0 ? (float)hp / prop.maxHp : 0f;    // 血条横向填充比例。
        HpBarUp.transform.localScale = new Vector3(scaleX, 1f, 1f);
    }

    /// <summary>
    /// 显示血条，并按建筑配置设置下一次自动隐藏时间。
    /// </summary>
    private void ShowBarTemporarily()
    {
        GameObjectProperty prop = EnsureProp();
        if (prop == null)
            return;

        SetBarActive(true);
        hideTime = Time.time + prop.barSustainTime;
    }

    /// <summary>
    /// 同时设置血条前景和背景对象的激活状态。
    /// </summary>
    /// <param name="active">是否显示整组血条对象。</param>
    private void SetBarActive(bool active)
    {
        if (HpBarUp != null)
        {
            HpBarUp.SetActive(active);
        }

        if (HpBarBottom != null)
        {
            HpBarBottom.SetActive(active);
        }
    }
    #endregion
}
