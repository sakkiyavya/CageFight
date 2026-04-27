using System.Collections.Generic;
using UnityEngine;
using System.Collections;

[ExecuteAlways]
public class BuildingBase : MonoBehaviour, ILevelComponent
{
    public System.Type DataType => typeof(BuildingBaseData);

    public ComponentData ExtractData()
    {
        return new BuildingBaseData
        {
            occupySpace = this.occupySpace,
            buildTime = this.buildTime
        };
    }

    public void ApplyData(ComponentData data)
    {
        if (data is BuildingBaseData bData)
        {
            this.occupySpace = bData.occupySpace;
            this.buildTime = bData.buildTime;
        }
    }

    public Vector2Int occupySpace = Vector2Int.one;
    public GameObject buildAnime;
    public float buildTime = 3f;
    protected bool isCompleted = false;
    protected SpriteRenderer spr;

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
        if(buildAnime == null) return;
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
        for (int x = 0; x < occupySpace.x; x++)
        {
            for (int y = 0; y < occupySpace.y; y++)
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
            Mathf.FloorToInt(transform.position.x - occupySpace.x / 2f),
            Mathf.FloorToInt(transform.position.y - occupySpace.y / 2f)
        );
    }

    // 刷新地图占用状态。
    public void RefreshOccupancy()
    {
        MapCells mapCells = MapCells.Instance;
        if (mapCells == null)
        {
            return;
        }

        Vector2Int currentBasePos = GetBasePos();
        bool needsSync =
            !hasRegisteredOccupancy ||
            currentBasePos != lastOccupyBasePos ||
            occupySpace != lastOccupySpace ||
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
        lastOccupySpace = occupySpace;
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

        if (buildAnime != null)
        {
            buildAnimeInstance = Instantiate(buildAnime, transform.position, transform.rotation);
        }

        if (buildTime > 0f)
        {
            float elapsed = 0f;
            while (elapsed < buildTime)
            {
                elapsed += Time.deltaTime;

                if (buildingHealth != null)
                {
                    float percent = Mathf.Clamp01(elapsed / buildTime);
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
    }

    // 编辑器下实时同步占用。
    private void Update()
    {
        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            RefreshOccupancy();
        }
        #endif
    }

#if UNITY_EDITOR

    // 绘制并校正编辑器预览。
    private void OnDrawGizmos()
    {
        if (Application.isPlaying) return;

        Vector2 snappedPos = new Vector2(
            Mathf.FloorToInt(transform.position.x - occupySpace.x / 2f) + occupySpace.x / 2f,
            Mathf.FloorToInt(transform.position.y - occupySpace.y / 2f) + occupySpace.y / 2f
        );

        transform.position = new Vector3(snappedPos.x, snappedPos.y, transform.position.z);
        ChechValid();
    }
#endif
}
