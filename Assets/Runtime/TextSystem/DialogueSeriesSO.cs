using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 按播放顺序保存的一组连续对话配置。
/// </summary>
[CreateAssetMenu(fileName = "NewDialogueSeries", menuName = "TextSystem/Dialogue Series")]
public sealed class DialogueSeriesSO : ScriptableObject
{
    [Tooltip("连续对话的播放顺序。允许直接引用 DialogueConfigSO。")]
    [SerializeField]
    private List<DialogueConfigSO> dialogues = new List<DialogueConfigSO>();

    public IReadOnlyList<DialogueConfigSO> Dialogues => dialogues;
    public int Count => dialogues != null ? dialogues.Count : 0;

    /// <summary>
    /// 获取指定位置的对话；索引无效时返回空。
    /// </summary>
    public DialogueConfigSO GetDialogue(int index)
    {
        return dialogues != null && index >= 0 && index < dialogues.Count
            ? dialogues[index]
            : null;
    }

    /// <summary>
    /// 从指定位置开始查找下一条非空配置。
    /// </summary>
    public bool TryGetNextValid(int startIndex, out int index, out DialogueConfigSO dialogue)
    {
        index = -1;
        dialogue = null;

        if (dialogues == null)
            return false;

        for (int i = Mathf.Max(0, startIndex); i < dialogues.Count; i++)
        {
            if (dialogues[i] == null)
                continue;

            index = i;
            dialogue = dialogues[i];
            return true;
        }

        return false;
    }
}
