using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class UISystemBase : MonoBehaviour
{
    [Header("起始状态 (位置/尺寸/旋转/缩放)")]
    public Vector3 startPos;
    public Vector3 startSize;
    public Vector3 startRot;
    public Vector3 startScale = Vector3.one;

    [Header("结束状态 (位置/尺寸/旋转/缩放)")]
    public Vector3 endPos;
    public Vector3 endSize;
    public Vector3 endRot;
    public Vector3 endScale = Vector3.one;

    [Header("动画设置")]
    public float transitionTime = 0.5f;

    public float buttonRadius = 100;

    public List<UISystemBase> subUI = new List<UISystemBase>();
    protected bool isOpen = true;

    protected RectTransform rectTransform;
    protected Coroutine effectCoroutine;

    protected virtual void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    // 控制动画方向 (true=向结束状态，false=向起始状态)
    public virtual void UIMotionEffect(bool toEnd)
    {
        PlayDirectionMove(toEnd);
        SubUIEffect(toEnd);
    }

    public virtual void UISparkEffect(bool toEnd)
    {
        // 预留其他特效接口
    }

    protected virtual void PlayDirectionMove(bool toEnd)
    {
        if (effectCoroutine != null)
        {
            StopCoroutine(effectCoroutine);
        }
        
        effectCoroutine = StartCoroutine(MoveUIEffectRoutine(toEnd));
    }

    protected virtual IEnumerator MoveUIEffectRoutine(bool toEnd)
    {
        float elapsedTime = 0f;
        
        // 确定起始和结束数值
        Vector3 fromP = toEnd ? startPos : endPos;
        Vector3 fromS = toEnd ? startSize : endSize;
        Vector3 fromR = toEnd ? startRot : endRot;
        Vector3 fromSc = toEnd ? startScale : endScale;

        Vector3 toP = toEnd ? endPos : startPos;
        Vector3 toS = toEnd ? endSize : startSize;
        Vector3 toR = toEnd ? endRot : startRot;
        Vector3 toSc = toEnd ? endScale : startScale;

        // 转换旋转
        Quaternion fromRotQ = Quaternion.Euler(fromR);
        Quaternion toRotQ = Quaternion.Euler(toR);

        while (elapsedTime < transitionTime)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsedTime / transitionTime);
            
            // 全量属性插值
            rectTransform.anchoredPosition = Vector2.Lerp(fromP, toP, t);
            rectTransform.sizeDelta = Vector2.Lerp(fromS, toS, t);
            rectTransform.localRotation = Quaternion.Lerp(fromRotQ, toRotQ, t);
            rectTransform.localScale = Vector3.Lerp(fromSc, toSc, t);
            
            yield return null; 
        }

        // 确保最终状态精准对齐
        rectTransform.anchoredPosition = toP;
        rectTransform.sizeDelta = toS;
        rectTransform.localRotation = toRotQ;
        rectTransform.localScale = toSc;

        effectCoroutine = null;
    }

    protected virtual void SubUIEffect(bool toEnd)
    {
        if(subUI.Count > 0)
        {
            foreach(var ui in subUI)
            {
                ui.UIMotionEffect(toEnd);
                ui.UISparkEffect(toEnd);
            }
        }
    }
}
