using System.Collections;
using UnityEngine;

/// <summary>
/// 呆呆（Damb ass）免死机制：
/// 致命伤害时经 CharacterHealth 死亡复活器接管——按比例恢复生命，
/// 并借用框架池化表现模块 UnitVisualFollower 在身体位置展示替换图片（免死姿态），
/// deathDelay 秒后再结算真正死亡。
/// 换图期间同步把移动速度降为 revivedMoveSpeed（默认 0.1，可缓慢移动），
/// 攻击、受击、索敌等一切照常；最终死亡经 health.Die 直接结算
/// （不经复活器，杜绝最终死亡被打断）。
///
/// 尺寸策略（重写要点）：不再在单位本体渲染器上换图 + 缩放单位 Transform。
/// 1. 替换视觉放在池化随从对象上，缩放只作用于随从，不影响单位碰撞体与后续还原；
/// 2. 尺寸基准取 Awake 时预制体序列化身体贴图的真实显示尺寸（此时注册表/动画均未介入，
///    是唯一不受图集解析、PPU、动画帧差异影响的可靠基准）；
/// 3. 随从缩放 = 基准尺寸 ÷ 实际加载到的替换贴图包围盒 × replacementSize，
///    无论注册表返回哪一张图，最终显示尺寸都严格等于“身体尺寸 × replacementSize”。
/// </summary>
[RequireComponent(typeof(GameObjectProperty))]
[RequireComponent(typeof(CharacterHealth))]
public class DaiDaiScript : BehaviourBase
{
    [Header("免死后图片")]
    [SerializeField, ResourceKey(typeof(Sprite))]
    private string replacementSpriteKey = "Derivative-Two fool";

    [Header("免死后视觉承载")]
    [SerializeField, ResourceKey(typeof(GameObject)), Tooltip("替换图片的池化视觉预制体资源键")]
    private string visualPrefabKey = "UnitVisualFollower";

    [Tooltip("(1,1)匹配原图片大小，(0.5,0.5)缩小一半")]
    [SerializeField] private Vector2 replacementSize = Vector2.one;

    [Header("设置")]
    [SerializeField, Min(0.1f)] private float deathDelay = 10f;

    [SerializeField, Range(0f, 1f)]
    private float restoredHpPercent = 0.5f;

    [Tooltip("换图期间的移动速度（同步生效，退出时恢复原速）")]
    [SerializeField, Min(0f)] private float revivedMoveSpeed = 0.1f;

    private GameObjectProperty prop;
    private CharacterHealth health;
    private CharacterAI characterAI;
    private Animator animator;
    private SpriteRenderer bodyRenderer;

    // 身体基准显示尺寸与视觉中心偏移：Awake 时从序列化身体贴图直接读取（见类注释）。
    private Vector2 canonicalBodySize = Vector2.one;
    private Vector3 bodyVisualCenterOffset;

    // 经 ResourceManager 解析的缓存（延迟补齐，资源键已进公共预载清单）。
    private GameObject _visualPrefab;
    private Sprite _replacementSprite;

    private UnitVisualFollower _replacementFollower;

    private bool triggered;
    private float _moveSpeedBefore;         // 换图降速前保存的原速度（退出/回收时恢复）。

    /// <summary>经 CharacterAI 调度初始化：依赖已在 Awake 缓存，此处仅兜底补齐。</summary>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (this.prop == null)
            this.prop = prop;
        if (this.health == null)
            this.health = health;
    }

    /// <summary>免死由死亡复活器驱动，无每帧行为；返回 false 放行后续 AI。</summary>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        return false;
    }

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        health = GetComponent<CharacterHealth>();
        characterAI = GetComponent<CharacterAI>();
        animator = GetComponent<Animator>();
        bodyRenderer = GetComponentInChildren<SpriteRenderer>(true);

        // 此刻渲染器仍是预制体序列化的身体贴图（注册表与动画尚未介入），
        // 读取其世界包围盒作为身体显示尺寸与视觉中心偏移的唯一可靠基准。
        if (bodyRenderer != null)
        {
            Bounds bounds = bodyRenderer.bounds;
            if (bounds.size.x > 0.001f && bounds.size.y > 0.001f)
            {
                canonicalBodySize = bounds.size;
                bodyVisualCenterOffset = bounds.center - transform.position;
            }
        }
    }

    private void OnEnable()
    {
        triggered = false;
        // 经生命框架统一扩展点登记免死复活器（OnDisable 对称注销）。
        if (health != null)
            health.RegisterDeathReviver(TryRevive);
    }

    private void OnDisable()
    {
        if (health != null)
            health.UnregisterDeathReviver(TryRevive);

        StopAllCoroutines();

        if (animator != null)
            animator.enabled = true;

        if (characterAI != null)
            characterAI.enabled = true;

        // 恢复换图降速前的原速度（对象池复用时保证属性还原）。
        if (triggered && prop != null)
            prop.moveSpeed = _moveSpeedBefore;

        RestoreAppearance();
    }

    /// <summary>
    /// 免死接管（经 CharacterHealth 死亡复活器登记）：致命伤害后按比例恢复生命，
    /// 停止 AI 与攻击、展示替换姿态，并进入延迟死亡状态；本次伤害已由框架完成唯一结算，
    /// 不再自行调用伤害计算。
    /// </summary>
    /// <param name="lethalDamage">导致生命归零的伤害数据。</param>
    /// <returns>接管成功时返回 <see langword="true"/>（跳过常规死亡流程）。</returns>
    private bool TryRevive(Damage lethalDamage)
    {
        if (triggered)
            return false;

        triggered = true;

        int restoredHp =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    prop.maxHp * restoredHpPercent
                )
            );

        // 经生命框架受控 API 恢复生命（框架已结算完本次致命伤害）。
        if (health != null)
            health.SetHpKeepDeadState(restoredHp);

        // 免死姿态：打断当前攻击（AI 会重新索敌/行动）。
        prop.isAttack = false;
        prop.target = null;
        prop.path?.Clear();

        // 换图期间同步降速（可缓慢移动），退出/回收时恢复原速。
        _moveSpeedBefore = prop.moveSpeed;
        prop.moveSpeed = revivedMoveSpeed;

        StartCoroutine(DelayedDeath());
        return true;
    }

    /// <summary>
    /// 挂上替换视觉：经 ResourceManager 解析池化预制体与替换贴图，从对象池取
    /// UnitVisualFollower，把替换图缩放到“身体真实显示尺寸 × replacementSize”
    /// 并对齐身体视觉中心；视觉就绪后才隐藏身体贴图，避免资源缺失时单位凭空消失。
    /// </summary>
    /// <returns>替换视觉是否已就位。</returns>
    private bool TryAttachReplacementVisual()
    {
        if (_replacementFollower != null)
            return true;

        if (ResourceManager.Instance == null)
            return false;

        // 延迟补齐：资源键已进公共预载清单，正常对局首次免死时即已就绪。
        if (_visualPrefab == null && !string.IsNullOrEmpty(visualPrefabKey))
            _visualPrefab = ResourceManager.Instance.GetGameObject(visualPrefabKey);
        if (_replacementSprite == null && !string.IsNullOrEmpty(replacementSpriteKey))
            _replacementSprite = ResourceManager.Instance.GetSprite(replacementSpriteKey);

        if (_visualPrefab == null || _replacementSprite == null)
            return false;

        GameObject go = GameObjectPool.Instance.Get(_visualPrefab);
        if (go == null)
            return false;

        UnitVisualFollower follower = go.GetComponent<UnitVisualFollower>();
        if (follower == null)
        {
            // 预制体已预配置 UnitVisualFollower；缺失时归还并安全失败。
            GameObjectPool.Instance.Release(go);
            return false;
        }

        SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            GameObjectPool.Instance.Release(go);
            return false;
        }

        renderer.sprite = _replacementSprite;
        renderer.color = Color.white;
        renderer.sortingLayerID = 495858691;    // OnMap 层，盖在身体之上。
        if (bodyRenderer != null)
            renderer.sortingOrder = bodyRenderer.sortingOrder + 1;

        // 按“实际加载到的替换贴图”自身包围盒换算缩放，
        // 保证最终显示尺寸恒等于身体基准尺寸 × replacementSize。
        Vector2 loadedBounds = _replacementSprite.bounds.size;
        if (loadedBounds.x > 0.001f && loadedBounds.y > 0.001f)
        {
            Vector3 scale = go.transform.localScale;
            scale.x = canonicalBodySize.x / loadedBounds.x * replacementSize.x;
            scale.y = canonicalBodySize.y / loadedBounds.y * replacementSize.y;
            go.transform.localScale = scale;
        }

        // 随从贴图 pivot 为 0.5/0.5（中心即对象位置），偏移对齐身体视觉中心；
        // 无呼吸、全透明上限 1，表现为静态覆盖图。
        follower.Init(gameObject, bodyVisualCenterOffset, 0f, 1f, 1f);
        _replacementFollower = follower;

        if (bodyRenderer != null)
            bodyRenderer.enabled = false;

        return true;
    }

    /// <summary>
    /// 延迟死亡：等待替换视觉就位（资源缺失时最多重试 2 秒），
    /// 持续 deathDelay 秒后恢复动画并结算真正死亡。
    /// </summary>
    private IEnumerator DelayedDeath()
    {
        float deadline = Time.time + 2f;
        while (!TryAttachReplacementVisual() && Time.time < deadline)
            yield return null;

        yield return new WaitForSeconds(deathDelay);

        if (prop.isDead)
            yield break;

        FinishReplacementVisual();

        // 归还替换视觉后恢复身体显示，
        // 并直接进入常规死亡流程（health.Die 不经复活器，杜绝最终死亡被打断）。
        if (bodyRenderer != null)
            bodyRenderer.enabled = true;

        if (animator != null)
            animator.enabled = true;

        health.Die();
    }

    /// <summary>归还替换视觉（重复调用安全，随从可能已因宿主回收自动归还）。</summary>
    private void FinishReplacementVisual()
    {
        if (_replacementFollower != null)
        {
            if (_replacementFollower.IsActive)
                _replacementFollower.Finish();
            _replacementFollower = null;
        }
    }

    /// <summary>恢复身体显示，归还替换视觉。</summary>
    private void RestoreAppearance()
    {
        FinishReplacementVisual();

        if (bodyRenderer != null)
            bodyRenderer.enabled = true;
    }
}
