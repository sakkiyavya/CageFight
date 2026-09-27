using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局规则池资产：大师难度开启时，本局从中随机抽取 1 个负面规则 + 1 个中立规则
/// 追加到关卡已有规则上。池内容由编辑器工具 GlobalRulePoolAutoSync 自动维护——
/// 每次资源导入后扫描项目内全部 GlobalRuleDefinition 资产并按类别重建，无需手动拖入。
/// </summary>
[CreateAssetMenu(fileName = "GlobalRulePool", menuName = "StageSystem/全局规则池")]
public class GlobalRulePool : ScriptableObject
{
    [Tooltip("大师难度随机抽取用的规则资产池（按规则类别分组抽取；由编辑器自动同步）")]
    [SerializeField] private List<GlobalRuleDefinition> rules = new List<GlobalRuleDefinition>();

    /// <summary>规则池内容（运行时只读）。</summary>
    public List<GlobalRuleDefinition> Rules => rules;

#if UNITY_EDITOR
    /// <summary>编辑器自动同步入口：用扫描到的规则列表整体替换池内容。</summary>
    public void ReplaceRules(List<GlobalRuleDefinition> newRules)
    {
        rules = newRules != null ? newRules : new List<GlobalRuleDefinition>();
    }
#endif
}
