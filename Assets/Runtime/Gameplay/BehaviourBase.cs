using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BehaviourBase : MonoBehaviour
{
    #region 公开接口
    /// <summary>
    /// 执行一个 AI 行为步骤；基类不处理行为并返回失败，具体行为组件负责重写。
    /// </summary>
    /// <param name="self">执行行为的游戏对象。</param>
    /// <param name="prop">执行者的运行时属性和 AI 状态。</param>
    /// <param name="health">执行者的生命组件。</param>
    /// <returns>当前行为是否完成或成功处理。</returns>
    public virtual bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        return false;
    }

    /// <summary>
    /// 为行为缓存执行者及其依赖组件；基类不保存状态，具体行为组件负责重写。
    /// </summary>
    /// <param name="self">执行行为的游戏对象。</param>
    /// <param name="prop">执行者的运行时属性和 AI 状态。</param>
    /// <param name="health">执行者的生命组件。</param>
    public virtual void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        
    }
    #endregion
}
