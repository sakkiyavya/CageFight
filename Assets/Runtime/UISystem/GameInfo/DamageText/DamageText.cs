using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 伤害/治疗跳字：
/// 数值优先用 UIImage 的 number_0..number_9 数字贴图渲染（预制体预摆 6 个数字位，居中填充）；
/// 数字贴图尚未全部缓存或位数超出槽位上限时回退 TMP 文本显示。
/// miss / 金币不足等自定义文本仍走 TMP。
/// 动效：上飘 + 随机横移 + 弹出回弹缩放 + 后半段淡出，结束后归还对象池。
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class DamageText : MonoBehaviour
{
    [SerializeField, Tooltip("数字位贴图（从左到右排列；数值按位数居中填充，其余位隐藏）")]
    private Image[] digitImages = new Image[0];

    private const int DigitCount = 10;

    private static Sprite[] _digitSprites;                       // number_0..number_9 全部缓存就绪后建立的共享缓存。
    private static bool _digitFallbackWarned;                    // 数字贴图未就绪的一次性诊断标记。

    private TextMeshProUGUI _tmpText;                            // 文本模式使用的 TMP 组件。
    private DamageTextPool _pool;                                // 动画结束后接收当前对象的对象池。
    private bool _usingDigits;                                   // 本次跳字是否使用数字贴图模式。

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
    /// 设置显示数值和颜色：优先数字贴图渲染，贴图未就绪时回退 TMP。
    /// </summary>
    /// <param name="value">需要显示的伤害或治疗数值。</param>
    /// <param name="color">本次跳字使用的颜色。</param>
    /// <param name="pool">动画结束后负责回收当前对象的跳字池。</param>
    public void Init(int value, Color color, DamageTextPool pool)
    {
        if (!TryInitDigits(value, color, pool))
            Init(value.ToString(), color, pool);
    }

    /// <summary>
    /// 设置显示文本和颜色（如未命中 “miss”），并重置跳字动画。
    /// </summary>
    /// <param name="text">需要显示的文本。</param>
    /// <param name="color">本次跳字使用的文本颜色。</param>
    /// <param name="pool">动画结束后负责回收当前对象的跳字池。</param>
    public void Init(string text, Color color, DamageTextPool pool)
    {
        _pool = pool;

        // 文本模式：启用 TMP、隐藏全部数字位。
        _usingDigits = false;
        SetAllDigitsActive(false);
        if (_tmpText != null)
        {
            _tmpText.enabled = true;
            _tmpText.text = text;
            _tmpText.color = color;
        }

        ResetAnimation();
    }
    #endregion

    #region 数字贴图模式
    /// <summary>
    /// 数字贴图模式：按位数居中填充数字位；贴图未全部缓存或位数超上限时返回 false（回退 TMP）。
    /// </summary>
    private bool TryInitDigits(int value, Color color, DamageTextPool pool)
    {
        if (digitImages == null || digitImages.Length == 0)
        {
            Debug.LogWarning("[DamageText] 预制体未包含数字位（digitImages 为空），数字跳字回退 TMP。请重新导入 Text (TMP).prefab。", this);
            return false;
        }

        Sprite[] sprites = ResolveDigitSprites();
        if (sprites == null)
            return false;

        int count = CountDigits(Mathf.Abs(value));
        if (count > digitImages.Length)
            return false;

        _pool = pool;
        _usingDigits = true;
        if (_tmpText != null)
            _tmpText.enabled = false;

        // 先全部隐藏，再按位数居中填充（个位在最右侧可见槽）。
        SetAllDigitsActive(false);
        int start = (digitImages.Length - count) / 2;
        int n = Mathf.Abs(value);
        for (int j = 0; j < count; j++)
        {
            int slotIndex = start + count - 1 - j;
            Image image = slotIndex >= 0 && slotIndex < digitImages.Length ? digitImages[slotIndex] : null;
            if (image == null)
                continue;

            image.gameObject.SetActive(true);
            image.sprite = sprites[n % 10];
            image.color = color;
            n /= 10;
        }

        ResetAnimation();
        return true;
    }

    /// <summary>十进制位数（0 = 1 位）。</summary>
    private static int CountDigits(int value)
    {
        if (value < 10)
            return 1;

        int count = 0;
        int n = value;
        while (n > 0)
        {
            n /= 10;
            count++;
        }
        return count;
    }

    /// <summary>解析 number_0..number_9 贴图；全部缓存就绪后共享缓存，任一缺失返回 null（并一次性告警）。</summary>
    private static Sprite[] ResolveDigitSprites()
    {
        if (_digitSprites != null)
            return _digitSprites;

        if (ResourceManager.Instance == null)
            return null;

        var sprites = new Sprite[DigitCount];
        for (int i = 0; i < DigitCount; i++)
        {
            sprites[i] = ResourceManager.Instance.GetSprite("number_" + i);
            if (sprites[i] == null)
            {
                // 主动补加载（编辑器直取路径同步写入缓存；不依赖任何预载时机），下一次 Init 即命中。
                ResourceManager.Instance.LoadExtraResourceAsync<Sprite>("number_" + i);

                if (!_digitFallbackWarned)
                {
                    _digitFallbackWarned = true;
                    Debug.LogWarning($"[DamageText] 数字贴图未就绪：number_{i} 未缓存，已补发加载，本次回退 TMP。");
                }
                return null;   // 尚未全部缓存：本次回退 TMP，下次再试。
            }
        }

        _digitSprites = sprites;
        Debug.Log("[DamageText] 数字贴图 number_0..number_9 已就绪，跳字切换为贴图模式。");
        return _digitSprites;
    }

    private void SetAllDigitsActive(bool active)
    {
        if (digitImages == null)
            return;

        for (int i = 0; i < digitImages.Length; i++)
        {
            if (digitImages[i] != null)
                digitImages[i].gameObject.SetActive(active);
        }
    }
    #endregion

    /// <summary>重置跳字动画的起点、时间和随机水平偏移。</summary>
    private void ResetAnimation()
    {
        _startPos = transform.position;
        _elapsed = 0f;
        _randomX = Random.Range(-0.5f, 0.5f);
        _isPlaying = true;
    }

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

        // 3. 后半段自动淡出（数字贴图模式逐位淡出，文本模式淡 TMP）。
        if (progress > 0.5f)
        {
            float alpha = Mathf.Lerp(1f, 0f, (progress - 0.5f) / 0.5f);
            if (_usingDigits)
            {
                if (digitImages != null)
                {
                    for (int i = 0; i < digitImages.Length; i++)
                    {
                        Image image = digitImages[i];
                        if (image == null || !image.gameObject.activeSelf)
                            continue;

                        Color tempColor = image.color;
                        tempColor.a = alpha;
                        image.color = tempColor;
                    }
                }
            }
            else if (_tmpText != null)
            {
                Color tempColor = _tmpText.color;
                tempColor.a = alpha;
                _tmpText.color = tempColor;
            }
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
