using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class StageAnimatorControllerData : ComponentData
{
    [ResourceKey(typeof(RuntimeAnimatorController))]
    public string animatorControllerKey;
}

[ExecuteAlways]
[RequireComponent(typeof(Animator))]
public class StageAnimatorController : MonoBehaviour, ILevelComponent
{
    [ResourceKey(typeof(RuntimeAnimatorController))]
    [Tooltip("Animator controller resource key.")]
    public string animatorControllerKey;

    private Animator _animator;

    public Type DataType => typeof(StageAnimatorControllerData);

    private void Start()
    {
        CacheComponent();

        if (Application.isPlaying)
        {
            ApplyRuntimeResource();
        }
    }

    private void OnEnable()
    {
        CacheComponent();

        if (Application.isPlaying)
        {
            ApplyRuntimeResource();
        }
#if UNITY_EDITOR
        else
        {
            UpdateEditorPreview();
        }
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ClearEditorPreview();
        }
#endif
    }

//     private void OnValidate()
//     {
//         CacheComponent();

// #if UNITY_EDITOR
//         if (!Application.isPlaying)
//         {
//             UpdateEditorPreview();
//         }
// #endif
//     }

    public ComponentData ExtractData()
    {
        return new StageAnimatorControllerData
        {
            animatorControllerKey = animatorControllerKey
        };
    }

    public void ApplyData(ComponentData data)
    {
        StageAnimatorControllerData controllerData = data as StageAnimatorControllerData;
        if (controllerData == null)
        {
            return;
        }

        animatorControllerKey = controllerData.animatorControllerKey;

        if (Application.isPlaying)
        {
            ApplyRuntimeResource();
        }
#if UNITY_EDITOR
        else
        {
            UpdateEditorPreview();
        }
#endif
    }

    private void CacheComponent()
    {
        if (_animator == null)
        {
            _animator = GetComponent<Animator>();
        }
    }

    private void ApplyRuntimeResource()
    {
        CacheComponent();

        if (_animator == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(animatorControllerKey))
        {
            _animator.runtimeAnimatorController = null;
            return;
        }

        RuntimeAnimatorController controller = ResourceManager.Instance != null
            ? ResourceManager.Instance.GetAnimatorController(animatorControllerKey)
            : null;
        if (controller == null)
        {
            Debug.LogWarning($"[StageAnimatorController] Missing RuntimeAnimatorController resource: {animatorControllerKey}", this);
        }

        _animator.runtimeAnimatorController = controller;
    }

#if UNITY_EDITOR
    private void UpdateEditorPreview()
    {
        CacheComponent();

        if (_animator == null || string.IsNullOrEmpty(animatorControllerKey))
        {
            ClearEditorPreview();
            return;
        }

        AnimatorControllerRegistry registry = FindRegistry<AnimatorControllerRegistry>();
        _animator.runtimeAnimatorController = registry != null ? registry.GetAsset(animatorControllerKey) : null;
    }

    private void ClearEditorPreview()
    {
        CacheComponent();

        if (_animator != null)
        {
            _animator.runtimeAnimatorController = null;
        }
    }

    private static T FindRegistry<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids.Length == 0)
        {
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }
#endif
}
