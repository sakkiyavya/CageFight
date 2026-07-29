using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class TempStageConfigSender : MonoBehaviour
{
    [FormerlySerializedAs("levelConfig")]
    public StageConfig stageConfig;    // 启动测试时发送给资源管理器的关卡配置。

    #region 生命周期与回调
    /// <summary>
    /// 场景启动后，在资源管理器和关卡配置均有效时发起关卡资源加载。
    /// </summary>
    void Start()
    {
        if(ResourceManager.Instance && stageConfig)
            ResourceManager.Instance.LoadStageResources(stageConfig);
    }
    #endregion

}
