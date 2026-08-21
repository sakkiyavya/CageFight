using UnityEngine;

[DefaultExecutionOrder(-500)]
[RequireComponent(typeof(GameObjectProperty))]
public class SummonedUnitFacingFix : BehaviourBase
{
    private GameObjectProperty prop;
    private bool lastFacingLeft;

    /// <summary>经 CharacterAI 调度初始化：依赖已在 Awake 缓存，此处仅兜底补齐。</summary>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (this.prop == null)
            this.prop = prop;
    }

    /// <summary>朝向由 LateUpdate 表现层驱动，无每帧 AI 行为；返回 false 放行后续 AI。</summary>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        return false;
    }

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
    }

    private void OnEnable()
    {
        lastFacingLeft = !prop.isFacingLeft;
        RefreshFacing();
    }

    private void LateUpdate()
    {
        if (prop.target != null)
        {
            prop.isFacingLeft =
                prop.target.transform.position.x < transform.position.x;
        }

        RefreshFacing();
    }

    private void RefreshFacing()
    {
        if (transform.rotation != Quaternion.identity)
            transform.rotation = Quaternion.identity;

        if (lastFacingLeft == prop.isFacingLeft)
            return;

        lastFacingLeft = prop.isFacingLeft;

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) *
                  (prop.isFacingLeft ? -1f : 1f);
        transform.localScale = scale;
    }
}