using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class BuildingBase : MonoBehaviour
{
    public Vector2Int occupySpace = Vector2Int.one;
    protected int maxHP = 100;
    protected int hp = 100;
    protected bool isCompleted = false;
    protected SpriteRenderer spr;

    private void Awake()
    {
        spr = GetComponent<SpriteRenderer>();
    }

    public bool ChechValid()
    {
        if (MapCells.Instance == null) return false;
        
        // 获取当前位置占用的所有网格坐标
        List<Vector2Int> cellsToOccupy = GetOccupyCells();
        List<GameObject> cellsObj = MapCells.Instance.GetOccupiers(cellsToOccupy);
        bool isValid = true;
        foreach(var obj in cellsObj)
        {
            if(obj != gameObject)
            {
                isValid = false;
                break;
            }
        }

        // 如果获取到了 SpriteRenderer，则根据合法性改变颜色
        if (spr != null)
        {
            spr.color = isValid ? Color.white : Color.red;
            // print("改变物体颜色");
        }

        return isValid;
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

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // 仅在非运行模式下执行编辑器的吸附逻辑
        if (Application.isPlaying) return;

        // 编辑器模式下的网格自动吸附逻辑
        Vector2 snappedPos = new Vector2(
            Mathf.FloorToInt(transform.position.x - occupySpace.x / 2f) + occupySpace.x / 2f,
            Mathf.FloorToInt(transform.position.y - occupySpace.y / 2f) + occupySpace.y / 2f
        );

        transform.position = new Vector3(snappedPos.x, snappedPos.y, transform.position.z);

        // 自动检测位置是否合法并触发视觉反馈
        ChechValid();
    }
#endif
}
