using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用 GameObject 对象池，支持多种预制体，单例模式。
/// </summary>
public class GameObjectPool : MonoBehaviour
{
    private class PoolData
    {
        public Queue<GameObject> queue = new Queue<GameObject>();                                                  // 当前预制体对应的空闲实例队列。
        public Transform subRoot;                                                                                  // 当前预制体实例在层级中的收纳节点。
    }

    public static GameObjectPool Instance { get; private set; }

    private Dictionary<GameObject, Queue<GameObject>> _pools = new Dictionary<GameObject, Queue<GameObject>>();    // 预制体到空闲实例队列的映射。
    private Dictionary<GameObject, GameObject> _instanceToPrefab = new Dictionary<GameObject, GameObject>();       // 池化实例到来源预制体的反向映射。
    private Dictionary<GameObject, Transform> _poolSubRoots = new Dictionary<GameObject, Transform>();             // 预制体到专用层级节点的映射。
    private Transform _poolRoot;                                                                                   // 所有池化对象的根节点。

    #region 生命周期与回调
    /// <summary>
    /// 初始化对象池单例和持久化根节点；场景中出现重复实例时销毁后创建的对象。
    /// </summary>
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            _poolRoot = new GameObject("GameObjectPool_Root").transform;
            DontDestroyOnLoad(_poolRoot.gameObject);
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    #endregion

    #region 公开接口
    /// <summary>
    /// 为指定预制体创建对象池，并提前生成一定数量的停用实例。
    /// </summary>
    /// <param name="prefab">需要建立对象池的预制体。</param>
    /// <param name="count">需要预先创建的实例数量。</param>
    public void InitPool(GameObject prefab, int count)
    {
        if (prefab == null) return;

        EnsurePoolExists(prefab);

        for (int i = 0; i < count; i++)
        {
            GameObject obj = CreateNewInstance(prefab);                                                            // 新创建的池化实例。
            RecycleToPool(prefab, obj);
        }
    }

    /// <summary>
    /// 从指定预制体的对象池中取出并激活一个实例；池为空时会自动创建新实例。
    /// </summary>
    /// <param name="prefab">需要获取实例的来源预制体。</param>
    /// <returns>可立即使用的激活实例；预制体为空时返回 <see langword="null"/>。</returns>
    public GameObject Get(GameObject prefab)
    {
        if (prefab == null) return null;

        EnsurePoolExists(prefab);

        GameObject obj;                                                                                            // 本次返回的实例。
        if (_pools[prefab].Count > 0)
        {
            obj = _pools[prefab].Dequeue();
        }
        else
        {
            obj = CreateNewInstance(prefab);
        }

        // obj.transform.SetParent(null);
        obj.SetActive(true);

        return obj;
    }
    /// <summary>
    /// 停用对象并放回其来源预制体的对象池；非池化实例会被销毁。
    /// </summary>
    /// <param name="obj">需要回收的对象实例。</param>
    public void Release(GameObject obj)
    {
        if (obj == null) return;

        if (_instanceToPrefab.TryGetValue(obj, out GameObject prefab))
        {
            RecycleToPool(prefab, obj);
        }
        else
        {
            Debug.LogWarning($"[GameObjectPool] 尝试回收一个非对象池生成的实例: {obj.name}");
            Destroy(obj);
        }
    }
    /// <summary>
    /// 查询池化实例最初由哪个预制体创建。
    /// </summary>
    /// <param name="instance">由该对象池创建的实例。</param>
    /// <returns>实例对应的来源预制体；实例为空或不属于对象池时返回 <see langword="null"/>。</returns>
    public GameObject GetPrefab(GameObject instance)
    {
        if (instance == null)
            return null;
        _instanceToPrefab.TryGetValue(instance, out GameObject prefab);
        return prefab;
    }
    /// <summary>
    /// 销毁指定预制体当前处于空闲状态的全部实例，并移除对应池和层级节点。
    /// </summary>
    /// <param name="prefab">需要清理对象池的预制体。</param>
    public void ClearPool(GameObject prefab)
    {
        if (prefab == null || !_pools.ContainsKey(prefab)) return;

        var queue = _pools[prefab];
        while (queue.Count > 0)
        {
            GameObject obj = queue.Dequeue();                                                                      // 待销毁的空闲实例。
            _instanceToPrefab.Remove(obj);
            Destroy(obj);
        }
        _pools.Remove(prefab);

        if (_poolSubRoots.TryGetValue(prefab, out Transform subRoot))
        {
            Destroy(subRoot.gameObject);
            _poolSubRoots.Remove(prefab);
        }
    }

    /// <summary>
    /// 销毁所有空闲实例及其层级节点，并清空对象池的全部索引。
    /// </summary>
    public void ClearAll()
    {
        foreach (var prefab in _pools.Keys)
        {
            var queue = _pools[prefab];
            while (queue.Count > 0)
            {
                GameObject obj = queue.Dequeue();                                                                  // 待销毁的空闲实例。
                Destroy(obj);
            }
        }
        _pools.Clear();
        _instanceToPrefab.Clear();

        foreach (var subRoot in _poolSubRoots.Values)
        {
            Destroy(subRoot.gameObject);
        }
        _poolSubRoots.Clear();
    }
    #endregion

    #region 内部辅助
    /// <summary>
    /// 确保指定预制体已经拥有空闲队列和专用层级节点。
    /// </summary>
    /// <param name="prefab">需要检查或初始化对象池的预制体。</param>
    private void EnsurePoolExists(GameObject prefab)
    {
        if (!_pools.ContainsKey(prefab))
        {
            _pools[prefab] = new Queue<GameObject>();
            
            // 为该 prefab 创建专用的子根节点
            GameObject subRootObj = new GameObject($"Pool_{prefab.name}");                                         // 当前预制体的池化层级节点。
            subRootObj.transform.SetParent(_poolRoot);
            _poolSubRoots[prefab] = subRootObj.transform;
        }
    }

    /// <summary>
    /// 在指定预制体的专用层级节点下创建实例，并登记实例与预制体的对应关系。
    /// </summary>
    /// <param name="prefab">用于创建实例的预制体。</param>
    /// <returns>新创建且尚未回收的实例。</returns>
    private GameObject CreateNewInstance(GameObject prefab)
    {
        GameObject obj = Instantiate(prefab, _poolSubRoots[prefab]);                                               // 新创建的实例。
        _instanceToPrefab[obj] = prefab;
        return obj;
    }

    /// <summary>
    /// 停用实例、移回对应层级节点，并加入预制体的空闲队列。
    /// </summary>
    /// <param name="prefab">实例的来源预制体。</param>
    /// <param name="obj">需要放回对象池的实例。</param>
    private void RecycleToPool(GameObject prefab, GameObject obj)
    {
        obj.SetActive(false);
        obj.transform.SetParent(_poolSubRoots[prefab]);
        _pools[prefab].Enqueue(obj);
    }
    #endregion
}
