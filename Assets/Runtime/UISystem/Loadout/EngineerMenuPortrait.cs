using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>主界面当前工程师头像：显示对应头像并循环其休闲帧。</summary>
[DisallowMultipleComponent]
public sealed class EngineerMenuPortrait : MonoBehaviour
{
    [SerializeField] private PlayerLoadoutManager loadout;
    [SerializeField] private Image portrait;

    private Sprite[] frames;
    private float frameTime;
    private float elapsed;
    private int frameIndex;
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

    private IEnumerator SetupRoutine()
    {
        while (loadout && !loadout.IsReady) yield return null;
        if (loadout) yield return loadout.PreloadPresentationResources();
        setupRoutine = null;
        Refresh();
    }

    private void Update()
    {
        if (frames == null || frames.Length < 2 || !portrait) return;
        elapsed += Time.unscaledDeltaTime;
        if (elapsed < frameTime) return;
        elapsed -= frameTime;
        frameIndex = (frameIndex + 1) % frames.Length;
        portrait.sprite = frames[frameIndex];
    }

    private void Refresh()
    {
        frames = null;
        frameIndex = 0;
        elapsed = 0f;
        if (!portrait || !loadout || !loadout.TryGetSelectedEngineer(out EngineerDefinition engineer) ||
            !ResourceManager.Instance) return;

        string[] keys = engineer.IdlePortraitFrameKeys;
        if (keys.Length == 0)
        {
            portrait.sprite = ResourceManager.Instance.GetSprite(engineer.IconKey);
            return;
        }

        frames = new Sprite[keys.Length];
        int count = 0;
        for (int i = 0; i < keys.Length; i++)
        {
            Sprite sprite = ResourceManager.Instance.GetSprite(keys[i]);
            if (sprite) frames[count++] = sprite;
        }
        if (count == 0)
        {
            portrait.sprite = ResourceManager.Instance.GetSprite(engineer.IconKey);
            frames = null;
            return;
        }
        if (count != frames.Length) System.Array.Resize(ref frames, count);
        frameTime = 1f / Mathf.Max(1f, engineer.IdlePortraitFrameRate);
        portrait.sprite = frames[0];
    }
}
