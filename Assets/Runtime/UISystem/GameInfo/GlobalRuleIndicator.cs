using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 局内全局规则图标组：仅在局内（FSM = Gameplay）显示，主菜单/选关不显示。
/// 图标行 = 本关 StageConfig.globalRules 拖入的规则（按顺序）+ 选关界面点亮的 Strengthen1/2，
/// 按槽位顺序向右侧依次排列延伸。
/// 点击图标弹出该规则的效果字幕（小半透明窗），再点任意区域（全屏透明关闭层）自动关闭。
/// 每帧对比局内状态、选中掩码与关卡规则签名，变化时刷新（整数比较，成本可忽略）。
/// </summary>
public class GlobalRuleIndicator : MonoBehaviour
{
    /// <summary>当前场景唯一的规则指示器（供图标点击脚本回调）。</summary>
    public static GlobalRuleIndicator Instance { get; private set; }

    [Serializable]
    private class DisplayEntry
    {
        public Sprite icon;
        public string description;
    }

    [SerializeField, Tooltip("图标槽（顺序 = 从左到右的排列顺序）")]
    private Image[] icons = new Image[0];

    [SerializeField, Tooltip("Strengthen1（金币强化）图标：与选关按钮同素材")]
    private Sprite strengthen1Icon;

    [SerializeField, Tooltip("Strengthen2（建筑等级强化）图标：与选关按钮同素材")]
    private Sprite strengthen2Icon;

    [SerializeField, Tooltip("效果字幕弹窗（小半透明面板，含 TMP 文本）")]
    private GameObject popupRoot;

    [SerializeField, Tooltip("效果字幕文本（TMP）")]
    private TMP_Text popupText;

    [SerializeField, Tooltip("全屏透明点击关闭层：弹出字幕时启用，点击任意区域关闭")]
    private GameObject clickCatcher;

    private readonly List<DisplayEntry> _entries = new List<DisplayEntry>();
    private int _shownMask = -1;
    private bool _shownInGame;
    private int _shownRulesSignature = -1;

    /// <summary>效果字幕当前是否弹出。</summary>
    public bool IsPopupOpen => popupRoot != null && popupRoot.activeSelf;

    private void Awake()
    {
        Instance = this;

        // 关闭层只负责“点击任意区域关闭”。
        GlobalRuleIconClick catcher = clickCatcher != null
            ? clickCatcher.GetComponent<GlobalRuleIconClick>()
            : null;
        catcher?.Setup(this, null);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        _shownMask = -1;
        _shownRulesSignature = -1;
    }

    private void Update()
    {
        SceneFSM fsm = SceneFSM.Instance;
        bool inGame = fsm != null && fsm.CurrentStateEnum == GameState.Gameplay;
        int mask = GlobalRuleManager.SelectedMask;
        int signature = GetStageRulesSignature(fsm);

        if (inGame == _shownInGame && mask == _shownMask && signature == _shownRulesSignature)
            return;

        _shownInGame = inGame;
        _shownMask = mask;
        _shownRulesSignature = signature;
        Refresh(inGame);
    }

    /// <summary>按局内状态刷新图标行与字幕弹窗。</summary>
    public void Refresh(bool inGame)
    {
        BuildEntries();

        if (inGame)
        {
            // 状态变化才走到这里（Update 门控），打印一行便于定位图标缺失问题。
            var names = new System.Text.StringBuilder();
            for (int i = 0; i < _entries.Count; i++)
            {
                if (i > 0)
                    names.Append("、");
                names.Append(_entries[i].description.Split('\n')[0]);
            }
            Debug.Log($"[GlobalRuleIndicator] 刷新图标行：共 {_entries.Count} 条（{names}）。", this);
        }

        for (int i = 0; i < icons.Length; i++)
        {
            Image slot = icons[i];
            if (slot == null)
            {
                Debug.LogWarning($"[GlobalRuleIndicator] 图标槽 {i} 未配置 Image，已跳过。", this);
                continue;
            }

            bool show = inGame && i < _entries.Count;
            slot.gameObject.SetActive(show);
            if (!show)
                continue;

            DisplayEntry entry = _entries[i];
            slot.sprite = entry.icon;

            GlobalRuleIconClick click = slot.GetComponent<GlobalRuleIconClick>();
            click?.Setup(this, entry.description);
        }

        // 离开局内时收起字幕（并停用关闭层）。
        if (!inGame)
            ClosePopup();
    }

    /// <summary>弹出该规则的效果字幕（小半透明窗 + 全屏点击关闭层）。</summary>
    public void ShowPopup(string description)
    {
        if (popupRoot == null)
            return;

        if (popupText != null)
            popupText.text = string.IsNullOrEmpty(description) ? string.Empty : description;

        popupRoot.SetActive(true);
        if (clickCatcher != null)
            clickCatcher.SetActive(true);
    }

    /// <summary>关闭效果字幕（同时停用全屏点击关闭层）。</summary>
    public void ClosePopup()
    {
        if (popupRoot != null)
            popupRoot.SetActive(false);

        if (clickCatcher != null)
            clickCatcher.SetActive(false);
    }

    /// <summary>组装本局图标行：先关卡规则（StageConfig 拖入顺序），再大师难度随机追加规则，最后选关点亮的 Strengthen 规则。</summary>
    private void BuildEntries()
    {
        _entries.Clear();

        SceneFSM fsm = SceneFSM.Instance;
        StageConfig config = fsm != null ? fsm.CurrentStageConfig : null;

        // 大师难度随机规则：刷新前兜底抽取（幂等，正常已在 BeginStageLoad/GameplayState 完成）。
        GlobalRuleManager.PrepareMasterRules(config);

        if (config != null && config.GlobalRules != null)
        {
            for (int i = 0; i < config.GlobalRules.Count; i++)
                AddRuleEntry(config.GlobalRules[i]);
        }

        // 大师难度随机抽取的负面/中立规则（本局开始时生成）。
        IReadOnlyList<GlobalRuleDefinition> masterRules = GlobalRuleManager.MasterExtraRules;
        for (int i = 0; i < masterRules.Count; i++)
            AddRuleEntry(masterRules[i]);

        if (GlobalRuleManager.IsEnabled(GlobalRuleId.Strengthen1))
        {
            _entries.Add(new DisplayEntry
            {
                icon = strengthen1Icon,
                description = GlobalRuleManager.Strengthen1Description
            });
        }

        if (GlobalRuleManager.IsEnabled(GlobalRuleId.Strengthen2))
        {
            _entries.Add(new DisplayEntry
            {
                icon = strengthen2Icon,
                description = GlobalRuleManager.Strengthen2Description
            });
        }
    }

    /// <summary>把一条规则定义转成图标行条目（图标未拖入时只告警不显示）。</summary>
    private void AddRuleEntry(GlobalRuleDefinition rule)
    {
        if (rule == null)
            return;

        if (rule.Icon == null)
            Debug.LogWarning($"[GlobalRuleIndicator] 全局规则 '{rule.DisplayName}' 未拖入渲染图，局内不显示图标。", this);

        _entries.Add(new DisplayEntry
        {
            icon = rule.Icon,
            description = string.IsNullOrEmpty(rule.DisplayName)
                ? rule.Description
                : rule.DisplayName + "\n" + rule.Description
        });
    }

    /// <summary>关卡规则集合签名：本关配置或规则列表变化时触发图标行刷新。</summary>
    private static int GetStageRulesSignature(SceneFSM fsm)
    {
        StageConfig config = fsm != null ? fsm.CurrentStageConfig : null;
        if (config == null || config.GlobalRules == null || config.GlobalRules.Count == 0)
            return 0;

        unchecked
        {
            int hash = 17;
            for (int i = 0; i < config.GlobalRules.Count; i++)
            {
                GlobalRuleDefinition rule = config.GlobalRules[i];
                hash = hash * 31 + (rule != null ? rule.GetInstanceID() : 0);
            }

            return hash;
        }
    }
}
