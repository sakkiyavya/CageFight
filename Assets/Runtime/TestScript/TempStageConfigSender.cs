using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class TempStageConfigSender : MonoBehaviour
{
    [FormerlySerializedAs("levelConfig")]
    public StageConfig stageConfig;

    void Start()
    {
        if(ResourceManager.Instance && stageConfig)
            ResourceManager.Instance.LoadStageResources(stageConfig);
    }

}
