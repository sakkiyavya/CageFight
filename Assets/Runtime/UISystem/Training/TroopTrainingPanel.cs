using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 兵营训练选择面板：三排槽位分别对应一/二/三阶兵种，按兵营等级逐步解锁。
/// 面板与槽位均由预制体预先配置（不动态创建 UI）；运行时只填充当前选中建筑提供的兵种，
/// 未解锁、未拥有或维护费不足的兵种头像暗淡显示且不可点击，点击后交由建筑开始训练并自动关闭面板。
/// 关闭按钮由本组件自身处理（面板级 IPointerClickHandler），不依赖 Button 事件绑定。
/// </summary>
[DisallowMultipleComponent]
public sealed class TroopTrainingPanel : UISystemBase, IPointerDownHandler, IPointerClickHandler
{
    [SerializeField] private TroopTrainingSlot[] slots = new TroopTrainingSlot[0];
    [SerializeField] private Image closeButtonImage;
    [SerializeField, Tooltip("取消出兵按钮（位于关闭键下方）；点击后取消当前兵营正在训练的兵种并关闭面板")]
    private Image cancelButtonImage;

    private const string PanelPrefabKey = "TroopTrainingPanel";

    private static TroopTrainingPanel instance;
    private static BuildingTraining activeBuilding;
    private static BuildingTraining lastClosedBuilding;
    private static bool warnedOpenFailure;
    private static float nextOpenAt = float.NegativeInfinity;

    public static TroopTrainingPanel Instance => instance;
    /// <summary>当前面板服务的训练建筑；面板关闭时为 null。</summary>
    public static BuildingTraining ActiveBuilding => activeBuilding;
    /// <summary>面板刚关闭的短时间窗口内禁止该建筑重开（防止关闭点击被误判为建筑点击）。</summary>
    private static bool RecentlyClosed => Time.unscaledTime < nextOpenAt;

    protected override void Awake()
    {
        base.Awake();
        if (instance != null && instance != this)
        {
            // 重复实例：不销毁场景对象，仅停用本组件并保留首个实例（规范禁止业务脚本 Destroy）。
            Debug.LogWarning("[TroopTrainingPanel] 场景中存在重复实例，本组件已停用。", this);
            enabled = false;
            return;
        }
        instance = this;
        foreach (TroopTrainingSlot slot in slots)
            if (slot) slot.SetPanel(this);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void OnEnable()
    {
        // 订阅经济变化：收入结算或维护费登记变化时实时刷新槽位可用状态
        // （维护费不足的兵种实时变灰、不可点击；阶段收入提升后自动恢复可点）。
        if (Coins.Instance)
        {
            Coins.Instance.OnGainCoins += RefreshEconomyTick;
            Coins.Instance.OnUpkeepChanged += RefreshEconomyChange;
        }
    }

    private void OnDisable()
    {
        // 对称退订，避免面板关闭后仍持有经济事件回调（池化对象不得残留监听）。
        if (Coins.Instance)
        {
            Coins.Instance.OnGainCoins -= RefreshEconomyTick;
            Coins.Instance.OnUpkeepChanged -= RefreshEconomyChange;
        }
    }

    /// <summary>金币每秒结算后刷新槽位可用状态。</summary>
    private void RefreshEconomyTick(int _) => RefreshIfOpen();

    /// <summary>维护费登记变化（训练/取消/换兵种/建筑被毁）后刷新槽位可用状态。</summary>
    private void RefreshEconomyChange() => RefreshIfOpen();

    /// <summary>面板打开中且有选中建筑时重新填充槽位状态。</summary>
    private void RefreshIfOpen()
    {
        if (activeBuilding && gameObject.activeInHierarchy)
            Refresh();
    }

    /// <summary>
    /// 打开面板并选中指定训练建筑；旧选中建筑自动取消发光。
    /// 面板为根级 Canvas（挂到屏幕中心），纯显隐式开关，不依赖位移动画与 UIStack 弹栈。
    /// 面板预制体未预载或对象池未就绪时返回 false（一次性警告）。
    /// </summary>
    public static bool Open(BuildingTraining building)
    {
        if (!building) return false;
        if (instance && RecentlyClosed && lastClosedBuilding == building) return true;
        if (!instance && !TryCreatePanel()) return false;

        instance.gameObject.SetActive(true);
        instance.transform.SetParent(null);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localScale = Vector3.one;

        if (activeBuilding && activeBuilding != building)
            activeBuilding.SetSelected(false);
        activeBuilding = building;
        building.SetSelected(true);

        instance.Refresh();
        if (UIStack.Instance && UIStack.Instance.Peek() != instance)
            UIStack.Instance.Push(instance);
        return true;
    }

    /// <summary>建筑被回收或销毁时调用：取消其选中状态并隐藏面板。</summary>
    public static void DeselectBuilding(BuildingTraining building)
    {
        if (!building || activeBuilding != building) return;
        activeBuilding = null;
        if (instance)
        {
            instance.Hide();
            if (UIStack.Instance && UIStack.Instance.Peek() == instance)
                UIStack.Instance.Pop();
        }
    }

    /// <summary>槽位点击：交由选中建筑开始持续产出，成功后自动关闭面板。</summary>
    public void SelectTroop(TroopDefinition troop)
    {
        if (!activeBuilding || !troop || !activeBuilding.TryStartTraining(troop)) return;
        Close();
    }

    /// <summary>
    /// 关闭按钮按下兜底：按下的对象是关闭按钮图标或其任意子对象时立即关闭面板
    /// （与建筑点击同机制；槽位按下不会误触发，槽位选择仍走点击）。
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        RaycastResult raycast = eventData.pointerPressRaycast;
        if (raycast.gameObject == null) return;

        if (closeButtonImage &&
            (raycast.gameObject == closeButtonImage.gameObject ||
             raycast.gameObject.transform.IsChildOf(closeButtonImage.transform)))
        {
            Close();
            return;
        }

        // 取消出兵按钮兜底：取消当前训练并关闭面板。
        if (cancelButtonImage &&
            (raycast.gameObject == cancelButtonImage.gameObject ||
             raycast.gameObject.transform.IsChildOf(cancelButtonImage.transform)))
        {
            CancelCurrentTraining();
        }
    }

    /// <summary>取消当前兵营训练并关闭面板。</summary>
    private void CancelCurrentTraining()
    {
        if (activeBuilding)
            activeBuilding.CancelTraining();
        Close();
    }

    /// <summary>
    /// 关闭按钮点击兜底：按下的对象是关闭按钮图标或其任意子对象时关闭面板
    /// （与按钮组件、按钮专用处理器构成三层保障；槽位点击不会误触发）。
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        RaycastResult raycast = eventData.pointerPressRaycast;
        if (raycast.gameObject == null) return;

        if (closeButtonImage &&
            (raycast.gameObject == closeButtonImage.gameObject ||
             raycast.gameObject.transform.IsChildOf(closeButtonImage.transform)))
        {
            Close();
            return;
        }

        // 取消出兵按钮兜底：取消当前训练并关闭面板。
        if (cancelButtonImage &&
            (raycast.gameObject == cancelButtonImage.gameObject ||
             raycast.gameObject.transform.IsChildOf(cancelButtonImage.transform)))
        {
            CancelCurrentTraining();
        }
    }

    /// <summary>退出键入口：隐藏面板并取消建筑选中，不再依赖 UIStack 或关闭动画。</summary>
    public void Close()
    {
        nextOpenAt = Time.unscaledTime + .5f;
        if (UIStack.Instance && UIStack.Instance.Peek() == this)
            UIStack.Instance.Pop();
        else
            Hide();
    }

    /// <summary>立即取消建筑选中并隐藏面板本体（纯 SetActive，不依赖任何动画）。</summary>
    private void Hide()
    {
        lastClosedBuilding = activeBuilding;
        if (activeBuilding)
        {
            activeBuilding.SetSelected(false);
            activeBuilding = null;
        }
        HideAllSlots();
        gameObject.SetActive(false);
    }

    /// <summary>按三排阶数填充全部槽位；无选中建筑时隐藏所有槽位。</summary>
    public void Refresh()
    {
        if (!activeBuilding)
        {
            HideAllSlots();
            return;
        }

        for (int row = 1; row <= 3; row++)
        {
            int filled = 0;
            foreach (TroopTrainingSlot slot in slots)
            {
                if (!slot || slot.Row != row) continue;

                TroopDefinition troop = activeBuilding.GetTroop(row, filled);
                if (troop != null)
                {
                    filled++;
                    slot.SetPresentation(troop,
                        activeBuilding.CanTrain(troop),
                        activeBuilding.IsTrainingTroop(troop));
                }
                else
                {
                    slot.SetPresentation(null, false, false);
                }
            }
        }
    }

    /// <summary>UIStack 弹栈时调用：立即隐藏面板并取消建筑选中（不依赖关闭动画）。</summary>
    public override void UIMotionEffect(bool toEnd)
    {
        base.UIMotionEffect(toEnd);
        if (!toEnd) Hide();
    }

    private void HideAllSlots()
    {
        foreach (TroopTrainingSlot slot in slots)
            if (slot) slot.SetPresentation(null, false, false);
    }

    /// <summary>
    /// 经对象池生成面板实例。面板自带 Canvas 与 CanvasScaler，独立于场景主画布渲染
    /// （m_OverrideSorting 置顶），不挂到普通 Transform 下，避免锚点与点击位置错位。
    /// </summary>
    private static bool TryCreatePanel()
    {
        if (!ResourceManager.Instance || !GameObjectPool.Instance || !UIStack.Instance)
        {
            WarnOpenFailure("[TroopTrainingPanel] ResourceManager/GameObjectPool/UIStack 未就绪，无法打开训练面板。");
            return false;
        }

        GameObject prefab = ResourceManager.Instance.GetGameObject(PanelPrefabKey);
        if (!prefab)
        {
            WarnOpenFailure("[TroopTrainingPanel] 训练面板预制体未预载。");
            return false;
        }

        GameObject panelObject = GameObjectPool.Instance.Get(prefab);
        if (!panelObject) return false;

        instance = panelObject.GetComponent<TroopTrainingPanel>();
        if (!instance)
        {
            GameObjectPool.Instance.Release(panelObject);
            Debug.LogError("[TroopTrainingPanel] 面板预制体根节点缺少 TroopTrainingPanel 组件。");
            return false;
        }

        foreach (TroopTrainingSlot slot in instance.slots)
            if (slot) slot.SetPanel(instance);
        return true;
    }

    private static void WarnOpenFailure(string message)
    {
        if (warnedOpenFailure) return;
        warnedOpenFailure = true;
        Debug.LogWarning(message);
    }
}
