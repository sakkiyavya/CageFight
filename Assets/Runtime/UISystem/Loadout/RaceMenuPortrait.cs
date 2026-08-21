using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主界面当前种族头像：跟随选装系统的种族选择显示对应图标。
/// 通过 PlayerLoadoutManager.Changed 事件接入（选择种族写入 UserGlobalInfo 后自动刷新），
/// 图标按资源键经 ResourceManager 取得；仅新增本脚本并挂到主界面种族头像 Image 上即可生效。
/// </summary>
[DisallowMultipleComponent]
public sealed class RaceMenuPortrait : MonoBehaviour
{
    [SerializeField] private PlayerLoadoutManager loadout;   // 选装解析入口（提供当前种族）。
    [SerializeField] private Image portrait;                 // 主界面的种族头像图片。

    private Coroutine setupRoutine;

    private void OnEnable()
    {
        if (portrait) portrait.preserveAspect = true;
        if (loadout) loadout.Changed += Refresh;
        setupRoutine = StartCoroutine(SetupRoutine());
    }

    private void OnDisable()
    {
        if (loadout) loadout.Changed -= Refresh;
        if (setupRoutine != null) StopCoroutine(setupRoutine);
        setupRoutine = null;
    }

    /// <summary>等待选装就绪并预载展示图标后首次刷新。</summary>
    private IEnumerator SetupRoutine()
    {
        while (loadout && !loadout.IsReady) yield return null;
        if (loadout) yield return loadout.PreloadPresentationResources();
        setupRoutine = null;
        Refresh();
    }

    /// <summary>按当前所选种族刷新头像；未选择或资源未就绪时隐藏头像。</summary>
    private void Refresh()
    {
        if (!portrait || !loadout) return;

        Sprite sprite = loadout.TryGetSelectedRace(out RaceDefinition race) && ResourceManager.Instance
            ? ResourceManager.Instance.GetSprite(race.IconKey)
            : null;

        portrait.sprite = sprite;
        portrait.color = sprite ? Color.white : Color.clear;
    }
}
