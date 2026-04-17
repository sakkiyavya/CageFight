using System.Collections.Generic;
using UnityEngine;
using System.Collections;

[ExecuteAlways]
public class BuildingBase : MonoBehaviour
{
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
    private void Awake()
    {
        CacheComponents();
    }

    private void OnEnable()
    {
        CacheComponents();
        RefreshOccupancy();
    }

    private void OnDisable()
    {
        StopBuildRoutine();
        ClearOccupiedCells();
    }

    private void OnDestroy()
    {
        StopBuildRoutine();
        ClearOccupiedCells();
    }

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

    private Vector2Int GetBasePos()
    {
        return new Vector2Int(
            Mathf.FloorToInt(transform.position.x - occupySpace.x / 2f),
            Mathf.FloorToInt(transform.position.y - occupySpace.y / 2f)
        );
    }

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

    private void StopBuildRoutine()
    {
        if (buildCoroutine != null)
        {
            StopCoroutine(buildCoroutine);
            buildCoroutine = null;
        }

        CleanupBuildAnimeInstance();
    }

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
