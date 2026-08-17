using System.Collections.Generic;
using UnityEngine;
using System.Collections;

[ExecuteAlways]
[RequireComponent(typeof(GameObjectProperty))]
public class BuildingBase : MonoBehaviour
{
    private GameObjectProperty _prop;                                                             // 建筑的占地、施工资源和时长属性。
    protected bool isCompleted = false;                                                           // 当前建筑是否已完成施工。
    protected SpriteRenderer spr;                                                                 // 建筑主体的精灵渲染器。

    /// <summary>建筑是否已完成施工，供战斗类扩展（如哨塔 AI）在完工前停火。</summary>
    public bool IsCompleted => isCompleted;

    private List<Vector2Int> occupiedCells = new List<Vector2Int>();                              // 当前建筑登记占用的全部网格。
    private Vector2Int lastOccupyBasePos = new Vector2Int(int.MinValue, int.MinValue);            // 最近同步占用时的左下网格坐标。
    private Vector2Int lastOccupySpace = new Vector2Int(int.MinValue, int.MinValue);              // 最近同步占用时的网格尺寸。
    private int lastMapVersion = -1;                                                              // 最近同步占用时的地图版本。
    private bool hasRegisteredOccupancy = false;                                                  // 当前建筑是否已经登记地图占用。
    private Coroutine buildCoroutine;                                                             // 当前正在运行的施工协程。
    private GameObject buildAnimeInstance;                                                        // 施工期间显示的临时特效实例。
    BuildingHealth buildingHealth;                                                                // 施工期间逐步更新的生命组件。

    #region 生命周期与清理回调
    /// <summary>
    /// 缓存建筑属性、精灵渲染器和生命组件。
    /// </summary>
    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        CacheComponents();
    }

    /// <summary>
    /// 建筑启用时刷新依赖组件并将当前占地同步到地图。
    /// </summary>
    private void OnEnable()
    {
        CacheComponents();
        RefreshOccupancy();
    }

    /// <summary>
    /// 建筑停用时停止施工、清理施工特效并释放地图占用。
    /// </summary>
    private void OnDisable()
    {
        StopBuildRoutine();
        ClearOccupiedCells();
    }

    /// <summary>
    /// 建筑销毁时停止施工、清理施工特效并释放地图占用。
    /// </summary>
    private void OnDestroy()
    {
        StopBuildRoutine();
        ClearOccupiedCells();
    }
    #endregion

    #region 放置、施工入口与网格占用
    /// <summary>
    /// 检查建筑全部占用网格是否位于地图内且未被其他对象占用，
    /// 并用白色或红色更新建筑预览颜色。
    /// </summary>
    /// <returns>当前位置能否合法放置该建筑。</returns>
    public bool ChechValid()
    {
        MapCells mapCells = MapCells.Instance;                                                    // 当前地图网格管理器。
        if (mapCells == null) return false;

        RefreshOccupancy();

        List<Vector2Int> cellsToOccupy = GetOccupyCells();                                        // 当前建筑准备占用的网格。
        bool isValid = true;                                                                      // 本轮合法性检查结果。

        foreach (var cell in cellsToOccupy)
        {
            if (!mapCells.IsInRange(cell.x, cell.y))
            {
                isValid = false;
                break;
            }
        }

        List<GameObject> cellsObj = mapCells.GetOccupiers(cellsToOccupy);                         // 目标网格中已登记的占用对象。
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

    /// <summary>
    /// 停止旧施工流程并清理旧特效，然后启动新的施工协程。
    /// 未配置施工特效资源键时当前实现不会开始施工。
    /// </summary>
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

    /// <summary>
    /// 根据建筑左下基准坐标和占地尺寸生成全部占用网格。
    /// </summary>
    /// <returns>当前建筑覆盖的网格坐标列表。</returns>
    public List<Vector2Int> GetOccupyCells()
    {
        Vector2Int basePos = GetBasePos();                                                        // 建筑占用区域的左下坐标。

        List<Vector2Int> cells = new List<Vector2Int>();                                          // 生成的占用网格列表。
        for (int x = 0; x < _prop.occupySpace.x; x++)
        {
            for (int y = 0; y < _prop.occupySpace.y; y++)
            {
                cells.Add(new Vector2Int(basePos.x + x, basePos.y + y));
            }
        }
        return cells;
    }

    /// <summary>
    /// 根据建筑中心世界坐标和占地尺寸计算占用区域的左下网格坐标。
    /// </summary>
    /// <returns>建筑占用矩形的左下网格坐标。</returns>
    private Vector2Int GetBasePos()
    {
        return new Vector2Int(
            (int)(transform.position.x - _prop.occupySpace.x / 2f + 0.5f),
            (int)(transform.position.y - _prop.occupySpace.y / 2f + 0.5f)
        );
    }

    /// <summary>
    /// 当建筑位置、占地尺寸或地图版本变化时，移除旧占用并重新登记当前占用网格。
    /// </summary>
    public void RefreshOccupancy()
    {
        CacheComponents();
        if (_prop == null) return;

        MapCells mapCells = MapCells.Instance;                                                    // 当前地图网格管理器。
        if (mapCells == null)
        {
            return;
        }

        Vector2Int currentBasePos = GetBasePos();                                                 // 当前占用区域左下坐标。
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

    /// <summary>
    /// 从地图移除建筑当前登记的全部占用网格，并重置本地同步状态。
    /// </summary>
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
    #endregion

    #region 施工流程与特效
    /// <summary>
    /// 将建筑生命降为零并隐藏主体，创建施工特效；
    /// 在施工时长内按进度恢复生命，完成后显示主体并清理特效。
    /// </summary>
    /// <returns>逐帧更新施工进度直到建筑完成的协程。</returns>
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

        if (!string.IsNullOrEmpty(_prop.buildAnime))
        {
            GameObject animePrefab = ResourceManager.Instance.GetGameObject(_prop.buildAnime);    // 配置的施工动画预制体。
            if (animePrefab != null)
            {
                buildAnimeInstance = Instantiate(animePrefab, transform.position, transform.rotation);
            }
        }

        if (_prop.buildTime > 0f)
        {
            float elapsed = 0f;                                                                   // 已经施工的时间。
            while (elapsed < _prop.buildTime)
            {
                elapsed += Time.deltaTime;

                if (buildingHealth != null)
                {
                    float percent = Mathf.Clamp01(elapsed / _prop.buildTime);                     // 当前施工完成比例。
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

    /// <summary>
    /// 停止仍在运行的施工协程，并清理施工特效实例。
    /// </summary>
    private void StopBuildRoutine()
    {
        if (buildCoroutine != null)
        {
            StopCoroutine(buildCoroutine);
            buildCoroutine = null;
        }

        CleanupBuildAnimeInstance();
    }

    /// <summary>
    /// 根据运行环境销毁当前施工特效，并清空缓存引用。
    /// </summary>
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
    #endregion

    #region 组件缓存与自动同步
    /// <summary>
    /// 按需缓存精灵渲染器、生命组件和建筑属性组件。
    /// </summary>
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

    /// <summary>
    /// 每帧检查并同步建筑的地图占用。
    /// </summary>
    private void Update()
    {
        RefreshOccupancy();
    }
    #endregion

#if UNITY_EDITOR

    #region 编辑器预览
    /// <summary>
    /// 在编辑器中将建筑中心吸附到网格，并刷新放置合法性颜色预览。
    /// </summary>
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
    #endregion
#endif
}
