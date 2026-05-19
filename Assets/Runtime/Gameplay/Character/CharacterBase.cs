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
        UpdateAtkRange();
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

    /// <summary>
    /// 根据当前占用网格和朝向，计算并更新攻击范围的世界坐标到 prop。
    /// </summary>
    public void UpdateAtkRange()
    {
        if (_prop == null) _prop = GetComponent<GameObjectProperty>();
        if (_prop == null) return;

        // 确保占用信息最新
        Vector2Int basePos = lastOccupyBasePos;

        int startX = _prop.isFacingLeft
            ? basePos.x - _prop.atkRange.x + 1
            : basePos.x;
        int startY = basePos.y + Mathf.CeilToInt((_prop.occupySpace.y - _prop.atkRange.y) / 2.0f);

        _prop.atkRangeMin = new Vector2Int(startX, startY);
        _prop.atkRangeMax = new Vector2Int(startX + _prop.atkRange.x - 1, startY + _prop.atkRange.y - 1);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        if (_prop == null) _prop = GetComponent<GameObjectProperty>();
        if (_prop == null) return;

        RefreshOccupancy();
        UpdateAtkRange();

        // 1. 绘制当前占用的格子 (青色)
        Gizmos.color = Color.cyan;
        foreach (var cell in currentCells)
        {
            Gizmos.DrawWireCube(new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0), Vector3.one);
        }

        // 2. 绘制攻击范围 (红色)，使用 prop 中已计算好的世界坐标
        Gizmos.color = Color.red;
        for (int x = _prop.atkRangeMin.x; x <= _prop.atkRangeMax.x; x++)
        {
            for (int y = _prop.atkRangeMin.y; y <= _prop.atkRangeMax.y; y++)
            {
                Gizmos.DrawWireCube(new Vector3(x + 0.5f, y + 0.5f, 0), Vector3.one * 0.8f);
            }
        }
    }
#endif
}
