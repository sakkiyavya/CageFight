using UnityEngine;

/// <summary>
/// 通用跟随型池化视觉（护盾、覆盖层、光环等）：生成后经 Init 绑定宿主，
/// 按本地偏移跟随宿主，并按呼吸参数驱动透明度；宿主死亡/回收或显式 Finish 时
/// 自动归还对象池。排序层、贴图、缩放由生成方在生成后配置。
/// 表现逻辑统一收敛在本组件中，Buff/技能脚本不再自行创建与回收临时视觉对象。
/// 
/// 归属决议（负责人 2026-08-22）：正式接管为框架池化表现模块。
/// 使用边界：仅承载纯表现（跟随/呼吸/回收），禁止承载战斗数值或状态逻辑；
/// 生成必须经 ResourceManager 资源键 + GameObjectPool，业务不得自行实例化。
/// </summary>
public class UnitVisualFollower : MonoBehaviour
{
    private GameObject _host;          // 宿主（单位根对象）。
    private Vector3 _localOffset;      // 相对宿主位置的偏移。
    private float _breathSpeed;        // 呼吸频率（每秒周期数；0 = 不呼吸）。
    private float _breathMinAlpha;     // 呼吸透明度下限。
    private float _breathMaxAlpha;     // 呼吸透明度上限。
    private SpriteRenderer _renderer;  // 本体渲染器。
    private Sprite _defaultSprite;     // 预制体自带贴图（池化复用时恢复，防止跨 Buff 贴图污染）。

    /// <summary>是否处于活跃跟随状态。</summary>
    public bool IsActive { get; private set; }

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        if (_renderer != null)
            _defaultSprite = _renderer.sprite;
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
            color.a = _breathMaxAlpha;
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

        // 归还前恢复预制体默认贴图：同一资源键的 UnitVisualFollower 被多个 Buff/技能共用，
        // 不恢复会残留上一任使用者的贴图，导致池化复用偶发显示错误图片。
        if (_renderer != null)
            _renderer.sprite = _defaultSprite;

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
