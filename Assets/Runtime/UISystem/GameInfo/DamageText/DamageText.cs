using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class DamageText : MonoBehaviour
{
    private TextMeshProUGUI _tmpText;                            // 当前跳字使用的文本组件。
    private DamageTextPool _pool;                                // 动画结束后接收当前对象的对象池。

    // 动效状态变量
    private Vector3 _startPos;                                   // 本次跳字动画的起始世界坐标。
    private float _elapsed;                                      // 本次动画已经播放的时间。
    private float _duration = 0.8f;                              // 单次跳字动画的总时长。
    private float _randomX;                                      // 本次动画随机选择的水平飘移量。
    private bool _isPlaying;                                     // 当前是否正在更新跳字动画。

    #region 生命周期与回调
    /// <summary>
    /// 缓存同一对象上的 TextMeshPro 文本组件。
    /// </summary>
    private void Awake()
    {
        _tmpText = GetComponent<TextMeshProUGUI>();
    }
    #endregion

    #region 公开接口
    /// <summary>
    /// 设置显示数值和颜色，记录回收对象池，并重置跳字动画的起点、时间和随机水平偏移。
    /// </summary>
    /// <param name="value">需要显示的伤害或治疗数值。</param>
    /// <param name="color">本次跳字使用的文本颜色。</param>
    /// <param name="pool">动画结束后负责回收当前对象的跳字池。</param>
    public void Init(int value, Color color, DamageTextPool pool)
    {
        Init(value.ToString(), color, pool);
    }

    /// <summary>
    /// 设置显示文本和颜色，记录回收对象池，并重置跳字动画的起点、时间和随机水平偏移。
    /// </summary>
    /// <param name="text">需要显示的文本（如未命中 “miss”）。</param>
    /// <param name="color">本次跳字使用的文本颜色。</param>
    /// <param name="pool">动画结束后负责回收当前对象的跳字池。</param>
    public void Init(string text, Color color, DamageTextPool pool)
    {
        _pool = pool;
        _tmpText.text = text;


        _tmpText.color = color; // 亮紫色


        // 初始化动画状态
        _startPos = transform.position;
        _elapsed = 0f;
        _randomX = Random.Range(-0.5f, 0.5f);
        _isPlaying = true;
    }
    #endregion
    

    #region 生命周期与回调
    /// <summary>
    /// 更新跳字的上飘、随机横移、弹出缩放和后半段淡出效果；动画结束后将对象归还池中。
    /// </summary>
    private void Update()
    {
        if (!_isPlaying) return;

        _elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(_elapsed / _duration);    // 本次动画的归一化进度。

        // 1. 向上漂移 & 水平随机散开
        transform.position = _startPos + new Vector3(_randomX * progress, progress * 1.5f, 0);

        // 2. 极简的“弹出-回弹”缩放动画
        float scale;                                             // 当前帧应用的统一缩放值。
        if (progress < 0.2f)
        {
            scale = Mathf.Lerp(0f, 1.3f, progress / 0.2f);
        }
        else
        {
            scale = Mathf.Lerp(1.3f, 1.0f, (progress - 0.2f) / 0.8f);
        }
        transform.localScale = new Vector3(scale, scale, 1f);

        // 3. 后半段自动淡出
        if (progress > 0.5f)
        {
            float fadeProgress = (progress - 0.5f) / 0.5f;       // 后半段淡出的归一化进度。
            Color tempColor = _tmpText.color;                    // 保留 RGB、仅修改透明度的临时颜色。
            tempColor.a = Mathf.Lerp(1f, 0f, fadeProgress);
            _tmpText.color = tempColor;
        }

        // 4. 动画结束后，自动放回对象池
        if (progress >= 1f)
        {
            _isPlaying = false;
            if (_pool != null)
            {
                _pool.ReturnToPool(gameObject);
            }
        }
    }
    #endregion
}
