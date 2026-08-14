using UnityEngine;

[DefaultExecutionOrder(-500)]
[RequireComponent(typeof(GameObjectProperty))]
public class SummonedUnitFacingFix : MonoBehaviour
{
    private GameObjectProperty prop;
    private bool lastFacingLeft;

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