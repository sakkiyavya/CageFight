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



    private readonly Queue<GameObject> _pool = new Queue<GameObject>();                                   // 当前可复用的停用跳字实例。

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

        // 初始化预先载入对象
        if (damageTextPrefab != null)
        {
            for (int i = 0; i < initialSize; i++)
            {
                CreateNewInstance();
            }
        }
    }
    #endregion

    #region 内部辅助
    /// <summary>
    /// 创建一个跳字实例，将其停用并加入空闲队列。
    /// </summary>
    /// <returns>新创建且已经进入对象池的跳字对象。</returns>
    private GameObject CreateNewInstance()
    {
        GameObject obj = Instantiate(damageTextPrefab, transform);                                        // 新创建的跳字对象。
        obj.SetActive(false);
        _pool.Enqueue(obj);
        return obj;
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

        GameObject obj = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(damageTextPrefab, transform);    // 本次使用的跳字对象。

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

        GameObject obj = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(damageTextPrefab, transform);    // 本次使用的跳字对象。

        obj.transform.position = pos;
        obj.SetActive(true);

        DamageText textComp = obj.GetComponent<DamageText>();                                             // 控制文本内容和动画的组件。
        if (textComp != null)
        {
            textComp.Init(value, healColor, this);
        }
    }
    #endregion

    #region 公开接口
    /// <summary>
    /// 停用播放结束的跳字对象，并将其放回空闲队列。
    /// </summary>
    /// <param name="obj">需要回收的跳字对象。</param>
    public void ReturnToPool(GameObject obj)
    {
        obj.SetActive(false);
        _pool.Enqueue(obj);
    }
    #endregion
}
