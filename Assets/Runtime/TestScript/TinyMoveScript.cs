using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
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
        transform.position = new Vector3(3 * (float)math.cos(Time.time * 3.1415), 3 * (float)math.sin(Time.time * 3.1415), 0);
    }
}
