using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class SceneStateBase : MonoBehaviour
{
    [Header("该状态激活时打开的 UI 模块")]
    [SerializeField] List<UISystemBase> stateModules = new List<UISystemBase>();

    private LevelConfig _levelConfig;

    protected LevelConfig CurrentLevelConfig => _levelConfig;

    internal void SetLevelConfig(LevelConfig levelConfig)
    {
        _levelConfig = levelConfig;
    }

    public virtual IEnumerator Enter()
    {
        yield return OpenModules();
        yield return OnEnter();
    }

    public virtual IEnumerator Exit()
    {
        yield return CloseModules();
        yield return OnExit();
    }

    private IEnumerator OpenModules()
    {
        var coroutines = new List<Coroutine>();
        foreach (var module in stateModules)
        {
            if (module != null)
            {
                module.gameObject.SetActive(true);
                coroutines.Add(StartCoroutine(module.UIMotionEffectRoutine(true)));
            }
        }
        foreach (var coroutine in coroutines)
            yield return coroutine;
    }

    private IEnumerator CloseModules()
    {
        var coroutines = new List<Coroutine>();
        foreach (var module in stateModules)
        {
            if (module != null)
            {
                if (module.gameObject.activeInHierarchy)
                    coroutines.Add(StartCoroutine(module.UIMotionEffectRoutine(false)));
            }
        }
        foreach (var coroutine in coroutines)
            yield return coroutine;

        foreach (var module in stateModules)
        {
            if (module != null)
                module.gameObject.SetActive(false);
        }
    }

    protected virtual IEnumerator OnEnter() { yield return null; }
    protected virtual IEnumerator OnExit() { yield return null; }
}
