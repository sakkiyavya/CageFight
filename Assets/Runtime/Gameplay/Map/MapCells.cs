using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// 管理二维地图网格及每个格子的占用对象集合，为放置、寻路和索敌逻辑提供查询。
/// </summary>
[ExecuteAlways]
public class MapCells : MonoBehaviour
{
    static MapCells instance;                              // 地图网格管理器单例。
    public static MapCells Instance => instance;           // 当前可访问的网格管理器。
    public int Version => version;                         // 网格结构最近一次重建后的版本号。

    [Header("地图尺寸")]
    public int width = 20;                                 // 地图横向格子数量。
    public int height = 20;                                // 地图纵向格子数量。

    private HashSet<GameObject>[,] cellData;               // 每个网格当前登记的占用对象集合。
    private int version;                                   // 网格重新初始化的次数。

    #region 生命周期与回调
    /// <summary>
    /// 建立地图网格单例；重复实例会被立即销毁，首个实例会创建空网格数据。
    /// </summary>
    private void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this)
        {
            DestroyImmediate(gameObject);
            return;
        }

        InitializeGrid();
    }
    #endregion

    #region 网格管理
    /// <summary>
    /// 按当前宽高重新创建全部格子的空占用集合，并递增版本号通知依赖对象刷新登记。
    /// </summary>
    public void InitializeGrid()
    {
        cellData = new HashSet<GameObject>[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cellData[x, y] = new HashSet<GameObject>();
            }
        }

        version++;
    }

    /// <summary>
    /// 将同一占用对象登记到给定列表中的所有有效网格。
    /// </summary>
    /// <param name="cells">需要登记占用的网格坐标。</param>
    /// <param name="occupier">占用这些网格的游戏对象。</param>
    public void UseCells(List<Vector2Int> cells, GameObject occupier)
    {
        if (occupier == null || cellData == null) return;

        foreach (var pos in cells)
        {
            int x = pos.x;                                 // 当前网格横坐标。
            int y = pos.y;                                 // 当前网格纵坐标。

            if (IsInRange(x, y))
            {
                cellData[x, y].Add(occupier);
            }
        }
    }

    /// <summary>
    /// 从给定列表中的所有有效网格移除指定占用对象。
    /// </summary>
    /// <param name="cells">需要释放占用的网格坐标。</param>
    /// <param name="occupier">需要从网格中移除的游戏对象。</param>
    public void UnuseCells(List<Vector2Int> cells, GameObject occupier)
    {
        if (occupier == null || cellData == null) return;

        foreach (var pos in cells)
        {
            int x = pos.x;                                 // 当前网格横坐标。
            int y = pos.y;                                 // 当前网格纵坐标。

            if (IsInRange(x, y))
            {
                cellData[x, y].Remove(occupier);
            }
        }
    }

    /// <summary>
    /// 判断一组网格中是否存在越界坐标或至少一个已被对象占用的格子。
    /// 未初始化网格时按不可用处理。
    /// </summary>
    /// <param name="cells">需要整体检查的网格坐标。</param>
    /// <returns>任一格越界、被占用或网格未初始化时返回 <see langword="true"/>。</returns>
    public bool IsUse(List<Vector2Int> cells)
    {
        if (cellData == null) return true;

        foreach (var pos in cells)
        {
            int x = pos.x;                                 // 当前网格横坐标。
            int y = pos.y;                                 // 当前网格纵坐标。

            if (!IsInRange(x, y)) return true;
            if (cellData[x, y].Count > 0) return true;
        }
        return false;
    }

    /// <summary>
    /// 判断单个有效网格是否登记了至少一个占用对象。
    /// </summary>
    /// <param name="cell">需要检查的网格坐标。</param>
    /// <returns>有效网格已被占用时返回 <see langword="true"/>；越界或未初始化时返回 <see langword="false"/>。</returns>
    public bool IsUse(Vector2Int cell)
    {
        if (cellData == null || !IsInRange(cell.x, cell.y)) return false;
        return cellData[cell.x, cell.y].Count > 0;
    }

    /// <summary>
    /// 获取指定网格当前登记的占用对象快照。
    /// </summary>
    /// <param name="x">网格横坐标。</param>
    /// <param name="y">网格纵坐标。</param>
    /// <returns>占用对象列表；网格无效或未初始化时返回空列表。</returns>
    public List<GameObject> GetOccupiers(int x, int y)
    {
        if (cellData == null || !IsInRange(x, y)) return new List<GameObject>();
        return new List<GameObject>(cellData[x, y]);
    }

    /// <summary>
    /// 获取指定网格当前登记的占用对象数量。
    /// </summary>
    /// <param name="x">网格横坐标。</param>
    /// <param name="y">网格纵坐标。</param>
    /// <returns>占用对象数量；网格无效或未初始化时返回 0。</returns>
    public int GetOccupierCount(int x, int y)
    {
        if (cellData == null || !IsInRange(x, y)) return 0;
        return cellData[x, y].Count;
    }

    /// <summary>
    /// 汇总给定有效网格中的全部占用对象；同一对象占用多个格子时会在结果中重复出现。
    /// </summary>
    /// <param name="cells">需要查询的网格坐标集合。</param>
    /// <returns>按网格遍历顺序收集的占用对象列表。</returns>
    public List<GameObject> GetOccupiers(List<Vector2Int> cells)
    {
        if (cellData == null) return new List<GameObject>();

        List<GameObject> objs = new List<GameObject>();    // 汇总后的占用对象列表。
        foreach (var pos in cells)
        {
            if (IsInRange(pos.x, pos.y))
            {
                foreach (var obj in cellData[pos.x, pos.y])
                {
                    objs.Add(obj);
                }
            }
        }

        return objs;
    }

    /// <summary>
    /// 判断坐标是否位于当前地图宽高定义的有效网格范围内。
    /// </summary>
    /// <param name="x">待检查的横坐标。</param>
    /// <param name="y">待检查的纵坐标。</param>
    /// <returns>坐标同时位于横向和纵向边界内时返回 <see langword="true"/>。</returns>
    public void CollectOccupiersInBounds(
        Vector2Int min,
        Vector2Int max,
        HashSet<GameObject> results)
    {
        if (cellData == null || results == null)
            return;

        int minX = Mathf.Max(0, min.x);
        int minY = Mathf.Max(0, min.y);
        int maxX = Mathf.Min(width - 1, max.x);
        int maxY = Mathf.Min(height - 1, max.y);

        if (minX > maxX || minY > maxY)
            return;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                foreach (GameObject occupier in cellData[x, y])
                {
                    if (occupier != null)
                        results.Add(occupier);
                }
            }
        }
    }

    public bool IsInRange(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }
    #endregion

#if UNITY_EDITOR
    #region 编辑器回调
    /// <summary>
    /// Inspector 中地图尺寸变化时立即重建网格，并维护编辑器状态下的唯一实例引用。
    /// </summary>
    void OnValidate()
    {
        InitializeGrid();
        if (instance == null) instance = this;
        else if (instance != this)
        {
            DestroyImmediate(gameObject);
            return;
        }
    }
    #endregion
#endif
}
