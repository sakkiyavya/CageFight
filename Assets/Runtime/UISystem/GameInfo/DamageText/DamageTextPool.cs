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
            Destroy(gameObject);
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
    /// 经 GameObjectPool 取得一个跳字实例（池服务不可用时回退到直接实例化）。
    /// </summary>
    private GameObject GetInstance()
    {
        if (GameObjectPool.Instance != null)
            return GameObjectPool.Instance.Get(damageTextPrefab);

        return Instantiate(damageTextPrefab, transform);
    }

    /// <summary>
    /// 停用播放结束的跳字对象，并经 GameObjectPool 归还。
    /// </summary>
    /// <param name="obj">需要回收的跳字对象。</param>
    public void ReturnToPool(GameObject obj)
    {
        if (GameObjectPool.Instance != null)
        {
            GameObjectPool.Instance.Release(obj);
            return;
        }

        obj.SetActive(false);
        Destroy(obj);
    }
    #endregion
}
