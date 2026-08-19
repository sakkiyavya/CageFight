using UnityEngine;
using UnityEngine.EventSystems;

public class BuildingButton : UISystemBase, IPointerDownHandler, IPointerUpHandler
{
    public GameObject targetBuilding;                                        // 按下按钮时创建并进入放置模式的建筑预制体。
    
    
    #region 生命周期与回调
    /// <summary>
    /// 将按下位置转换到按钮局部坐标；转换成功后实例化目标建筑，并用当前指针进入建筑放置模式。
    /// </summary>
    /// <param name="eventData">包含按下位置、指针编号和事件摄像机的数据。</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (targetBuilding == null || BuildingPlace.Instance == null) return;

        Debug.Log("BuildingButton OnPointerDown");

        RectTransform rectTransform = transform as RectTransform;            // 当前建筑按钮的矩形变换。
        Vector2 localPosition;                                               // 指针相对按钮的局部坐标。

        // 参考 JoyStick 的做法，使用 ScreenPointToLocalPointInRectangle 将屏幕点转换为 UI 局部坐标
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out localPosition))
        {
            // // 在局部坐标系下判断点击点距离中心的长度是否在半径内
            // if (localPosition.magnitude <= buttonRadius)
            // {
                // 经对象池生成建筑并进入放置模式（池为空时对象池内部自动创建实例）。
                GameObject obj = GameObjectPool.Instance != null
                    ? GameObjectPool.Instance.Get(targetBuilding)
                    : Instantiate(targetBuilding);
                BuildingBase building = obj.GetComponent<BuildingBase>();    // 建筑的放置与建造逻辑组件。

                if (building != null)
                {
                    BuildingPlace.Instance.EnterPlaceMode(building, eventData.pointerId);
                }
            // }
        }
    }

    /// <summary>
    /// 指针抬起时请求放置系统结束当前放置模式，并根据当前位置决定建造或取消。
    /// </summary>
    /// <param name="eventData">本次抬起事件的指针数据；放置系统使用此前绑定的指针状态。</param>
    public void OnPointerUp(PointerEventData eventData)
    {
        // 手指抬起，尝试在当前位置放下建筑
        if (BuildingPlace.Instance != null)
        {
            BuildingPlace.Instance.ExitPlaceMode();
        }
    }
    #endregion
}
