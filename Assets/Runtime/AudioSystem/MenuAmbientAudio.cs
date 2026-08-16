using UnityEngine;

/// <summary>菜单循环环境音与开始关卡提示音。</summary>
public sealed class MenuAmbientAudio : MonoBehaviour
{
    public static MenuAmbientAudio Instance { get; private set; }

    [SerializeField, Range(0f, 1f)] private float cageDoorVolume = .7f;
    [SerializeField] private string cageDoorKey = "Cage door";
    [SerializeField] private string beginKey = "Begin";

    private AudioSource musicRequest;
    private AudioSource effectRequest;
    private bool stageRequested;
    private bool cageRequested;
    private bool beginPending;

    private void Awake()
    {
        Instance = this;
        musicRequest = gameObject.AddComponent<AudioSource>();
        musicRequest.playOnAwake = false;
        musicRequest.loop = true;
        musicRequest.spatialBlend = 0f;

        effectRequest = gameObject.AddComponent<AudioSource>();
        effectRequest.playOnAwake = false;
        effectRequest.spatialBlend = 0f;
        effectRequest.priority = 4;
    }

    private void Update()
    {
        if (beginPending) TryPlayBegin();

        SceneFSM fsm = SceneFSM.Instance;
        if (fsm && fsm.CurrentStateEnum == GameState.Menu) stageRequested = false;
        if (stageRequested || (fsm && (fsm.CurrentStateEnum == GameState.Loading ||
            fsm.CurrentStateEnum == GameState.Gameplay))) return;

        TryPlayCageDoor();
    }

    public static void NotifyBeginStage()
    {
        if (Instance) Instance.BeginStage();
    }

    public static void NotifyMenuBegin()
    {
        if (!Instance) return;
        Instance.beginPending = true;
        Instance.TryPlayBegin();
    }

    private void BeginStage()
    {
        stageRequested = true;
        cageRequested = false;
        beginPending = true;
        AudioManager.Instance?.StopMusic();
        TryPlayBegin();
    }

    private void TryPlayBegin()
    {
        AudioClip clip = ResourceManager.Instance ? ResourceManager.Instance.GetAudio(beginKey) : null;
        if (!clip || !AudioManager.Instance) return;
        beginPending = false;
        effectRequest.clip = clip;
        AudioManager.Instance.PlayEffect(effectRequest, 4, 0f,
            Camera.main ? Camera.main.transform : transform);
    }

    private void TryPlayCageDoor()
    {
        if (cageRequested || !AudioManager.Instance || !ResourceManager.Instance) return;
        AudioClip clip = ResourceManager.Instance.GetAudio(cageDoorKey);
        if (!clip) return;

        cageRequested = true;
        musicRequest.clip = clip;
        musicRequest.volume = cageDoorVolume;
        AudioManager.Instance.PlayMusic(musicRequest);
    }
}
