using System.Collections;
using UnityEngine;

/// <summary>
/// Cat addict（猫瘾）：
/// - 召唤出场时由小变大（缩放动画），离场时变小消失（Despawn 调用或死亡外的主动移除）；
/// - 不可攻击建筑：自动把 FindEnemy 索敌切换为排除建筑（无需手动勾选）。
/// 攻击行为走标准 AI（atkObj 配置 "Cat grass smoke" 弹幕，弹幕命中效果由
/// CatGrassSmokeProjectile 处理：敌方浓缩 / 友方治疗+狂暴）。
/// 缩放动画在 LateUpdate 持续应用（带朝向翻转），避免被 Move 的 localScale 覆盖。
/// </summary>
public class CatAddict : MonoBehaviour
{
    [Header("出场/离场动画")]
    [SerializeField, Min(0.01f)]
    private float spawnDuration = 0.3f;     // 出场由小变大持续秒。
    [SerializeField, Min(0.01f)]
    private float despawnDuration = 0.3f;   // 离场变小消失持续秒。

    private GameObjectProperty _prop;
    private Vector3 baseScale;              // 基础体型（含朝向符号，由 Move 维护的翻转约定）。
    private float scaleFactor = 1f;         // 当前缩放系数（0 = 完全消失，1 = 正常体型）。
    private Coroutine scaleRoutine;
    private bool despawning;                // 是否正在离场缩小。

    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();

        // 不可攻击建筑：把索敌切换为排除建筑。
        FindEnemy findEnemy = GetComponent<FindEnemy>();
        if (findEnemy != null)
            findEnemy.SetExcludeBuildings(true);
    }

    private void OnEnable()
    {
        baseScale = transform.localScale;
        // 召唤出场：由小变大。
        scaleFactor = 0f;
        despawning = false;
        StartScaleAnimation(0f, 1f, spawnDuration, null);
    }

    private void OnDisable()
    {
        StopScaleAnimation();
        scaleFactor = 1f;
        despawning = false;
        transform.localScale = baseScale;
    }

    /// <summary>
    /// 离场：播放变小动画，结束后将本对象回收进对象池（死亡走常规死亡流程，不调用本方法）。
    /// </summary>
    public void Despawn()
    {
        if (despawning || _prop == null || _prop.isDead)
            return;

        despawning = true;
        StartScaleAnimation(scaleFactor, 0f, despawnDuration, () =>
        {
            GameObjectPool pool = GameObjectPool.Instance;
            if (pool != null)
                pool.Release(gameObject);
        });
    }

    /// <summary>
    /// 持续应用缩放（含水平朝向翻转），在 LateUpdate 执行以覆盖 Move 的 localScale 写入。
    /// </summary>
    private void LateUpdate()
    {
        if (_prop == null)
            return;

        Vector3 flip = new Vector3(_prop.isFacingLeft ? -1f : 1f, 1f, 1f);
        transform.localScale = Vector3.Scale(baseScale, flip) * scaleFactor;
    }

    private void StartScaleAnimation(float from, float to, float duration, System.Action onDone)
    {
        StopScaleAnimation();
        scaleRoutine = StartCoroutine(ScaleRoutine(from, to, duration, onDone));
    }

    private IEnumerator ScaleRoutine(float from, float to, float duration, System.Action onDone)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // 出场用缓出（先快后慢），离场用缓入（先慢后快）。
            t = to > from ? 1f - (1f - t) * (1f - t) : t * t;
            scaleFactor = Mathf.Lerp(from, to, t);
            yield return null;
        }

        scaleFactor = to;
        scaleRoutine = null;
        if (onDone != null)
            onDone();
    }

    private void StopScaleAnimation()
    {
        if (scaleRoutine != null)
        {
            StopCoroutine(scaleRoutine);
            scaleRoutine = null;
        }
    }
}
