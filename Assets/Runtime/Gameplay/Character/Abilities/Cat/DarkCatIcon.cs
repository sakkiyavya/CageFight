using UnityEngine;

/// <summary>
/// Dark cat 攻击图标运行时：从目标位置飞向暗猫并缩小消失，结束后归还对象池。
/// </summary>
public class DarkCatIcon : MonoBehaviour
{
    Transform target;
    Vector3 startPosition, startScale;
    float duration, timer;

    public void Play(Transform destination, float time)
    {
        target = destination;
        startPosition = transform.position;
        startScale = transform.localScale;
        duration = Mathf.Max(.01f, time);
        timer = 0;
    }

    void Update()
    {
        if (!target) return;

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / duration);

        transform.position =
            Vector3.Lerp(startPosition, target.position, t);

        transform.localScale =
            Vector3.Lerp(startScale, Vector3.zero, t);

        if (t >= 1)
        {
            transform.localScale = startScale;
            GameObjectPool.Instance.Release(gameObject);
        }
    }
}
