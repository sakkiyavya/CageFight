using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TempLevelConfigSender : MonoBehaviour
{
    public LevelConfig levelConfig;

    void Start()
    {
        if(ResourceManager.Instance && levelConfig)
            ResourceManager.Instance.LoadStageResources(levelConfig);
    }

}
