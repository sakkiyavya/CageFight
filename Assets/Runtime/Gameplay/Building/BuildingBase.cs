using System.Collections.Generic;
using UnityEngine;
using System.Collections;

[ExecuteAlways]
[RequireComponent(typeof(GameObjectProperty))]
public class BuildingBase : MonoBehaviour
{
    #region 属性引用及基础变量
    private GameObjectProperty _prop;
    protected bool isCompleted = false;
    protected SpriteRenderer spr;
    #endregion

    private List<Vector2Int> occupiedCells = new List<Vector2Int>();
    private Vector2Int lastOccupyBasePos = new Vector2Int(int.MinValue, int.MinValue);
    private Vector2Int lastOccupySpace = new Vector2Int(int.MinValue, int.MinValue);
    private int lastMapVersion = -1;
    private bool hasRegisteredOccupancy = false;
    private Coroutine buildCoroutine;
    private GameObject buildAnimeInstance;
    BuildingHealth buildingHealth;
    // 缓存建筑组件。
    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        CacheComponents();
    }

    // 启用时同步占用数据。
    private void OnEnable()
    {
        CacheComponents();
        RefreshOccupancy();
    }

    // 禁用时清理占用状态。
    private void OnDisable()
    {
        StopBuildRoutine();
        ClearOccupiedCells();
    }

    // 销毁时清理占用状态。
    private void OnDestroy()
    {
        StopBuildRoutine();
        ClearOccupiedCells();
    }

    // 检查当前位置是否可放置。
    public bool ChechValid()
    {
        MapCells mapCells = MapCells.Instance;
        if (mapCells == null) return false;

        RefreshOccupancy();

        List<Vector2Int> cellsToOccupy = GetOccupyCells();
        bool isValid = true;

        foreach (var cell in cellsToOccupy)
        {
            if (!mapCells.IsInRange(cell.x, cell.y))
            {
                isValid = false;
                break;
            }
        }

        List<GameObject> cellsObj = mapCells.GetOccupiers(cellsToOccupy);
        foreach (var obj in cellsObj)
        {
            if (obj != gameObject)
            {
                isValid = false;
                break;
            }
        }

        if (spr != null)
        {
            spr.color = isValid ? Color.white : Color.red;
        }

        return isValid;
    }

    // 开始建筑施工流程。
    public void StartBuild()
    {
        if(_prop.buildAnime == null) return;
        if (buildCoroutine != null)
        {
            StopCoroutine(buildCoroutine);
            buildCoroutine = null;
        }

        CleanupBuildAnimeInstance();
        buildCoroutine = StartCoroutine(BuildRoutine());
    }

    // 获取当前占用的格子。
    public List<Vector2Int> GetOccupyCells()
    {
        Vector2Int basePos = GetBasePos();

        List<Vector2Int> cells = new List<Vector2Int>();
        for (int x = 0; x < _prop.occupySpace.x; x++)
        {
            for (int y = 0; y < _prop.occupySpace.y; y++)
            {
                cells.Add(new Vector2Int(basePos.x + x, basePos.y + y));
            }
        }
        return cells;
    }

    // 计算占用区域基点。
    private Vector2Int GetBasePos()
    {
        return new Vector2Int(
            (int)(transform.position.x - _prop.occupySpace.x / 2f + 0.5f),
            (int)(transform.position.y - _prop.occupySpace.y / 2f + 0.5f)
        );
    }

    // 刷新地图占用状态。
    public void RefreshOccupancy()
    {
        CacheComponents();
        if (_prop == null) return;

        MapCells mapCells = MapCells.Instance;
        if (mapCells == null)
        {
            return;
        }

        Vector2Int currentBasePos = GetBasePos();
        bool needsSync =
            !hasRegisteredOccupancy ||
            currentBasePos != lastOccupyBasePos ||
            _prop.occupySpace != lastOccupySpace ||
            mapCells.Version != lastMapVersion;

        if (!needsSync)
        {
            return;
        }

        if (hasRegisteredOccupancy)
        {
            mapCells.UnuseCells(occupiedCells, gameObject);
        }

        occupiedCells = GetOccupyCells();
        mapCells.UseCells(occupiedCells, gameObject);

        lastOccupyBasePos = currentBasePos;
        lastOccupySpace = _prop.occupySpace;
        lastMapVersion = mapCells.Version;
        hasRegisteredOccupancy = true;
    }

    // 清除已登记的占用格子。
    private void ClearOccupiedCells()
    {
        if (!hasRegisteredOccupancy)
        {
            return;
        }

        if (MapCells.Instance != null)
        {
            MapCells.Instance.UnuseCells(occupiedCells, gameObject);
        }

        occupiedCells.Clear();
        hasRegisteredOccupancy = false;
        lastMapVersion = -1;
    }

    // 执行施工协程。
    private IEnumerator BuildRoutine()
    {
        CacheComponents();

        isCompleted = false;
        if (buildingHealth != null)
        {
            buildingHealth.SetPercentHp(0f);
        }

        if (spr != null)
        {
            spr.enabled = false;
        }

        if (_prop.buildAnime != null)
        {
            buildAnimeInstance = Instantiate(_prop.buildAnime, transform.position, transform.rotation);
        }

        if (_prop.buildTime > 0f)
        {
            float elapsed = 0f;
            while (elapsed < _prop.buildTime)
            {
                elapsed += Time.deltaTime;

                if (buildingHealth != null)
                {
                    float percent = Mathf.Clamp01(elapsed / _prop.buildTime);
                    buildingHealth.SetPercentHp(percent);
                }

                yield return null;
            }
        }

        isCompleted = true;
        if (buildingHealth != null)
        {
            buildingHealth.SetPercentHp(1f);
        }

        if (spr != null)
        {
            spr.enabled = true;
        }

        CleanupBuildAnimeInstance();
        buildCoroutine = null;
    }

    // 停止施工流程。
    private void StopBuildRoutine()
    {
        if (buildCoroutine != null)
        {
            StopCoroutine(buildCoroutine);
            buildCoroutine = null;
        }

        CleanupBuildAnimeInstance();
    }

    // 清理施工特效实例。
    private void CleanupBuildAnimeInstance()
    {
        if (buildAnimeInstance == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(buildAnimeInstance);
        }
        else
        {
            DestroyImmediate(buildAnimeInstance);
        }

        buildAnimeInstance = null;
    }

    // 缓存常用组件。
    private void CacheComponents()
    {
        if (spr == null)
        {
            spr = GetComponent<SpriteRenderer>();
        }
        if (buildingHealth == null)
        {
            buildingHealth = GetComponent<BuildingHealth>();
        }
        if (_prop == null)
        {
            _prop = GetComponent<GameObjectProperty>();
        }
    }

    // 持续同步占用。
    private void Update()
    {
        RefreshOccupancy();
    }

#if UNITY_EDITOR

    // 绘制并校正编辑器预览。
    private void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        CacheComponents();
        if (_prop == null) return;

        Vector2 snappedPos = new Vector2(
            (int)(transform.position.x - _prop.occupySpace.x / 2f + 0.5f) + _prop.occupySpace.x / 2f,
            (int)(transform.position.y - _prop.occupySpace.y / 2f + 0.5f) + _prop.occupySpace.y / 2f
        );

        transform.position = new Vector3(snappedPos.x, snappedPos.y, transform.position.z);
        ChechValid();
    }
#endif
}
