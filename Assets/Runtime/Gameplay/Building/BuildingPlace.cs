using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingPlace : MonoBehaviour
{
    public static BuildingPlace Instance { get; private set; }

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
            MapCells.Instance.UseCells(currentBuilding.GetOccupyCells());
            
            // 完成放置
            currentBuilding = null;
            isInPlaceMode = false;
            fingerHandler.Unbind();
            return true;
        }

        // 位置不合法，销毁并释放
        Destroy(currentBuilding);
        currentBuilding = null;
        isInPlaceMode = false;
        fingerHandler.Unbind();
        return false;
    }

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

                    if (fingerHandler.TryBind(t.fingerId)) break;
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

                Vector2Int gridPos = new Vector2Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.y));
                Vector2 snappedPos = new Vector2(
                    gridPos.x + currentBuilding.occupySpace.x / 2f,
                    gridPos.y + currentBuilding.occupySpace.y / 2f
                );

                currentBuilding.transform.position = snappedPos;
            }

            // 抬起手指时释放绑定
            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                fingerHandler.Unbind();
            }
        }
    }
}
