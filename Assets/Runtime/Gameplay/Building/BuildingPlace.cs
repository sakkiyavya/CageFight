using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingPlace : MonoBehaviour
{
    public static BuildingPlace Instance { get; private set; }
    #region 生命周期与回调
    /// <summary>
    /// 建立建筑放置管理器单例；场景中存在重复实例时销毁后创建的对象。
    /// </summary>
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    #endregion

    private BuildingBase currentBuilding;                                                        // 当前正在拖动预览的建筑。
    private bool isInPlaceMode = false;                                                          // 是否正在处理建筑放置输入。

    // 手指处理器
    private FingerIDHander fingerHandler = new FingerIDHander();                                 // 放置流程独占触摸输入的手指绑定器。

    #region 公开接口
    /// <summary>
    /// 判断指定建筑是否正处于拖拽放置预览阶段。
    /// 预览中的建筑只应展示放置合法性，不应执行战斗或生产行为。
    /// </summary>
    /// <param name="building">需要判断的建筑实例。</param>
    /// <returns>该建筑正在被拖拽预览时返回 <see langword="true"/>。</returns>
    public bool IsBuildingInPreview(BuildingBase building)
    {
        return isInPlaceMode && currentBuilding != null && currentBuilding == building;
    }

    /// <summary>
    /// 设置待放置建筑，重置旧触摸绑定，并按需立即占用创建建筑的指针。
    /// </summary>
    /// <param name="building">需要进入拖动预览的建筑实例。</param>
    /// <param name="initialFingerId">创建建筑时已经按下的指针编号；-1 表示等待新的有效触摸。</param>
    public void EnterPlaceMode(BuildingBase building, int initialFingerId = -1)
    {
        currentBuilding = building;
        isInPlaceMode = true;
        
        fingerHandler.Unbind();
        if (initialFingerId != -1)
        {
            fingerHandler.TryBind(initialFingerId);
        }

        Debug.Log("进入放置模式");
    }

    /// <summary>
    /// 检查建筑当前位置是否合法；合法时启动施工，非法时销毁预览对象，
    /// 最后退出放置模式并释放触摸绑定。
    /// </summary>
    /// <returns>建筑通过合法性检查并成功开始施工时返回 <see langword="true"/>。</returns>
    public bool ExitPlaceMode()
    {
        if (!isInPlaceMode || currentBuilding == null) return false;

        // 检测当前位置是否合法
        if (currentBuilding.ChechValid())
        {
            // 正式占用地图网格
            // 完成放置
            currentBuilding.StartBuild();
            currentBuilding = null;
            isInPlaceMode = false;
            fingerHandler.Unbind();
            return true;
        }

        // Debug.LogError("位置不合法");
        // 位置不合法，销毁并释放
        Destroy(currentBuilding.gameObject);
        currentBuilding = null;
        isInPlaceMode = false;
        fingerHandler.Unbind();
        return false;
    }
    #endregion

    private Vector2Int lastBasePos = new Vector2Int(-999, -999);                                 // 记录上次检测时的网格起始坐标

    #region 生命周期与回调
    /// <summary>
    /// 放置模式下绑定第一个未被 UI 占用的触摸，并持续将建筑中心吸附到对应地图网格。
    /// 仅在基准网格变化时重新检查合法性，触摸结束后释放绑定。
    /// </summary>
    private void Update()
    {
        if (!isInPlaceMode || currentBuilding == null) return;

        // 1. 如果还没绑定手指，寻找第一个有效手指（不在 UI 上）
        if (!fingerHandler.IsOccupied)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);                                                     // 当前检查的触摸。
                if (t.phase == TouchPhase.Began)
                {
                    // 过滤 UI 点击
                    if (UnityEngine.EventSystems.EventSystem.current != null && 
                        UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(t.fingerId))
                        continue;

                    if (fingerHandler.TryBind(t.fingerId)) 
                    {
                        lastBasePos = new Vector2Int(-999, -999); // 绑定新手指时重置记录
                        break;
                    }
                }
            }
        }

        // 2. 如果已经绑定，则追踪该手指
        Touch? activeTouch = fingerHandler.GetActiveTouch();                                     // 当前放置流程绑定的活动触摸。
        if (activeTouch.HasValue)
        {
            Touch touch = activeTouch.Value;                                                     // 当前帧的触摸数据。
            if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Stationary)
            {
                Vector3 worldPos = Camera.main.ScreenToWorldPoint(touch.position);               // 触摸位置转换得到的世界坐标。
                worldPos.z = 0;

                GameObjectProperty prop = currentBuilding.GetComponent<GameObjectProperty>();    // 当前建筑的占地属性。
                Vector2Int occupySpace = prop != null ? prop.occupySpace : Vector2Int.one;       // 建筑占用的网格尺寸。

                // 计算对齐后的网格左下角起始坐标
                Vector2Int currentBasePos = new Vector2Int(
                    Mathf.FloorToInt(worldPos.x - occupySpace.x / 2f),
                    Mathf.FloorToInt(worldPos.y - occupySpace.y / 2f)
                );

                // 计算建筑中心点位置（用于视觉同步）
                Vector2 snappedPos = new Vector2(
                    currentBasePos.x + occupySpace.x / 2f,
                    currentBasePos.y + occupySpace.y / 2f
                );
                currentBuilding.transform.position = snappedPos;
                currentBuilding.RefreshOccupancy();

                // 性能优化：只有在网格坐标发生变化时，才重新检测合法性
                if (currentBasePos != lastBasePos)
                {
                    currentBuilding.ChechValid();
                    lastBasePos = currentBasePos;
                }
            }

            // 抬起手指时释放绑定
            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                fingerHandler.Unbind();
            }
        }
    }
    #endregion
}
