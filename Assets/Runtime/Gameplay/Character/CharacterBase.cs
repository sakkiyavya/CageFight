using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(GameObjectProperty))]
public class CharacterBase : MonoBehaviour
{
    private GameObjectProperty _prop;
    private Vector2Int lastOccupyBasePos = new Vector2Int(int.MinValue, int.MinValue);
    private Vector2Int lastOccupySpace = new Vector2Int(int.MinValue, int.MinValue);
    private bool hasRegisteredOccupancy = false;
    private int lastMapVersion = -1;
    private List<Vector2Int> currentCells = new List<Vector2Int>();

    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
    }

    private void Update()
    {
        RefreshOccupancy();
    }

    private void OnDisable()
    {
        ClearOccupancy();
    }

    private void OnDestroy()
    {
        ClearOccupancy();
    }

    /// <summary>
    /// 刷新角色在地图网格中的占用状态。
    /// </summary>
    public void RefreshOccupancy()
    {
        if (_prop == null) _prop = GetComponent<GameObjectProperty>();
        if (_prop == null) return;

        MapCells mapCells = MapCells.Instance;
        if (mapCells == null) return;

        Vector2Int currentBasePos = GetBasePos();

        // 检查是否需要更新占用
        bool needsSync = !hasRegisteredOccupancy || 
                         currentBasePos != lastOccupyBasePos || 
                         _prop.occupySpace != lastOccupySpace || 
                         mapCells.Version != lastMapVersion;

        if (!needsSync) return;

        // 清除旧的占用
        ClearOccupancy();

        // 注册新的占用
        lastOccupyBasePos = currentBasePos;
        lastOccupySpace = _prop.occupySpace;
        lastMapVersion = mapCells.Version;

        for (int x = 0; x < _prop.occupySpace.x; x++)
        {
            for (int y = 0; y < _prop.occupySpace.y; y++)
            {
                currentCells.Add(new Vector2Int(currentBasePos.x + x, currentBasePos.y + y));
            }
        }
        
        mapCells.UseCells(currentCells, gameObject);
        hasRegisteredOccupancy = true;
    }

    private Vector2Int GetBasePos()
    {
        if (_prop == null) _prop = GetComponent<GameObjectProperty>();
        return new Vector2Int(
            (int)(transform.position.x - _prop.occupySpace.x / 2f + 0.5f),
            (int)(transform.position.y - _prop.occupySpace.y / 2f + 0.5f)
        );
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

        currentCells.Clear();
        hasRegisteredOccupancy = false;
        lastMapVersion = -1;
    }

#if UNITY_EDITOR
    // 绘制并校正编辑器预览
    private void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        if (_prop == null) _prop = GetComponent<GameObjectProperty>();
        if (_prop == null) return;

        RefreshOccupancy();

        // 1. 绘制当前占用的格子 (青色)
        Gizmos.color = Color.cyan;
        foreach (var cell in currentCells)
        {
            Gizmos.DrawWireCube(new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0), Vector3.one);
        }

        // 2. 绘制攻击范围 (红色)
        Gizmos.color = Color.red;
        Vector2Int baseX = lastOccupyBasePos;
        Vector2Int rangeSize = _prop.atkRange;
        bool isLeft = _prop.isFacingLeft;

        int startX = isLeft ? lastOccupyBasePos.x - rangeSize.x : lastOccupyBasePos.x + _prop.occupySpace.x;
        // 垂直对齐：居中，y为偶数时偏上 (使用 CeilToInt 实现偏上对齐)
        int startY = lastOccupyBasePos.y + Mathf.CeilToInt((_prop.occupySpace.y - rangeSize.y) / 2.0f);

        for (int x = 0; x < rangeSize.x; x++)
        {
            for (int y = 0; y < rangeSize.y; y++)
            {
                Gizmos.DrawWireCube(new Vector3(startX + x + 0.5f, startY + y + 0.5f, 0), Vector3.one * 0.8f);
            }
        }
    }
#endif
}
