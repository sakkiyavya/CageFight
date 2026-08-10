using UnityEngine;

[DefaultExecutionOrder(-500)]
[RequireComponent(typeof(GameObjectProperty))]
public class SummonedUnitFacingFix : MonoBehaviour
{
    private GameObjectProperty prop;

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
    }

    private void Update()
    {
        // 单位不应像弹幕一样通过旋转控制方向。
        transform.rotation = Quaternion.identity;

        if (prop.target != null)
        {
            prop.isFacingLeft =
                prop.target.transform.position.x
                < transform.position.x;
        }
    }

    private void LateUpdate()
    {
        // 修正召唤瞬间被ShootProjectile设置的旋转。
        transform.rotation = Quaternion.identity;

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) *
                  (prop.isFacingLeft ? -1f : 1f);

        transform.localScale = scale;
    }
}