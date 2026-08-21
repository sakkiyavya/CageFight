using System.Collections.Generic;
using UnityEngine;

public class DamageTextPool : MonoBehaviour
{
    private static DamageTextPool _instance;                                                              // 伤害跳字对象池单例。
    public static DamageTextPool Instance => _instance;                                                   // 当前可访问的跳字对象池。

    [Header("对象池预制体")]
    [SerializeField] private GameObject damageTextPrefab;                                                 // 用于创建伤害或治疗跳字的预制体。
    [SerializeField] private int initialSize = 50;                                                        // 启动时预热的跳字实例数量。
    [SerializeField] Color damageColor = Color.red;                                                       // 伤害数值使用的文本颜色。
    [SerializeField] Color healColor = Color.green;                                                       // 治疗数值使用的文本颜色。
    [SerializeField] Color missColor = Color.white;                                                       // 未命中（miss）跳字使用的文本颜色。

    private bool warnedPoolMissing;                                                                       // 对象池未就绪的一次性警告标记。

    #region 生命周期与回调
    /// <summary>
    /// 建立对象池单例，并按配置数量预先创建停用的跳字实例。
    /// </summary>
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else
        {
            // 重复实例：不销毁场景对象，仅停用本组件并保留首个实例（规范禁止业务脚本 Destroy）。
            Debug.LogWarning("[DamageTextPool] 场景中存在重复实例，本组件已停用。", this);
            enabled = false;
            return;
        }

        // 预热：经 GameObjectPool 创建并立即归还指定数量的跳字实例。
        if (damageTextPrefab != null && GameObjectPool.Instance != null)
        {
            for (int i = 0; i < initialSize; i++)
            {
                GameObject obj = GameObjectPool.Instance.Get(damageTextPrefab);
                obj.SetActive(false);
                GameObjectPool.Instance.Release(obj);
            }
        }
    }
    #endregion

    #region 游戏逻辑
    /// <summary>
    /// 从池中取得跳字对象，在指定世界坐标以伤害颜色显示最终伤害值。
    /// </summary>
    /// <param name="damage">包含最终伤害数值的伤害数据。</param>
    /// <param name="pos">跳字出现的世界坐标。</param>
    public void ShowDamage(Damage damage, Vector3 pos)
    {
        if (damageTextPrefab == null) return;

        GameObject obj = GetInstance();                                                             // 本次使用的跳字对象。
        if (obj == null) return;                                                                   // 对象池未就绪：安全失败，不显示跳字。

        obj.transform.position = pos;
        obj.SetActive(true);

        DamageText textComp = obj.GetComponent<DamageText>();                                             // 控制文本内容和动画的组件。
        if (textComp != null)
        {
            textComp.Init(damage.finalDamage, damageColor, this);
        }
    }

    /// <summary>
    /// 从池中取得跳字对象，在指定世界坐标以治疗颜色显示恢复量。
    /// </summary>
    /// <param name="value">需要显示的治疗数值。</param>
    /// <param name="pos">跳字出现的世界坐标。</param>
    public void ShowHeal(int value, Vector3 pos)
    {
        if (damageTextPrefab == null) return;

        GameObject obj = GetInstance();                                                             // 本次使用的跳字对象。
        if (obj == null) return;                                                                   // 对象池未就绪：安全失败，不显示跳字。

        obj.transform.position = pos;
        obj.SetActive(true);

        DamageText textComp = obj.GetComponent<DamageText>();                                             // 控制文本内容和动画的组件。
        if (textComp != null)
        {
            textComp.Init(value, healColor, this);
        }
    }

    /// <summary>
    /// 从池中取得跳字对象，在指定世界坐标以未命中颜色显示 “miss” 字样。
    /// </summary>
    /// <param name="pos">跳字出现的世界坐标。</param>
    public void ShowMiss(Vector3 pos)
    {
        if (damageTextPrefab == null) return;

        GameObject obj = GetInstance();                                                             // 本次使用的跳字对象。
        if (obj == null) return;                                                                   // 对象池未就绪：安全失败，不显示跳字。

        obj.transform.position = pos;
        obj.SetActive(true);

        DamageText textComp = obj.GetComponent<DamageText>();                                             // 控制文本内容和动画的组件。
        if (textComp != null)
        {
            textComp.Init("miss", missColor, this);
        }
    }
    #endregion

    #region 公开接口
    /// <summary>
    /// 经 GameObjectPool 取得一个跳字实例；池服务未就绪时安全失败并返回 null（规范禁止 Instantiate 回退）。
    /// 跳字是 UGUI 文本，必须在画布层级下才能渲染：池化实例默认收纳在无画布的池根节点，
    /// 取出时挂到本对象所在的画布下；worldPositionStays=false 保持局部缩放为 1，
    /// 最终屏幕尺寸由画布自身缩放决定，随后由调用方设置世界位置。
    /// </summary>
    private GameObject GetInstance()
    {
        if (GameObjectPool.Instance != null)
        {
            GameObject obj = GameObjectPool.Instance.Get(damageTextPrefab);
            if (obj != null && transform.parent != null)
                obj.transform.SetParent(transform.parent, false);
            return obj;
        }

        // 对象池未就绪时安全失败：不实例化新对象（规范禁止业务脚本 Instantiate 兜底）。
        if (!warnedPoolMissing)
        {
            warnedPoolMissing = true;
            Debug.LogWarning("[DamageTextPool] GameObjectPool 未就绪，无法显示跳字。", this);
        }
        return null;
    }

    /// <summary>
    /// 停用播放结束的跳字对象，并经 GameObjectPool 归还；池服务未就绪时仅停用实例。
    /// </summary>
    /// <param name="obj">需要回收的跳字对象。</param>
    public void ReturnToPool(GameObject obj)
    {
        if (GameObjectPool.Instance != null)
        {
            GameObjectPool.Instance.Release(obj);
            return;
        }

        // 对象池未就绪：仅停用实例，不 Destroy（规范禁止业务脚本销毁回退）。
        if (!warnedPoolMissing)
        {
            warnedPoolMissing = true;
            Debug.LogWarning("[DamageTextPool] GameObjectPool 未就绪，跳字实例仅停用未回收。", this);
        }
        obj.SetActive(false);
    }
    #endregion
}
