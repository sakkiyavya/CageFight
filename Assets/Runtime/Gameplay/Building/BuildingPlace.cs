using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingPlace : MonoBehaviour
{
    public static BuildingPlace Instance { get; private set; }
    // 初始化放置管理器。
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private BuildingBase currentBuilding;
    private bool isInPlaceMode = false;

    // 手指处理器
    private FingerIDHander fingerHandler = new FingerIDHander();

    /// <summary>
    /// 进入放置模式
    /// </summary>
    /// <param name="building">要放置的建筑</param>
    /// <param name="initialFingerId">初始绑定的手指 ID（可选）</param>
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
    /// 退出放置模式并尝试放置
    /// </summary>
    /// <returns>放置成功返回 true，否则返回 false</returns>
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

    private Vector2Int lastBasePos = new Vector2Int(-999, -999); // 记录上次检测时的网格起始坐标

    // 持续更新建筑预览位置。
    private void Update()
    {
        if (!isInPlaceMode || currentBuilding == null) return;

        // 1. 如果还没绑定手指，寻找第一个有效手指（不在 UI 上）
        if (!fingerHandler.IsOccupied)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
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
        Touch? activeTouch = fingerHandler.GetActiveTouch();
        if (activeTouch.HasValue)
        {
            Touch touch = activeTouch.Value;

            if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Stationary)
            {
                Vector3 worldPos = Camera.main.ScreenToWorldPoint(touch.position);
                worldPos.z = 0;

                // 计算对齐后的网格左下角起始坐标
                Vector2Int currentBasePos = new Vector2Int(
                    Mathf.FloorToInt(worldPos.x - currentBuilding.occupySpace.x / 2f),
                    Mathf.FloorToInt(worldPos.y - currentBuilding.occupySpace.y / 2f)
                );

                // 计算建筑中心点位置（用于视觉同步）
                Vector2 snappedPos = new Vector2(
                    currentBasePos.x + currentBuilding.occupySpace.x / 2f,
                    currentBasePos.y + currentBuilding.occupySpace.y / 2f
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
}
