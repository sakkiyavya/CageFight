using UnityEngine;

/// <summary>
/// 通用跟随型池化视觉（护盾、覆盖层、光环等）：生成后经 Init 绑定宿主，
/// 按本地偏移跟随宿主，并按呼吸参数驱动透明度；宿主死亡/回收或显式 Finish 时
/// 自动归还对象池。排序层、贴图、缩放由生成方在生成后配置。
/// 表现逻辑统一收敛在本组件中，Buff/技能脚本不再自行创建与回收临时视觉对象。
/// </summary>
public class UnitVisualFollower : MonoBehaviour
{
    private GameObject _host;          // 宿主（单位根对象）。
    private Vector3 _localOffset;      // 相对宿主位置的偏移。
    private float _breathSpeed;        // 呼吸频率（每秒周期数；0 = 不呼吸）。
    private float _breathMinAlpha;     // 呼吸透明度下限。
    private float _breathMaxAlpha;     // 呼吸透明度上限。
    private SpriteRenderer _renderer;  // 本体渲染器。

    /// <summary>是否处于活跃跟随状态。</summary>
    public bool IsActive { get; private set; }

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// 绑定宿主并开始跟随/呼吸表现。
    /// </summary>
    /// <param name="host">宿主单位根对象（死亡或回收后本视觉自动归还对象池）。</param>
    /// <param name="localOffset">相对宿主位置的本地偏移。</param>
    /// <param name="breathSpeed">呼吸频率（每秒周期数），0 表示固定透明度。</param>
    /// <param name="breathMinAlpha">呼吸透明度下限。</param>
    /// <param name="breathMaxAlpha">呼吸透明度上限。</param>
    public void Init(GameObject host, Vector3 localOffset, float breathSpeed, float breathMinAlpha, float breathMaxAlpha)
    {
        _host = host;
        _localOffset = localOffset;
        _breathSpeed = Mathf.Max(0f, breathSpeed);
        _breathMinAlpha = breathMinAlpha;
        _breathMaxAlpha = breathMaxAlpha;
        IsActive = true;

        if (host != null)
            transform.position = host.transform.position + localOffset;

        if (_renderer != null)
        {
            Color color = _renderer.color;
            color.a = _breathSpeed > 0f ? _breathMaxAlpha : _breathMaxAlpha;
            _renderer.color = color;
        }
    }

    private void Update()
    {
        if (!IsActive)
            return;

        // 宿主死亡/回收 → 自动归还。
        if (_host == null || !_host.activeInHierarchy)
        {
            Finish();
            return;
        }

        transform.position = _host.transform.position + _localOffset;

        if (_renderer != null && _breathSpeed > 0f)
        {
            float t = 0.5f + 0.5f * Mathf.Sin(Time.time * _breathSpeed * Mathf.PI * 2f);
            Color color = _renderer.color;
            color.a = Mathf.Lerp(_breathMinAlpha, _breathMaxAlpha, t);
            _renderer.color = color;
        }
    }

    /// <summary>结束表现并归还对象池。</summary>
    public void Finish()
    {
        IsActive = false;
        _host = null;
        GameObjectPool.Instance.Release(gameObject);
    }

    private void OnEnable()
    {
        IsActive = false;
        _host = null;
    }

    private void OnDisable()
    {
        IsActive = false;
        _host = null;
    }
}
