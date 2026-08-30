using UnityEngine;

/// <summary>
/// 一条对话的数据配置。正文直接保存在配置中，人物立绘只保存资源逻辑 Key。
/// </summary>
[CreateAssetMenu(fileName = "NewDialogueConfig", menuName = "TextSystem/Dialogue Config")]
public sealed class DialogueConfigSO : ScriptableObject
{
    [Tooltip("人物立绘的 Sprite 资源 Key。留空时使用 Unity UI 内建白色纹理；非空资源必须已由当前关卡预加载。")]
    [SerializeField, ResourceKey(typeof(Sprite))]
    private string portraitSpriteKey;

    [Tooltip("本条对话显示的正文。")]
    [SerializeField, TextArea(3, 10)]
    private string text;

    public string PortraitSpriteKey => portraitSpriteKey;
    public string Text => text;
}
