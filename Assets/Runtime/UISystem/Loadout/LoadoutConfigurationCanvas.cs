using UnityEngine;
using System.Collections;

public sealed class LoadoutConfigurationCanvas : MonoBehaviour
{
    [SerializeField] private PlayerLoadoutManager loadout;
    [SerializeField] private GameObject engineerPage;
    [SerializeField] private GameObject racePage;
    [SerializeField] private GameObject spellPage;
    [SerializeField] private LoadoutSelectionPanel engineerPanel;
    [SerializeField] private LoadoutSelectionPanel racePanel;
    [SerializeField] private LoadoutSelectionPanel spellPanel;
    [Header("Visual")]
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private RectTransform closeButton;
    [SerializeField] private RectTransform[] navigationButtons;
    [SerializeField] private Sprite selectedCheckSprite;
    [SerializeField, Range(.5f, 1f)] private float panelScale = .7f;
    [SerializeField] private float transitionTime = .28f;
    [SerializeField] private float enterDistance = 900f;

    private int page;
    private bool layoutCached;
    private Coroutine motion;
    private Vector2 panelEndPosition;
    private Vector2 closeEndPosition;
    private Vector3 panelEndScale;
    private Vector3 closeEndScale;
    private float closeEndAngle;
    private Vector2[] navigationEndPositions;
    private Vector3[] navigationEndScales;

    private void Awake()
    {
        CacheLayout();
        ApplyCheckSprite();
    }

    public void OpenEngineer() => Open(engineerPage);
    public void OpenRace() => Open(racePage);

    public void OpenSpell()
    {
        if (spellPanel && loadout)
            spellPanel.SetSpellSlot(loadout.TryGetGameplaySpell(1, out _) ? 1 : 0);
        Open(spellPage);
    }

    public void PreviousCategory() => OpenCategory(page - 1);
    public void NextCategory() => OpenCategory(page + 1);
    public void PreviousOptions() => CurrentPanel()?.PreviousPage();
    public void NextOptions() => CurrentPanel()?.NextPage();

    public void Close()
    {
        if (!gameObject.activeSelf) return;
        if (motion != null) StopCoroutine(motion);
        motion = StartCoroutine(Motion(false));
    }

    private void Open(GameObject page)
    {
        bool wasOpen = gameObject.activeSelf;
        if (!wasOpen) gameObject.SetActive(true);
        CacheLayout();
        ApplyCheckSprite();
        if (engineerPage) engineerPage.SetActive(page == engineerPage);
        if (racePage) racePage.SetActive(page == racePage);
        if (spellPage) spellPage.SetActive(page == spellPage);
        this.page = page == racePage ? 1 : page == spellPage ? 2 : 0;
        if (!wasOpen)
        {
            if (motion != null) StopCoroutine(motion);
            motion = StartCoroutine(Motion(true));
        }
    }

    private void OpenCategory(int value)
    {
        page = (value % 3 + 3) % 3;
        if (page == 0) OpenEngineer();
        else if (page == 1) OpenRace();
        else OpenSpell();
    }

    private LoadoutSelectionPanel CurrentPanel()
    {
        return page == 0 ? engineerPanel : page == 1 ? racePanel : spellPanel;
    }

    private void CacheLayout()
    {
        if (layoutCached || !panelRoot) return;
        layoutCached = true;
        panelEndPosition = panelRoot.anchoredPosition;
        panelEndScale = panelRoot.localScale * panelScale;
        if (!closeButton) return;
        closeEndPosition = closeButton.anchoredPosition * panelScale;
        closeEndScale = closeButton.localScale * panelScale;
        closeEndAngle = closeButton.localEulerAngles.z;
        navigationEndPositions = new Vector2[navigationButtons == null ? 0 : navigationButtons.Length];
        navigationEndScales = new Vector3[navigationEndPositions.Length];
        for (int i = 0; i < navigationEndPositions.Length; i++)
        {
            RectTransform button = navigationButtons[i];
            if (!button) continue;
            navigationEndPositions[i] = button.anchoredPosition * panelScale;
            navigationEndScales[i] = button.localScale * panelScale;
        }
    }

    private void ApplyCheckSprite()
    {
        engineerPanel?.SetCheckSprite(selectedCheckSprite);
        racePanel?.SetCheckSprite(selectedCheckSprite);
        spellPanel?.SetCheckSprite(selectedCheckSprite);
    }

    private IEnumerator Motion(bool opening)
    {
        Vector2 panelStart = opening ? panelEndPosition + Vector2.right * enterDistance : panelEndPosition;
        Vector2 panelTarget = opening ? panelEndPosition : panelEndPosition + Vector2.right * enterDistance;
        Vector3 scaleStart = opening ? panelEndScale * .9f : panelEndScale;
        Vector3 scaleTarget = opening ? panelEndScale : panelEndScale * .9f;
        Vector2 closeStart = opening ? closeEndPosition + Vector2.right * enterDistance : closeEndPosition;
        Vector2 closeTarget = opening ? closeEndPosition : closeEndPosition + Vector2.right * enterDistance;
        float angleStart = opening ? closeEndAngle - 180f : closeEndAngle;
        float angleTarget = opening ? closeEndAngle : closeEndAngle + 180f;

        if (panelRoot)
        {
            panelRoot.anchoredPosition = panelStart;
            panelRoot.localScale = scaleStart;
        }
        if (closeButton)
        {
            closeButton.anchoredPosition = closeStart;
            closeButton.localScale = closeEndScale * (opening ? .8f : 1f);
            closeButton.localRotation = Quaternion.Euler(0f, 0f, angleStart);
        }
        SetNavigation(0f, opening);

        for (float time = 0f; time < transitionTime; )
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, time / transitionTime);
            if (panelRoot)
            {
                panelRoot.anchoredPosition = Vector2.Lerp(panelStart, panelTarget, t);
                panelRoot.localScale = Vector3.Lerp(scaleStart, scaleTarget, t);
            }
            if (closeButton)
            {
                closeButton.anchoredPosition = Vector2.Lerp(closeStart, closeTarget, t);
                closeButton.localScale = Vector3.Lerp(closeEndScale * (opening ? .8f : 1f),
                    closeEndScale * (opening ? 1f : .8f), t);
                closeButton.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(angleStart, angleTarget, t));
            }
            SetNavigation(t, opening);
            yield return null;
        }

        if (panelRoot)
        {
            panelRoot.anchoredPosition = panelTarget;
            panelRoot.localScale = scaleTarget;
        }
        if (closeButton)
        {
            closeButton.anchoredPosition = closeTarget;
            closeButton.localScale = closeEndScale * (opening ? 1f : .8f);
            closeButton.localRotation = Quaternion.Euler(0f, 0f, angleTarget);
        }
        SetNavigation(1f, opening);
        motion = null;
        if (!opening) gameObject.SetActive(false);
    }

    private void SetNavigation(float time, bool opening)
    {
        if (navigationButtons == null) return;
        for (int i = 0; i < navigationButtons.Length; i++)
        {
            RectTransform button = navigationButtons[i];
            if (!button) continue;
            Vector2 endPosition = navigationEndPositions[i];
            Vector3 endScale = navigationEndScales[i];
            Vector2 startPosition = endPosition + Vector2.right * enterDistance;
            Vector3 startScale = endScale * .8f;
            button.anchoredPosition = Vector2.Lerp(opening ? startPosition : endPosition,
                opening ? endPosition : startPosition, time);
            button.localScale = Vector3.Lerp(opening ? startScale : endScale,
                opening ? endScale : startScale, time);
        }
    }
}
