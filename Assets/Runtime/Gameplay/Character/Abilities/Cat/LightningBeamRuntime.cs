using UnityEngine;

/// <summary>
/// 电柱运行时（独立文件，类名与文件名一致，供 LightningBeam 预制体挂载）：
/// Show 把电柱定位到两点中点、旋转对准目标、按距离拉伸，并在 duration 后自动归还对象池。
/// 重写要点：
/// 1. 长度基准取精灵自身固有包围盒（与当前缩放无关），不再缓存渲染器包围盒，
///    杜绝池化复用时“上一轮拉伸值被当作基准”导致的逐次缩放叠加（电柱越用越细直至消失）；
/// 2. 每次启用（池化复用）重置变换与状态，保证每轮 Show 都从单位缩放开始；
/// 3. 排序层在 Show 时防御性写入 OnMap 层 order 1，盖在单位身体之上，表现稳定。
/// </summary>
class LightningBeamRuntime : MonoBehaviour
{
    private SpriteRenderer _renderer;
    private float _spriteLength = 1f;       // 精灵固有长度（世界单位，与当前缩放无关）。
    private float _releaseTime;             // 本次展示的回收时刻。
    private bool _waitingForRelease;

    private void Awake()
    {
        _renderer = GetComponentInChildren<SpriteRenderer>();

        // 精灵固有包围盒尺寸不受变换缩放影响，是拉伸换算的可靠基准。
        if (_renderer != null && _renderer.sprite != null)
            _spriteLength = Mathf.Max(0.01f, _renderer.sprite.bounds.size.x);
    }

    /// <summary>
    /// 展示电柱：置于两点中点，旋转对准目标方向，X 轴按“距离 ÷ 精灵固有长度”拉伸，
    /// 持续 duration 秒后自动归还对象池。
    /// </summary>
    /// <param name="start">电柱起点（发射点）。</param>
    /// <param name="end">电柱终点（目标位置）。</param>
    /// <param name="duration">展示时长（秒）。</param>
    public void Show(Vector3 start, Vector3 end, float duration)
    {
        Vector3 direction = end - start;
        float distance = direction.magnitude;

        if (distance <= 0.0001f)
        {
            GameObjectPool.Instance.Release(gameObject);
            return;
        }

        transform.position = (start + end) * 0.5f;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        transform.localScale =
            new Vector3(distance / _spriteLength, 1f, 1f);

        if (_renderer != null)
        {
            _renderer.sortingLayerID = 495858691;   // OnMap 层。
            _renderer.sortingOrder = 1;             // 盖在单位身体（order 0）之上。
        }

        _releaseTime = Time.time + Mathf.Max(0.01f, duration);
        _waitingForRelease = true;
    }

    private void Update()
    {
        if (!_waitingForRelease || Time.time < _releaseTime)
            return;

        _waitingForRelease = false;
        GameObjectPool.Instance.Release(gameObject);
    }

    private void OnEnable()
    {
        // 池化复用入口：清除上一轮状态并重置变换，
        // 防止上一轮的拉伸缩放与旋转被带入下一轮展示。
        _waitingForRelease = false;
        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.identity;
    }

    private void OnDisable()
    {
        _waitingForRelease = false;
    }
}
