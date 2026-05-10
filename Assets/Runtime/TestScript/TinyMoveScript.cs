using System.Collections;
using System.Collections.Generic;
// using Unity.Mathematics;
using UnityEngine;

public class TinyMoveScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = new Vector3(3 * (float)Mathf.Cos(Time.time * 3.1415f), 3 * (float)Mathf.Sin(Time.time * 3.1415f), 0);
    }
}
