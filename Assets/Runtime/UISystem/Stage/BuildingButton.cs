using UnityEngine;
using UnityEngine.EventSystems;

public class BuildingButton : UISystemBase, IPointerDownHandler, IPointerUpHandler
{
    public GameObject targetBuilding;
    
    
    // 开始拖出建筑。
    public void OnPointerDown(PointerEventData eventData)
    {
        if (targetBuilding == null || BuildingPlace.Instance == null) return;

        Debug.Log("BuildingButton OnPointerDown");

        RectTransform rectTransform = transform as RectTransform;
        Vector2 localPosition;

        // 参考 JoyStick 的做法，使用 ScreenPointToLocalPointInRectangle 将屏幕点转换为 UI 局部坐标
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out localPosition))
        {
            // // 在局部坐标系下判断点击点距离中心的长度是否在半径内
            // if (localPosition.magnitude <= buttonRadius)
            // {
                // 实例化建筑并进入放置模式
                GameObject obj = Instantiate(targetBuilding);
                BuildingBase building = obj.GetComponent<BuildingBase>();

                if (building != null)
                {
                    BuildingPlace.Instance.EnterPlaceMode(building, eventData.pointerId);
                }
            // }
        }
    }

    // 结束建筑放置。
    public void OnPointerUp(PointerEventData eventData)
    {
        // 手指抬起，尝试在当前位置放下建筑
        if (BuildingPlace.Instance != null)
        {
            BuildingPlace.Instance.ExitPlaceMode();
        }
    }
}
