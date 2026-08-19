using UnityEngine;

/// <summary>
/// Iron Wall Guard（铁壁卫兵）：每 applyInterval 秒（默认 1.5）对自身施加一层
/// “坚毅”（ResoluteBuff）。坚毅实例由预制体提供（把 RemoteResource/Buff/ResoluteBuff
/// 预制体拖入 resoluteBuff 字段），每层持续时长、护盾贴图等全部配置在坚毅预制体上，
/// 本脚本不参与贴图/时长的运行时配置，避免配置链路漂移。
/// 通过 GameObjectProperty.ApplyStatus 统一入口施加，仅新增本脚本即可生效。
/// </summary>
[RequireComponent(typeof(GameObjectProperty))]
public class IronWallGuard : MonoBehaviour
{
    [Header("坚毅施放")]
    [SerializeField, Min(0.1f)]
    private float applyInterval = 1.5f;      // 施放间隔秒（每 1.5 秒获得一层）。

    [Header("坚毅 Buff")]
    [SerializeField, Tooltip("坚毅 Buff 预制体实例（RemoteResource/Buff/ResoluteBuff）")]
    private ResoluteBuff resoluteBuff;

    private GameObjectProperty _prop;
    private float timer;

    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
    }

    private void OnEnable()
    {
        timer = 0f;
    }

    private void Update()
    {
        if (_prop == null || _prop.isDead || resoluteBuff == null)
            return;

        timer += Time.deltaTime;
        if (timer < applyInterval)
            return;

        timer = 0f;
        _prop.ApplyStatus(resoluteBuff);
    }
}
