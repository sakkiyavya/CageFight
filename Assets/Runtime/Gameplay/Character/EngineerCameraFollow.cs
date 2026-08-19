using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>跟随当前工程师，限制在地图内，并让背景缓慢追随镜头。</summary>
[RequireComponent(typeof(Camera))]
public class EngineerCameraFollow : MonoBehaviour
{
    public EngineerController target;
    public Transform background;
    public bool autoFindBackground = true;
    public string backgroundNameKeyword = "BackGround";
    [Header("镜头手感")]
    [Min(0f)] public float cameraSmoothTime = .25f;

    [Header("只允许地图上方额外视野")]
    [Min(0f)] public float topExtraView = 5f;
    [Min(0f)] public float horizontalExtraView = 5f;
    [Min(0f)] public float backgroundEdgeMargin = 3f;
    [Min(0f)] public float backgroundTopSafetyMargin = 1f;
    [Header("背景缓动")]
    [Range(0f, 1f)] public float backgroundFollowRatio = .85f;
    [Min(0f)] public float backgroundSmoothTime = .8f;
    public float backgroundYOffset = 6f;
    public SpriteRenderer backgroundRenderer;
    public Vector2 mapOrigin = new Vector2(0f, 1f);

    Camera cam;
    Vector3 cameraVelocity, backgroundVelocity;
    Vector3 backgroundStart, cameraStart;
    Vector3 backgroundMinOffset, backgroundMaxOffset;
    bool backgroundReady, backgroundAligned;
    Sprite lastBackgroundSprite;
    float nextBackgroundSearchTime;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cameraStart = transform.position;
        if (background) SetBackground(backgroundRenderer
            ? backgroundRenderer : background.GetComponentInChildren<SpriteRenderer>());
        else FindBackground();
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!background && autoFindBackground) FindBackground();
    }

    void LateUpdate()
    {
        if (!target) target = EngineerController.Active;
        if (!target) return;

        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize, target.cameraViewSize, 1f - Mathf.Exp(-8f * Time.deltaTime));

        Vector3 desired = ClampCamera(target.transform.position);
        desired.z = transform.position.z;
        transform.position = Vector3.SmoothDamp(
            transform.position, desired, ref cameraVelocity, cameraSmoothTime);

        if (!background)
        {
            if (autoFindBackground && Time.unscaledTime >= nextBackgroundSearchTime)
            {
                nextBackgroundSearchTime = Time.unscaledTime + 1f;
                FindBackground();
            }
            if (!background) return;
        }
        if (backgroundRenderer && backgroundRenderer.sprite != lastBackgroundSprite)
            SetBackground(backgroundRenderer);
        if (!backgroundReady)
        {
            backgroundStart = background.position;
            cameraStart = transform.position;
            backgroundReady = true;
        }

        Vector3 offset = transform.position - cameraStart;
        offset.z = 0f;
        Vector3 backgroundTarget = backgroundStart + offset * backgroundFollowRatio;
        if (backgroundRenderer && MapCells.Instance)
        {
            float halfWidth = cam.orthographicSize * cam.aspect;
            float left = mapOrigin.x - horizontalExtraView;
            float right = mapOrigin.x + MapCells.Instance.width + horizontalExtraView;
            float t = Mathf.InverseLerp(left + halfWidth, right - halfWidth, transform.position.x);
            backgroundTarget.x = Mathf.Lerp(
                left - backgroundMinOffset.x - backgroundEdgeMargin,
                right - backgroundMaxOffset.x + backgroundEdgeMargin, t);
        }
        backgroundTarget.y = target.transform.position.y + backgroundYOffset;
        if (backgroundRenderer)
        {
            float lowestY = transform.position.y + cam.orthographicSize
                + backgroundTopSafetyMargin - backgroundMaxOffset.y;
            backgroundTarget.y = Mathf.Max(backgroundTarget.y, lowestY);
        }
        if (!backgroundAligned)
        {
            background.position = backgroundTarget;
            backgroundAligned = true;
        }
        else
        {
            Vector3 next = Vector3.SmoothDamp(
                background.position, backgroundTarget, ref backgroundVelocity, backgroundSmoothTime);
            if (backgroundRenderer)
                next.y = Mathf.Max(next.y, transform.position.y + cam.orthographicSize
                    + backgroundTopSafetyMargin - backgroundMaxOffset.y);
            background.position = next;
        }
    }

    void FindBackground()
    {
        if (!autoFindBackground) return;
        SpriteRenderer[] renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        SpriteRenderer best = null;
        float bestArea = 0f;
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer.name.IndexOf(backgroundNameKeyword,
                    System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            float area = renderer.bounds.size.x * renderer.bounds.size.y;
            if (area <= bestArea) continue;
            best = renderer;
            bestArea = area;
        }
        if (best) SetBackground(best);
    }

    void SetBackground(SpriteRenderer renderer)
    {
        if (!renderer) return;
        backgroundRenderer = renderer;
        background = renderer.transform;
        backgroundStart = background.position;
        cameraStart = transform.position;
        backgroundMinOffset = renderer.bounds.min - background.position;
        backgroundMaxOffset = renderer.bounds.max - background.position;
        lastBackgroundSprite = renderer.sprite;
        backgroundVelocity = Vector3.zero;
        backgroundReady = true;
        backgroundAligned = false;
    }

    Vector3 ClampCamera(Vector3 desired)
    {
        if (!MapCells.Instance) return desired;
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        float minX = mapOrigin.x - horizontalExtraView + halfWidth;
        float maxX = mapOrigin.x + MapCells.Instance.width + horizontalExtraView - halfWidth;
        float minY = mapOrigin.y + halfHeight;
        float maxY = mapOrigin.y + MapCells.Instance.height + topExtraView - halfHeight;

        desired.x = minX > maxX ? mapOrigin.x + MapCells.Instance.width * .5f
            : Mathf.Clamp(desired.x, minX, maxX);
        desired.y = minY > maxY ? mapOrigin.y + MapCells.Instance.height * .5f
            : Mathf.Clamp(desired.y, minY, maxY);
        return desired;
    }

}
