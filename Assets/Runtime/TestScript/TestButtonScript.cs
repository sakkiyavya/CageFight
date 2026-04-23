using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TestButtonScript : MonoBehaviour
{
    public GameObject obj;
    public Button btn;
    // Start is called before the first frame update
    void Start()
    {
        if(btn == null)
            btn = GetComponent<Button>();
        if(btn != null)
            btn.onClick.AddListener(CreateNewObj);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    // 创建新的测试对象。
    public void CreateNewObj()
    {
        GameObject o = Instantiate(obj);
        o.transform.position = new Vector3(Random.Range(-3f,3f), Random.Range(-5f,5f), 0);
    }
}
