using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class CharacterBase : MonoBehaviour
{
    private Vector2Int lastCell = new Vector2Int(int.MinValue, int.MinValue);
    private bool hasRegisteredOccupancy = false;
    private int lastMapVersion = -1;
    private List<Vector2Int> currentCells = new List<Vector2Int>(1);

    private void Update()
    {
        RefreshOccupancy();
    }

    private void OnDisable()
    {
        ClearOccupancy();
    }

    /// <summary>
    /// 刷新角色在地图网格中的占用状态。
    /// 角色通常只占用当前中心点所在的单个格子。
    /// </summary>
    public void RefreshOccupancy()
    {
        MapCells mapCells = MapCells.Instance;
        if (mapCells == null) return;

        Vector2Int currentCell = new Vector2Int(
            Mathf.FloorToInt(transform.position.x),
            Mathf.FloorToInt(transform.position.y)
        );

        // 如果格子没变，且已经注册过，且地图版本没变，则不需要刷新
        if (hasRegisteredOccupancy && currentCell == lastCell && mapCells.Version == lastMapVersion) return;

        // 清除旧的占用
        ClearOccupancy();

        // 注册新的占用
        lastCell = currentCell;
        lastMapVersion = mapCells.Version;
        currentCells.Clear();
        currentCells.Add(lastCell);
        
        mapCells.UseCells(currentCells, gameObject);
        hasRegisteredOccupancy = true;
    }

    /// <summary>
    /// 清除角色在地图上的占用登记。
    /// </summary>
    public void ClearOccupancy()
    {
        if (!hasRegisteredOccupancy) return;

        MapCells mapCells = MapCells.Instance;
        if (mapCells != null)
        {
            mapCells.UnuseCells(currentCells, gameObject);
        }

        hasRegisteredOccupancy = false;
    }

#if UNITY_EDITOR
    // 绘制并校正编辑器预览（模仿 BuildingBase）
    private void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        
        RefreshOccupancy();

        // 绘制当前占用的格子
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(new Vector3(lastCell.x + 0.5f, lastCell.y + 0.5f, 0), Vector3.one);
    }
#endif
}
