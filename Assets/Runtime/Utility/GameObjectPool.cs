using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用 GameObject 对象池，支持多种预制体，单例模式。
/// </summary>
public class GameObjectPool : MonoBehaviour
{
    private class PoolData
    {
        public Queue<GameObject> queue = new Queue<GameObject>();
        public Transform subRoot;
    }

    public static GameObjectPool Instance { get; private set; }

    private Dictionary<GameObject, Queue<GameObject>> _pools = new Dictionary<GameObject, Queue<GameObject>>();
    private Dictionary<GameObject, GameObject> _instanceToPrefab = new Dictionary<GameObject, GameObject>();
    private Dictionary<GameObject, Transform> _poolSubRoots = new Dictionary<GameObject, Transform>();
    private Transform _poolRoot;

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

    /// <summary>
    /// 初始化指定预制体的对象池并预热。
    /// </summary>
    /// <param name="prefab">预制体</param>
    /// <param name="count">预先生成的数量</param>
    public void InitPool(GameObject prefab, int count)
    {
        if (prefab == null) return;

        EnsurePoolExists(prefab);

        for (int i = 0; i < count; i++)
        {
            GameObject obj = CreateNewInstance(prefab);
            RecycleToPool(prefab, obj);
        }
    }

    /// <summary>
    /// 获取并激活对象（由外部处理变换信息）。
    /// </summary>
    public GameObject Get(GameObject prefab)
    {
        if (prefab == null) return null;

        EnsurePoolExists(prefab);

        GameObject obj;
        if (_pools[prefab].Count > 0)
        {
            obj = _pools[prefab].Dequeue();
        }
        else
        {
            obj = CreateNewInstance(prefab);
        }

        obj.transform.SetParent(null);
        obj.SetActive(true);

        return obj;
    }

    /// <summary>
    /// 回收对象到对象池。
    /// </summary>
    /// <param name="obj">要回收的对象实例</param>
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
    /// 清空指定预制体的对象池。
    /// </summary>
    public void ClearPool(GameObject prefab)
    {
        if (prefab == null || !_pools.ContainsKey(prefab)) return;

        var queue = _pools[prefab];
        while (queue.Count > 0)
        {
            GameObject obj = queue.Dequeue();
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
    /// 清空所有对象池。
    /// </summary>
    public void ClearAll()
    {
        foreach (var prefab in _pools.Keys)
        {
            var queue = _pools[prefab];
            while (queue.Count > 0)
            {
                GameObject obj = queue.Dequeue();
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

    private void EnsurePoolExists(GameObject prefab)
    {
        if (!_pools.ContainsKey(prefab))
        {
            _pools[prefab] = new Queue<GameObject>();
            
            // 为该 prefab 创建专用的子根节点
            GameObject subRootObj = new GameObject($"Pool_{prefab.name}");
            subRootObj.transform.SetParent(_poolRoot);
            _poolSubRoots[prefab] = subRootObj.transform;
        }
    }

    private GameObject CreateNewInstance(GameObject prefab)
    {
        GameObject obj = Instantiate(prefab, _poolSubRoots[prefab]);
        _instanceToPrefab[obj] = prefab;
        return obj;
    }

    private void RecycleToPool(GameObject prefab, GameObject obj)
    {
        obj.SetActive(false);
        obj.transform.SetParent(_poolSubRoots[prefab]);
        _pools[prefab].Enqueue(obj);
    }
}
