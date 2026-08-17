using UnityEngine;

/// <summary>
/// Iron Wall Guard（铁壁卫兵）：每 applyInterval 秒自动对自身施加 layersPerApply 层“坚毅”，
/// 每层持续 layerDuration 秒（默认每秒 2 层、每层 4 秒）。
/// 坚毅（ResoluteBuff）：每层一次格挡——受伤时该次伤害变为 1 点且免疫击退，并消耗一层；
/// 层默认 4 秒后到期，因此常态维持约 2 × 4 = 8 层的格挡池，被击破后由下一轮施放补回。
/// 护盾贴图用预制体上的 Sprite 直接引用（shieldSprite，如 State1 AP_32），
/// 不经运行时资源键查找，避免加载链路异常导致显示错图；引用为空时回退到资源键。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
public class IronWallGuard : MonoBehaviour
{
    [Header("坚毅施放")]
    [SerializeField, Min(0.1f)]
    private float applyInterval = 1f;       // 施放间隔秒（每秒）。
    [SerializeField, Min(1)]
    private int layersPerApply = 2;         // 每次施放的坚毅层数（2 层）。
    [SerializeField, Min(0.1f)]
    private float layerDuration = 4f;       // 每层持续秒（4 秒）。

    [Header("护盾贴图")]
    [SerializeField, Tooltip("坚毅棱形护盾贴图（推荐直接拖 State1 AP_32 子精灵；为空时回退 shieldSpriteKey 按资源键查找）")]
    private Sprite shieldSprite;
    [SerializeField, ResourceKey(typeof(Sprite))]
    private string shieldSpriteKey = "State1 AP_32"; // 回退用的护盾贴图资源键。

    private GameObjectProperty _prop;
    private ResoluteBuff _resolute;
    private float timer;

    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _resolute = gameObject.AddComponent<ResoluteBuff>();
        _resolute.SetDuration(layerDuration);
        TryResolveShieldSprite();
    }

    private void OnEnable()
    {
        timer = 0f;
    }

    private void Update()
    {
        if (_prop == null || _prop.isDead)
            return;

        // 护盾贴图延迟补齐：Awake 时关卡精灵资源可能尚未加载完成（按 key 查找可能为空），
        // 在每次施放前重试，保证首层坚毅创建护盾时能拿到贴图。
        if (_resolute.ShieldSprite == null)
            TryResolveShieldSprite();

        timer += Time.deltaTime;
        if (timer < applyInterval)
            return;

        timer = 0f;
        for (int i = 0; i < layersPerApply; i++)
            _resolute.ApplyBuff(_prop);
    }

    /// <summary>
    /// 解析护盾贴图：优先用预制体上的 Sprite 直接引用（不经过运行时查找），
    /// 引用为空时回退到资源键查找。
    /// </summary>
    private void TryResolveShieldSprite()
    {
        if (shieldSprite != null)
        {
            _resolute.SetShieldSprite(shieldSprite);
            return;
        }

        if (!string.IsNullOrEmpty(shieldSpriteKey) && ResourceManager.Instance != null)
            _resolute.SetShieldSprite(ResourceManager.Instance.GetSprite(shieldSpriteKey));
    }
}
