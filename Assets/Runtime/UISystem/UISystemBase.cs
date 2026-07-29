using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class UISystemBase : MonoBehaviour
{
    [Header("起始状态 (位置/尺寸/旋转/缩放)")]
    public Vector3 startPos;                                                   // 关闭状态下的锚点位置。
    public Vector3 startSize;                                                  // 关闭状态下的矩形尺寸。
    public Vector3 startRot;                                                   // 关闭状态下的欧拉角。
    public Vector3 startScale = Vector3.one;                                   // 关闭状态下的局部缩放。

    [Header("结束状态 (位置/尺寸/旋转/缩放)")]
    public Vector3 endPos;                                                     // 打开状态下的锚点位置。
    public Vector3 endSize;                                                    // 打开状态下的矩形尺寸。
    public Vector3 endRot;                                                     // 打开状态下的欧拉角。
    public Vector3 endScale = Vector3.one;                                     // 打开状态下的局部缩放。

    [Header("动画设置")]
    public float transitionTime = 0.5f;                                        // 打开或关闭过渡的持续时间。

    public float buttonRadius = 100;                                           // 子类按钮可用于点击范围判断的半径。

    public List<UISystemBase> subUI = new List<UISystemBase>();                // 需要与当前界面同步播放动画的子界面。
    protected bool isOpen = true;                                              // 下一次切换所使用的开关状态。

    protected RectTransform rectTransform;                                     // 当前界面的矩形变换组件。
    protected Coroutine effectCoroutine;                                       // 当前正在播放的本体过渡协程。

    #region 生命周期与回调
    /// <summary>
    /// 缓存当前界面的 <see cref="RectTransform"/>，供后续过渡动画修改布局属性。
    /// </summary>
    protected virtual void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }
    #endregion

    #region 界面开关效果
    /// <summary>
    /// 启动当前界面及所有子界面的过渡动画，但不等待动画完成。
    /// </summary>
    /// <param name="toEnd"><see langword="true"/> 表示过渡到打开状态，<see langword="false"/> 表示返回关闭状态。</param>
    public virtual void UIMotionEffect(bool toEnd)
    {
        PlayDirectionMove(toEnd);
        SubUIEffect(toEnd);
    }

    /// <summary>
    /// 播放当前界面的过渡动画，再并行启动所有子界面的过渡和附加特效，并等待它们全部完成。
    /// 新动画开始前会停止当前对象上尚未完成的旧动画。
    /// </summary>
    /// <param name="toEnd"><see langword="true"/> 表示过渡到打开状态，<see langword="false"/> 表示返回关闭状态。</param>
    /// <returns>等待本体和全部子界面动画完成的协程。</returns>
    public virtual IEnumerator UIMotionEffectRoutine(bool toEnd)
    {
        // 停止旧协程，直接执行本体动画协程并等待完成
        if (effectCoroutine != null)
        {
            StopCoroutine(effectCoroutine);
            effectCoroutine = null;
        }

        yield return MoveUIEffectRoutine(toEnd);

        // 同步等待所有子UI动画完成
        if (subUI.Count > 0)
        {
            List<Coroutine> subRoutines = new List<Coroutine>();               // 正在等待的子界面过渡协程。
            foreach (var ui in subUI)
            {
                if (ui != null)
                {
                    subRoutines.Add(ui.StartCoroutine(ui.UIMotionEffectRoutine(toEnd)));
                    ui.UISparkEffect(toEnd);
                }
            }
            foreach (var routine in subRoutines)
            {
                yield return routine;
            }
        }
    }

    /// <summary>
    /// 为子类预留附加界面特效入口；基类不执行具体效果。
    /// </summary>
    /// <param name="toEnd"><see langword="true"/> 表示播放打开特效，<see langword="false"/> 表示播放关闭特效。</param>
    public virtual void UISparkEffect(bool toEnd)
    {
        // 预留其他特效接口
    }
    #endregion

    #region 方向移动配置
    /// <summary>
    /// 停止当前过渡并启动指定方向的新过渡协程。
    /// </summary>
    /// <param name="toEnd"><see langword="true"/> 表示过渡到打开状态，<see langword="false"/> 表示返回关闭状态。</param>
    protected virtual void PlayDirectionMove(bool toEnd)
    {
        if (effectCoroutine != null)
        {
            StopCoroutine(effectCoroutine);
        }
        
        effectCoroutine = StartCoroutine(MoveUIEffectRoutine(toEnd));
    }
    #endregion

    #region 位移动画与子界面联动
    /// <summary>
    /// 在指定时长内平滑插值界面的位置、尺寸、旋转和缩放，并在结束时精确写入目标值。
    /// </summary>
    /// <param name="toEnd"><see langword="true"/> 时从起始配置过渡到结束配置，反之则倒序播放。</param>
    /// <returns>逐帧更新界面布局直到过渡结束的协程。</returns>
    protected virtual IEnumerator MoveUIEffectRoutine(bool toEnd)
    {
        float elapsedTime = 0f;                                                // 当前过渡已经播放的时间。
        
        // 确定起始和结束数值
        Vector3 fromP = toEnd ? startPos : endPos;                             // 本次过渡的起始位置。
        Vector3 fromS = toEnd ? startSize : endSize;                           // 本次过渡的起始尺寸。
        Vector3 fromR = toEnd ? startRot : endRot;                             // 本次过渡的起始欧拉角。
        Vector3 fromSc = toEnd ? startScale : endScale;                        // 本次过渡的起始缩放。

        Vector3 toP = toEnd ? endPos : startPos;                               // 本次过渡的目标位置。
        Vector3 toS = toEnd ? endSize : startSize;                             // 本次过渡的目标尺寸。
        Vector3 toR = toEnd ? endRot : startRot;                               // 本次过渡的目标欧拉角。
        Vector3 toSc = toEnd ? endScale : startScale;                          // 本次过渡的目标缩放。

        // 转换旋转
        Quaternion fromRotQ = Quaternion.Euler(fromR);                         // 起始旋转的四元数表示。
        Quaternion toRotQ = Quaternion.Euler(toR);                             // 目标旋转的四元数表示。

        while (elapsedTime < transitionTime)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsedTime / transitionTime);    // 平滑后的归一化过渡进度。
            
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

    /// <summary>
    /// 立即启动所有子界面的同方向过渡和附加特效，不等待它们结束。
    /// </summary>
    /// <param name="toEnd"><see langword="true"/> 表示打开子界面，<see langword="false"/> 表示关闭子界面。</param>
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
    #endregion
}
