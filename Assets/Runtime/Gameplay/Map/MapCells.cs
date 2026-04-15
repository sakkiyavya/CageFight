using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地图网格数据管理类，记录网格的占用情况
/// </summary>
public class MapCells : MonoBehaviour
{
    public static MapCells Instance { get; private set; }

    [Header("地图尺寸")]
    public int width = 20;
    public int height = 20;

    // 使用二维数组记录占用情况：true 为占用，false 为空闲
    private bool[,] cellData;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        InitializeGrid();
    }

    /// <summary>
    /// 初始化网格
    /// </summary>
    public void InitializeGrid()
    {
        cellData = new bool[width, height];
    }

    /// <summary>
    /// 占用指定的网格集合
    /// </summary>
    public void UseCells(List<Vector2Int> cells)
    {
        foreach (var pos in cells)
        {
            int x = Mathf.FloorToInt(pos.x);
            int y = Mathf.FloorToInt(pos.y);

            if (IsInRange(x, y))
            {
                cellData[x, y] = true;
            }
        }
    }

    /// <summary>
    /// 移除指定的网格占用
    /// </summary>
    public void UnuseCells(List<Vector2Int> cells)
    {
        foreach (var pos in cells)
        {
            int x = Mathf.FloorToInt(pos.x);
            int y = Mathf.FloorToInt(pos.y);

            if (IsInRange(x, y))
            {
                cellData[x, y] = false;
            }
        }
    }

    /// <summary>
    /// 检测给定的网格集合中是否有任何一个已被占用
    /// </summary>
    /// <returns>如果有任何一个网格被占用或超出边界，返回 true</returns>
    public bool IsUse(List<Vector2Int> cells)
    {
        foreach (var pos in cells)
        {
            int x = Mathf.FloorToInt(pos.x);
            int y = Mathf.FloorToInt(pos.y);

            // 如果超出地图边界，通常视为“不可用”（已占用）
            if (!IsInRange(x, y)) return true;

            // 如果该网格已被占用
            if (cellData[x, y]) return true;
        }
        return false;
    }

    /// <summary>
    /// 检查坐标是否在合法范围内
    /// </summary>
    private bool IsInRange(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }
}
