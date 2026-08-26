using UnityEngine;
using TMPro;

/// <summary>
/// 局内战斗测试工具（临时）。IMGUI 直画按钮，绕开 EventSystem/画布/射线全部 UI 管线：
/// 进 Play 后屏幕右上角出现两个按钮，鼠标悬停 Game 视图点击即可：
/// [切换种族] —— 鼠系/猫系三个建筑系统（兵营/黑暗兵营/哨塔按钮目标预制体）整体互换；
/// [切换阵营] —— 切换后续放置建筑的阵营 0/1（不影响场上已放置建筑；产兵自动跟随）。
/// 仅测试用，正式发布前删除本组件即可。
/// </summary>
public class FightTest : MonoBehaviour
{
    [Header("三个建筑按钮（兵营/黑暗兵营/哨塔）")]
    [SerializeField] private BuildingButton[] buildingButtons = new BuildingButton[3];

    [SerializeField, Tooltip("与按钮一一对应的鼠系建筑预制体（兵营/黑暗兵营/哨塔）")]
    private GameObject[] mousePrefabs = new GameObject[3];

    [SerializeField, Tooltip("与按钮一一对应的猫系建筑预制体（兵营/黑暗兵营/哨塔）")]
    private GameObject[] catPrefabs = new GameObject[3];

    [Header("按键视觉（仅镜像翻转）")]
    [SerializeField] private RectTransform r1;
    [SerializeField] private RectTransform r2;

    [Header("状态文本（仅显示）")]
    [SerializeField] private TMP_Text raceText;
    [SerializeField] private TMP_Text sideText;

    /// <summary>后续放置建筑使用的阵营；-1 = 不干预（默认）。</summary>
    public static int DesiredSide { get; private set; } = -1;

    private bool _isCat;

    private void Awake()
    {
        // R1/R2 键图像左右翻转（镜像）。
        Flip(r1);
        Flip(r2);
        ApplyRace();
        RefreshText();
    }

    /// <summary>IMGUI 按钮：屏幕右上角，鼠标在 Game 视图内点击即触发。</summary>
    private void OnGUI()
    {
        GUI.skin.box.fontSize = 28;
        GUI.skin.label.fontSize = 26;
        GUI.skin.button.fontSize = 28;

        GUILayout.BeginArea(new Rect(Screen.width - 794f, 10f, 784f, 420f), GUI.skin.box);
        GUILayout.Label("FightTest（临时测试工具）");
        if (GUILayout.Button("切换种族（当前：" + (_isCat ? "猫系" : "鼠系") + "）"))
        {
            _isCat = !_isCat;
            ApplyRace();
            RefreshText();
        }
        string side = DesiredSide == 1 ? "1" : DesiredSide == 0 ? "0" : "默认";
        if (GUILayout.Button("切换阵营（当前：" + side + "）"))
        {
            DesiredSide = DesiredSide == 1 ? 0 : 1;
            RefreshText();
        }
        GUILayout.EndArea();
    }

    /// <summary>按当前种族把三个建筑按钮的目标预制体整体切换为鼠系/猫系。</summary>
    private void ApplyRace()
    {
        for (int i = 0; i < buildingButtons.Length; i++)
        {
            if (buildingButtons[i] == null)
                continue;

            GameObject prefab = _isCat
                ? (i < catPrefabs.Length ? catPrefabs[i] : null)
                : (i < mousePrefabs.Length ? mousePrefabs[i] : null);

            if (prefab != null)
                buildingButtons[i].SetTargetBuilding(prefab);
        }
    }

    private void RefreshText()
    {
        if (raceText != null)
            raceText.text = _isCat ? "猫系建筑" : "鼠系建筑";
        if (sideText != null)
            sideText.text = DesiredSide == 1 ? "阵营:1" : DesiredSide == 0 ? "阵营:0" : "阵营:默认";
    }

    /// <summary>水平翻转键位图像：仅改 localScale.x。</summary>
    private static void Flip(RectTransform rt)
    {
        if (rt == null)
            return;

        Vector3 scale = rt.localScale;
        scale.x = -Mathf.Abs(scale.x);
        rt.localScale = scale;
    }
}
