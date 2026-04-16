using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// 地图网格数据管理类，记录网格的占用情况
/// </summary>

[ExecuteAlways]
public class MapCells : MonoBehaviour
{
    static MapCells instance;
    public static MapCells Instance => instance;

    [Header("地图尺寸")]
    public int width = 20;
    public int height = 20;

    // 使用二维数组记录占用情况，每个格子存储占用的物体集合
    private HashSet<GameObject>[,] cellData;

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

    /// <summary>
    /// 初始化并填充网格数据
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

        // 如果在编辑器模式下，扫描场景中已有的建筑
        if (!Application.isPlaying)
        {
            RefreshEditorOccupancy();
        }
    }

    /// <summary>
    /// 编辑器模式下扫描所有建筑并记录占用
    /// </summary>
    public void RefreshEditorOccupancy()
    {
        if (cellData == null) InitializeGrid();
        
        // 清理所有网格
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cellData[x, y].Clear();
            }
        }

        BuildingBase[] buildings = FindObjectsOfType<BuildingBase>();
        foreach (var b in buildings)
        {
            UseCells(b.GetOccupyCells(), b.gameObject);
        }
    }

    /// <summary>
    /// 占用指定的网格集合
    /// </summary>
    public void UseCells(List<Vector2Int> cells, GameObject occupier)
    {
        if (occupier == null) return;

        foreach (var pos in cells)
        {
            int x = pos.x;
            int y = pos.y;

            if (IsInRange(x, y))
            {
                cellData[x, y].Add(occupier);
            }
        }
    }

    /// <summary>
    /// 移除指定的网格占用
    /// </summary>
    public void UnuseCells(List<Vector2Int> cells, GameObject occupier)
    {
        if (occupier == null) return;

        foreach (var pos in cells)
        {
            int x = pos.x;
            int y = pos.y;

            if (IsInRange(x, y))
            {
                cellData[x, y].Remove(occupier);
            }
        }
    }

    /// <summary>
    /// 检测给定的网格集合中是否有任何一个已被占用
    /// </summary>
    public bool IsUse(List<Vector2Int> cells)
    {
        foreach (var pos in cells)
        {
            int x = pos.x;
            int y = pos.y;

            if (!IsInRange(x, y)) return true; // 越界视为不可用
            if (cellData[x, y].Count > 0) return true;
        }
        return false;
    }
    public bool IsUse(Vector2Int cell)
    {
        return cellData[cell.x, cell.y].Count > 0;
    }

    /// <summary>
    /// 获取特定网格的所有占用对象列表
    /// </summary>
    public List<GameObject> GetOccupiers(int x, int y)
    {
        if (cellData == null || !IsInRange(x, y)) return new List<GameObject>();
        
        // 将 HashSet 转换为 List 返回
        return new List<GameObject>(cellData[x, y]);
    }
    public List<GameObject> GetOccupiers(List<Vector2Int> cells)
    {
        if (cellData == null) return new List<GameObject>();
        List<GameObject> objs = new List<GameObject>();

        foreach(var pos in cells)
        {
            if(IsInRange(pos.x, pos.y))
                foreach(var obj in cellData[pos.x, pos.y])
                {
                    objs.Add(obj);
                }
        }

        return objs;
    }


    private bool IsInRange(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }


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
}
