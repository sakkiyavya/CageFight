using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class UISystemBase : MonoBehaviour
{
    [Header("UI Effect 设置")]
    public Vector2 startPosition;     // 初始的 AnchoredPosition
    public Vector2 endPosition;       // 结束的 AnchoredPosition
    public float transitionTime = 0.5f; // 变换所需时间（秒）

    protected List<UISystemBase> subUI = new List<UISystemBase>();
    protected bool isOpenSubUI = true;

    protected RectTransform rectTransform;
    protected Coroutine effectCoroutine;

    protected virtual void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    // 重载 UIEffect 方法，允许传入 bool 值控制方向 (true=向结束位置，false=向起始位置)
    public virtual void UIMotionEffect(bool toEnd)
    {
        PlayDirectionMove(toEnd);
    }
    public virtual void UISparkEffect(bool toEnd)
    {
        
    }
    // 内部封装的核心插值移动控制
    protected virtual void PlayDirectionMove(bool toEnd)
    {
        // 如果当前有正在执行的动画，先停掉，防止动画冲突
        if (effectCoroutine != null)
        {
            StopCoroutine(effectCoroutine);
        }
        
        // 启动协程开始平滑移动
        effectCoroutine = StartCoroutine(MoveUIEffectRoutine(toEnd));
    }

    protected virtual IEnumerator MoveUIEffectRoutine(bool toEnd)
    {
        float elapsedTime = 0f;
        
        // 根据方向设定我们本次移动的起点和终点
        Vector2 fromPos = toEnd ? startPosition : endPosition;
        Vector2 toPos   = toEnd ? endPosition   : startPosition;

        // 确保一开始的时候处于计算得出的起点位置
        rectTransform.anchoredPosition = fromPos;

        // 根据时间进行循环实现平滑插值
        while (elapsedTime < transitionTime)
        {
            elapsedTime += Time.deltaTime;
            // 计算当前经过时间的比例 (0 到 1 之间)
            float t = elapsedTime / transitionTime;
            
            // 线性插值计算当前帧所在的位置
            rectTransform.anchoredPosition = Vector2.Lerp(fromPos, toPos, t);
            
            // 等待下一帧再继续
            yield return null; 
        }

        // 动画结束时，强制贴合到精准的目标终点位置
        rectTransform.anchoredPosition = toPos;
        effectCoroutine = null;
    }

    protected virtual void SubUIEffect()
    {
        if(subUI.Count > 0)
            foreach(var ui in subUI)
            {
                ui.UIMotionEffect(isOpenSubUI);
                ui.UISparkEffect(isOpenSubUI);
            }

        UIMotionEffect(isOpenSubUI);
        UISparkEffect(isOpenSubUI);

        isOpenSubUI = !isOpenSubUI;
    }




}
