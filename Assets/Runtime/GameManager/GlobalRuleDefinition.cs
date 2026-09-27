using UnityEngine;

/// <summary>
/// 全局规则分类。
/// </summary>
public enum GlobalRuleCategory
{
    [Tooltip("正面规则：对玩家有利的增益效果")]
    Positive = 0,

    [Tooltip("负面规则：对玩家不利的减益效果")]
    Negative = 1,

    [Tooltip("中立规则：改变玩法但不偏向任何一方的效果")]
    Neutral = 2,
}

/// <summary>
/// 全局规则定义资产：拖入 StageConfig.globalRules 后，本局整局生效。
/// 策划/使用方创建规则时只填写：类别、渲染图（可直接拖入精灵）、效果字幕；
/// 规则的实际效果由开发按 ruleId 在 GlobalRuleManager 中单独实现
/// （策划提出效果需求后，由开发补写代码并在资产上填写 ruleId）。
/// 局内左上角显示渲染图，点击弹出效果字幕，再点任意区域关闭。
/// 示例：敌方血量加强（负面）—— 本局所有敌方建筑与单位血量额外增加 30%。
/// </summary>
[CreateAssetMenu(fileName = "GlobalRule", menuName = "StageSystem/全局规则")]
public class GlobalRuleDefinition : ScriptableObject
{
    [Tooltip("规则分类：正面 / 负面 / 中立")]
    [SerializeField] private GlobalRuleCategory category = GlobalRuleCategory.Neutral;

    [Tooltip("规则显示名（局内弹出字幕的标题）")]
    [SerializeField] private string displayName = "全局规则";

    [TextArea(2, 4)]
    [Tooltip("效果字幕：局内点击渲染图时弹出的规则内容说明")]
    [SerializeField] private string description = string.Empty;

    [Tooltip("局内渲染图：拖入精灵后，本局局内显示该图（可点击弹出字幕）；留空则不显示图标")]
    [SerializeField] private Sprite icon;

    [Tooltip("效果实现键（开发填写）：开发按此 ID 在 GlobalRuleManager 中实现对应效果；创建规则时留空即可")]
    [SerializeField] private string ruleId = string.Empty;

    public GlobalRuleCategory Category => category;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public string RuleId => ruleId;
}
