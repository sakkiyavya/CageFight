using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Keep the TTSDK-generated profiler hidden until four fingers are held for two seconds.</summary>
[DefaultExecutionOrder(-32000)]
public sealed class DebugPanelGesture : MonoBehaviour
{
    private MonoBehaviour panel;
    private bool visible;
    private bool latched;
    private double holdStarted = -1;
    private readonly int[] fingerIds = new int[4];
    private readonly int[] currentIds = new int[4];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        var go = new GameObject("Debug panel gesture");
        DontDestroyOnLoad(go);
        go.AddComponent<DebugPanelGesture>();
    }

    private void Update()
    {
        // TTSDK creates this component only during exports with Profiling enabled.
        // Keep the listener separate so it can re-enable a hidden panel.
        if (!panel)
        {
            var go = GameObject.Find("StarkProfiler");
            if (go)
                foreach (var component in go.GetComponents<MonoBehaviour>())
                    if (component && component.GetType().Name == "StarkWebGLRuntimeProfiler")
                    {
                        panel = component;
                        panel.enabled = visible;
                        break;
                    }
        }

        // Use the active input module for TTSDK touch adaptation, including when timeScale is zero.
        var module = EventSystem.current ? EventSystem.current.currentInputModule : null;
        var input = module ? module.input : null;
        int count = input ? input.touchCount : UnityEngine.Input.touchCount;
        int active = 0;
        for (int i = 0; i < count; i++)
        {
            Touch touch = input ? input.GetTouch(i) : UnityEngine.Input.GetTouch(i);
            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) continue;
            if (active < 4) currentIds[active] = touch.fingerId;
            active++;
        }
        if (active == 0) latched = false;
        if (active != 4 || latched)
        {
            holdStarted = -1;
            return;
        }
        System.Array.Sort(currentIds);
        bool sameFingers = true;
        for (int i = 0; i < 4; i++)
            if (currentIds[i] != fingerIds[i]) sameFingers = false;
        if (holdStarted < 0 || !sameFingers)
        {
            System.Array.Copy(currentIds, fingerIds, 4);
            holdStarted = Time.realtimeSinceStartupAsDouble;
        }
        if (Time.realtimeSinceStartupAsDouble - holdStarted < 2.0) return;
        visible = !visible;
        if (panel) panel.enabled = visible;
        latched = true;
        holdStarted = -1;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) ResetHold();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) ResetHold();
    }

    private void ResetHold()
    {
        holdStarted = -1;
        latched = true; // Require release after returning from the background.
    }
}
