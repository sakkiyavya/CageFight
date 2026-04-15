using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingBase : MonoBehaviour
{
    public Vector2Int occupySpace = Vector2Int.one;
    protected int maxHP = 100;
    protected int hp = 100;
    protected bool isCompleted = false;

    public bool ChechValid()
    {
        if (MapCells.Instance == null) return false;
        
        // 获取当前位置占用的所有网格坐标
        List<Vector2Int> cellsToOccupy = GetOccupyCells();
        
        // 如果 IsUse 返回 true (表示已被占用)，则位置不合法，返回 false
        return !MapCells.Instance.IsUse(cellsToOccupy);
    }

    /// <summary>
    /// 获取建筑在当前位置下，基于中心点对齐后所占用的网格坐标列表
    /// </summary>
    public List<Vector2Int> GetOccupyCells()
    {
        // 由于 transform.position 是建筑中心点，计算左下角的网格起始坐标
        Vector2Int basePos = new Vector2Int(
            Mathf.FloorToInt(transform.position.x - occupySpace.x / 2f),
            Mathf.FloorToInt(transform.position.y - occupySpace.y / 2f)
        );

        List<Vector2Int> cells = new List<Vector2Int>();
        for (int x = 0; x < occupySpace.x; x++)
        {
            for (int y = 0; y < occupySpace.y; y++)
            {
                cells.Add(new Vector2Int(basePos.x + x, basePos.y + y));
            }
        }
        return cells;
    }
}
