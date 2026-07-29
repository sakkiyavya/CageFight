using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(GameObjectProperty))]
public class CharacterBase : MonoBehaviour
{
    private GameObjectProperty _prop;                                                                 // 提供占地、朝向和攻击范围的角色属性。
    private Vector2Int lastOccupyBasePos = new Vector2Int(int.MinValue, int.MinValue);                // 最近登记占用矩形的左下坐标。
    private Vector2Int lastOccupySpace = new Vector2Int(int.MinValue, int.MinValue);                  // 最近登记的占地尺寸。
    private bool hasRegisteredOccupancy = false;                                                      // 当前对象是否已写入地图占用数据。
    private int lastMapVersion = -1;                                                                  // 最近同步占用时的地图版本。
    private List<Vector2Int> currentCells = new List<Vector2Int>();                                   // 当前角色登记占用的全部网格。

    #region 生命周期与回调
    /// <summary>
    /// 缓存同一对象上的角色属性组件。
    /// </summary>
    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
    }

    /// <summary>
    /// 每帧同步角色地图占用，并根据占地和朝向更新世界攻击范围。
    /// </summary>
    private void Update()
    {
        RefreshOccupancy();
        UpdateAtkRange();
    }

    /// <summary>
    /// 角色停用时清除地图占用登记。
    /// </summary>
    private void OnDisable()
    {
        ClearOccupancy();
    }

    /// <summary>
    /// 角色销毁时清除地图占用登记。
    /// </summary>
    private void OnDestroy()
    {
        ClearOccupancy();
    }
    #endregion

    #region 网格占用与攻击范围
    /// <summary>
    /// 当角色位置、占地尺寸或地图版本变化时，移除旧登记并将当前占用网格重新写入地图。
    /// </summary>
    public void RefreshOccupancy()
    {
        if (_prop == null) _prop = GetComponent<GameObjectProperty>();
        if (_prop == null) return;

        MapCells mapCells = MapCells.Instance;                                                        // 当前地图网格管理器。
        if (mapCells == null) return;

        Vector2Int currentBasePos = GetBasePos();                                                     // 当前占用矩形左下坐标。

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

    /// <summary>
    /// 根据角色中心世界坐标和占地尺寸计算占用矩形的左下网格坐标。
    /// </summary>
    /// <returns>角色占用区域的左下网格坐标。</returns>
    private Vector2Int GetBasePos()
    {
        if (_prop == null) _prop = GetComponent<GameObjectProperty>();
        return new Vector2Int(
            (int)(transform.position.x - _prop.occupySpace.x / 2f + 0.5f),
            (int)(transform.position.y - _prop.occupySpace.y / 2f + 0.5f)
        );
    }

    /// <summary>
    /// 从地图移除当前角色登记的全部占用网格，并重置本地同步状态。
    /// </summary>
    public void ClearOccupancy()
    {
        if (!hasRegisteredOccupancy) return;

        MapCells mapCells = MapCells.Instance;                                                        // 当前地图网格管理器。
        if (mapCells != null)
        {
            mapCells.UnuseCells(currentCells, gameObject);
        }

        currentCells.Clear();
        hasRegisteredOccupancy = false;
        lastMapVersion = -1;
    }

    /// <summary>
    /// 根据角色占用区域、攻击范围尺寸和水平朝向，计算攻击矩形的最小与最大网格坐标。
    /// </summary>
    public void UpdateAtkRange()
    {
        if (_prop == null) _prop = GetComponent<GameObjectProperty>();
        if (_prop == null) return;

        // 确保占用信息最新
        Vector2Int basePos = lastOccupyBasePos;                                                       // 当前占用矩形左下坐标。

        int startX = _prop.isFacingLeft
            ? basePos.x - _prop.atkRange.x + 1
            : basePos.x;
        int startY = basePos.y + Mathf.CeilToInt((_prop.occupySpace.y - _prop.atkRange.y) / 2.0f);    // 纵向居中后的攻击矩形起点。

        _prop.atkRangeMin = new Vector2Int(startX, startY);
        _prop.atkRangeMax = new Vector2Int(startX + _prop.atkRange.x - 1, startY + _prop.atkRange.y - 1);
    }
    #endregion

#if UNITY_EDITOR
    #region 编辑器预览
    /// <summary>
    /// 在编辑器中同步占用和攻击范围，并分别用青色与红色线框绘制网格预览。
    /// </summary>
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
    #endregion
#endif
}
