using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 建筑拆除模式开关按钮（RemoveBuild）：点击在拆除模式间切换。
/// 拆除模式下所有可拆建筑（BuildUP.removable = true）显示红色本体与顶部金币图标，
/// 文本为返还金额（该建筑总花费的 50%），点击金币即播放拆除动画、返还金币并拆除建筑。
/// 与升级模式（BuildingUpgradeButton）互斥；进入拆除模式时自动关闭升级模式。
/// 点击走 EventSystem 指针事件（IPointerDown/IPointerClick，与训练取消按钮同机制），
/// 带 0.4 秒防抖窗口，把一次点击产生的多个事件合并为一次切换。
/// 拆除动画按资源键经对象池生成，播完自动回收（本组件不创建任何运行时对象）。
/// </summary>
[DisallowMultipleComponent]
public sealed class BuildingRemoveButton : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
{
    [ResourceKey(typeof(GameObject))]
    [SerializeField, Tooltip("拆除动画预制体的资源键")]
    private string demolishPrefabKey = "UPanime";
    [SerializeField, Min(.05f), Tooltip("拆除动画播放时长（秒），结束后自动回收动画对象")]
    private float demolishEffectTime = .7f;

    private static readonly List<BuildUP> buildings = new List<BuildUP>();
    private static BuildingRemoveButton instance;
    private static bool warnedMissingEffect;
    private static float nextToggleAt = float.NegativeInfinity;

    /// <summary>拆除模式是否开启。</summary>
    public static bool Active { get; private set; }
    /// <summary>场景中的拆除按钮实例（供建筑拆除流程调度动画协程）。</summary>
    public static BuildingRemoveButton Instance => instance;

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void OnEnable()
    {
        // 预载拆除动画（经资源框架异步预载，不在运行时拼装对象）。
        if (ResourceManager.Instance)
            ResourceManager.Instance.LoadExtraResourceAsync<GameObject>(demolishPrefabKey);
    }

    /// <summary>切换拆除模式：进入时先关闭升级模式（互斥），并刷新全部已登记建筑的拆除表现。</summary>
    public void ToggleRemove()
    {
        // 防抖：一次点击会依次触发 Down/Click，合并为一次切换。
        if (Time.unscaledTime < nextToggleAt)
            return;
        nextToggleAt = Time.unscaledTime + .4f;

        Active = !Active;
        if (Active)
            BuildingUpgradeButton.CloseAll();
        RefreshAll();
    }

    /// <summary>按下即切换（与训练取消按钮同机制：Down 与 Click 都触发，由防抖合并）。</summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        ToggleRemove();
    }

    /// <summary>抬起确认时兜底触发（同上，由防抖合并）。</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        ToggleRemove();
    }

    /// <summary>强制关闭拆除模式（进入升级模式等场景调用）。</summary>
    public static void CloseAll()
    {
        Active = false;
        RefreshAll();
    }

    /// <summary>登记一个可拆建筑（BuildUP.OnEnable 调用；removable=false 的不登记）。</summary>
    public static void Register(BuildUP building)
    {
        if (building && !buildings.Contains(building))
            buildings.Add(building);
    }

    /// <summary>注销建筑（BuildUP.OnDisable/OnDestroy 调用）。</summary>
    public static void Unregister(BuildUP building)
    {
        buildings.Remove(building);
    }

    /// <summary>
    /// 在指定世界坐标播放拆除动画：按资源键经对象池生成、重置动画状态，
    /// 播放 demolishEffectTime 秒后自动归还对象池。
    /// </summary>
    /// <param name="position">动画生成的世界坐标。</param>
    public static void PlayDemolishEffect(Vector3 position)
    {
        if (!instance || !ResourceManager.Instance || !GameObjectPool.Instance)
            return;

        GameObject prefab = ResourceManager.Instance.GetGameObject(instance.demolishPrefabKey);
        if (!prefab)
        {
            if (!warnedMissingEffect)
            {
                warnedMissingEffect = true;
                Debug.LogWarning($"[BuildingRemoveButton] 拆除动画预制体未预载：{instance.demolishPrefabKey}", instance);
            }
            return;
        }

        GameObject effect = GameObjectPool.Instance.Get(prefab);
        if (!effect) return;

        effect.transform.position = position;
        Animator animator = effect.GetComponent<Animator>();
        if (animator)
        {
            animator.Rebind();
            animator.Play(0, 0, 0f);
        }

        instance.StartCoroutine(instance.ReleaseEffectRoutine(effect, instance.demolishEffectTime));
    }

    /// <summary>刷新全部已登记建筑；失效引用就地清理。</summary>
    private static void RefreshAll()
    {
        for (int i = buildings.Count - 1; i >= 0; i--)
        {
            if (!buildings[i])
            {
                buildings.RemoveAt(i);
                continue;
            }

            buildings[i].ShowRemove(Active);
        }
    }

    /// <summary>等待动画时长后把特效对象归还对象池。</summary>
    private IEnumerator ReleaseEffectRoutine(GameObject effect, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (effect && GameObjectPool.Instance)
            GameObjectPool.Instance.Release(effect);
    }
}
