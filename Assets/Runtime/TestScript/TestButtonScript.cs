using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TestButtonScript : MonoBehaviour
{
    public GameObject obj;                  // 点击按钮时需要实例化的测试预制体。
    public Button btn;                      // 用于绑定创建回调的测试按钮。

    #region 生命周期与回调
    /// <summary>
    /// 获取未配置的按钮组件，并为有效按钮绑定测试对象创建回调。
    /// </summary>
    void Start()
    {
        if(btn == null)
            btn = GetComponent<Button>();
        if(btn != null)
            btn.onClick.AddListener(CreateNewObj);
    }

    /// <summary>
    /// Unity 每帧回调；当前测试脚本没有持续更新逻辑。
    /// </summary>
    void Update()
    {
        
    }
    #endregion

    #region 公开接口
    /// <summary>
    /// 实例化测试预制体，并将新对象随机放置在指定的二维矩形范围内。
    /// </summary>
    public void CreateNewObj()
    {
        GameObject o = Instantiate(obj);    // 本次创建的测试对象。
        o.transform.position = new Vector3(Random.Range(-3f,3f), Random.Range(-5f,5f), 0);
    }
    #endregion
}
