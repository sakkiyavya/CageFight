using System.Collections;
using System.Collections.Generic;   
using UnityEngine;

/// <summary>
/// 主菜单状态
/// </summary>
public class MenuState : SceneStateBase
{
    [SerializeField]
    List<UISystemBase> menuUIElements = new List<UISystemBase>(); // 主菜单UI元素列表
    UISystemBase maxTransitionUI; // 过渡时间最长的UI元素，作为状态切换的同步标志

    void Awake()
    {
        menuUIElements.AddRange(GetComponentsInChildren<UISystemBase>(true)); // 自动收集子对象中的UI元素
        float maxT = 0;
        int maxIndex = 0;
        for(int i = 0; i < menuUIElements.Count; i++)
        {
            if(maxT < menuUIElements[i].transitionTime)
            {
                maxT = menuUIElements[i].transitionTime;
                maxIndex = i;
            }
        }
        maxTransitionUI = menuUIElements[maxIndex];
        menuUIElements.Remove(maxTransitionUI);
    }

    public override IEnumerator Enter()
    {
        foreach(var ui in menuUIElements)
        {
            ui.UIMotionEffect(true); // 同步触发其他UI元素的渐入动画
        }
        yield return maxTransitionUI.UIMotionEffectRoutine(true);
    }

    public override IEnumerator Exit()
    {
        foreach(var ui in menuUIElements)
        {
            ui.UIMotionEffect(false); // 同步触发其他UI元素的渐出动画
        }
        yield return maxTransitionUI.UIMotionEffectRoutine(false);
    }
}
