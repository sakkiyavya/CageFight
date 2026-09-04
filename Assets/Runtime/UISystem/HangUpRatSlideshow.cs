using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂机面板“挂机鼠”幻灯片播放：按固定帧间隔循环轮播 frames 中的精灵（Hang up rat Ap 图集切片）。
/// 精灵帧由 Inspector 直接配置（序列化引用，不涉及运行时资源查询与场景遍历）；
/// 组件在 Awake 缓存目标 Image，帧切换无临时分配；随对象激活/停用自动启停。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class HangUpRatSlideshow : MonoBehaviour
{
    [SerializeField, Tooltip("轮播帧精灵序列（Hang up rat Ap 图集切片，按播放顺序排列）")]
    private Sprite[] frames = new Sprite[0];
    [SerializeField, Min(0.01f), Tooltip("每帧持续时间（秒）")]
    private float frameDuration = 0.0833333f;

    private Image _image;
    private int _frameIndex;
    private float _elapsed;

    private void Awake()
    {
        _image = GetComponent<Image>();
    }

    private void OnEnable()
    {
        _frameIndex = 0;
        _elapsed = 0f;
        ApplyFrame();
    }

    private void Update()
    {
        if (_image == null || frames == null || frames.Length == 0)
            return;

        _elapsed += Time.deltaTime;
        if (_elapsed < frameDuration)
            return;

        _elapsed -= frameDuration;
        _frameIndex = (_frameIndex + 1) % frames.Length;
        ApplyFrame();
    }

    /// <summary>将当前帧精灵写入目标 Image（空帧跳过，保持上一帧画面）。</summary>
    private void ApplyFrame()
    {
        if (_image == null || frames == null || frames.Length == 0)
            return;

        Sprite sprite = frames[_frameIndex];
        if (sprite != null)
            _image.sprite = sprite;
    }
}