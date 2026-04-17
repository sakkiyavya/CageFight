using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// 鍦板浘缃戞牸鏁版嵁绠＄悊绫伙紝璁板綍缃戞牸鐨勫崰鐢ㄦ儏鍐?
/// </summary>
[ExecuteAlways]
public class MapCells : MonoBehaviour
{
    static MapCells instance;
    public static MapCells Instance => instance;
    public int Version => version;

    [Header("鍦板浘灏哄")]
    public int width = 20;
    public int height = 20;

    private HashSet<GameObject>[,] cellData;
    private int version;

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

    public void UseCells(List<Vector2Int> cells, GameObject occupier)
    {
        if (occupier == null || cellData == null) return;

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

    public void UnuseCells(List<Vector2Int> cells, GameObject occupier)
    {
        if (occupier == null || cellData == null) return;

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

    public bool IsUse(List<Vector2Int> cells)
    {
        if (cellData == null) return true;

        foreach (var pos in cells)
        {
            int x = pos.x;
            int y = pos.y;

            if (!IsInRange(x, y)) return true;
            if (cellData[x, y].Count > 0) return true;
        }
        return false;
    }

    public bool IsUse(Vector2Int cell)
    {
        if (cellData == null || !IsInRange(cell.x, cell.y)) return false;
        return cellData[cell.x, cell.y].Count > 0;
    }

    public List<GameObject> GetOccupiers(int x, int y)
    {
        if (cellData == null || !IsInRange(x, y)) return new List<GameObject>();
        return new List<GameObject>(cellData[x, y]);
    }

    public int GetOccupierCount(int x, int y)
    {
        if (cellData == null || !IsInRange(x, y)) return 0;
        return cellData[x, y].Count;
    }

    public List<GameObject> GetOccupiers(List<Vector2Int> cells)
    {
        if (cellData == null) return new List<GameObject>();

        List<GameObject> objs = new List<GameObject>();
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

    public bool IsInRange(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

#if UNITY_EDITOR
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
#endif
}
