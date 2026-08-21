using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 训练面板的单个兵种槽位：显示兵种头像，未解锁/未拥有时整体暗淡并禁止点击，
/// 点击已解锁兵种时转发给所属面板开始训练。
/// </summary>
[DisallowMultipleComponent]
public sealed class TroopTrainingSlot : MonoBehaviour, IPointerClickHandler
{
    [SerializeField, Range(1, 3)] private int row = 1;
    [SerializeField] private Image icon;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField, Range(0f, 1f)] private float dimmedAlpha = .35f;
    [SerializeField, Range(0f, 1f)] private float trainingAlpha = .55f;

    private TroopTrainingPanel panel;
    private TroopDefinition troop;

    /// <summary>槽位所在排（1/2/3 对应一/二/三阶）。</summary>
    public int Row => row;
    public TroopDefinition Troop => troop;

    public void SetPanel(TroopTrainingPanel value) => panel = value;

    private void Awake()
    {
        if (icon) icon.preserveAspect = true;
    }

    /// <summary>刷新头像与可用状态；troop 为空时隐藏槽位。</summary>
    public void SetPresentation(TroopDefinition value, bool canTrain, bool isTraining)
    {
        troop = value;
        gameObject.SetActive(value != null);
        if (!value) return;

        Sprite sprite = ResourceManager.Instance
            ? ResourceManager.Instance.GetSprite(value.IconKey)
            : null;
        if (icon)
        {
            icon.sprite = sprite;
            icon.color = sprite ? Color.white : Color.clear;
        }

        if (canvasGroup)
        {
            canvasGroup.alpha = isTraining ? trainingAlpha : (canTrain ? 1f : dimmedAlpha);
            canvasGroup.interactable = canTrain;
            canvasGroup.blocksRaycasts = canTrain;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (panel && troop) panel.SelectTroop(troop);
    }
}
