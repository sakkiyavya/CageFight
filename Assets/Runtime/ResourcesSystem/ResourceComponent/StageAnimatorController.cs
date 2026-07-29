using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class StageAnimatorControllerData : ComponentData
{
    [ResourceKey(typeof(RuntimeAnimatorController))]
    public string animatorControllerKey;                                                     // 序列化保存的动画控制器资源键。
}

[ExecuteAlways]
[RequireComponent(typeof(Animator))]
public class StageAnimatorController : MonoBehaviour, IStageComponent
{
    [ResourceKey(typeof(RuntimeAnimatorController))]
    [Tooltip("Animator controller resource key.")]
    public string animatorControllerKey;                                                     // 当前 Animator 需要使用的控制器资源键。

    private Animator _animator;                                                              // 接收运行时或编辑器预览控制器的 Animator。

    public Type DataType => typeof(StageAnimatorControllerData);                             // 该组件对应的序列化数据类型。

    #region 生命周期与回调
    /// <summary>
    /// 缓存 Animator，并在运行时从资源管理器应用当前资源键对应的控制器。
    /// </summary>
    private void Start()
    {
        CacheComponent();

        if (Application.isPlaying)
        {
            ApplyRuntimeResource();
        }
    }

    /// <summary>
    /// 缓存 Animator；运行时应用已加载控制器，编辑器环境则刷新同步预览。
    /// </summary>
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

    /// <summary>
    /// 编辑器预览状态下停用组件时清除临时写入的动画控制器。
    /// </summary>
    private void OnDisable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ClearEditorPreview();
        }
#endif
    }
    #endregion

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

    #region 关卡数据转换
    /// <summary>
    /// 将当前动画控制器资源键导出为可写入关卡配置的组件数据。
    /// </summary>
    /// <returns>包含当前控制器资源键的 <see cref="StageAnimatorControllerData"/>。</returns>
    public ComponentData ExtractData()
    {
        return new StageAnimatorControllerData
        {
            animatorControllerKey = animatorControllerKey
        };
    }

    /// <summary>
    /// 从动画控制器组件数据恢复资源键，并根据运行环境应用已加载控制器或刷新编辑器预览。
    /// </summary>
    /// <param name="data">期望为 <see cref="StageAnimatorControllerData"/> 的关卡组件数据；类型不匹配时忽略。</param>
    public void ApplyData(ComponentData data)
    {
        StageAnimatorControllerData controllerData = data as StageAnimatorControllerData;    // 类型转换后的控制器数据。
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
    #endregion

    #region 运行时资源注入
    /// <summary>
    /// 在尚未缓存时获取同一对象上的 Animator。
    /// </summary>
    private void CacheComponent()
    {
        if (_animator == null)
        {
            _animator = GetComponent<Animator>();
        }
    }

    /// <summary>
    /// 根据当前资源键从资源管理器取得已加载动画控制器并写入 Animator；
    /// 空资源键会清空控制器，资源缺失时会记录警告。
    /// </summary>
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
    #endregion

#if UNITY_EDITOR
    #region 编辑器预览
    /// <summary>
    /// 在编辑器中查找控制器注册表并同步当前资源键对应的强引用，用于无需运行游戏的场景预览。
    /// </summary>
    private void UpdateEditorPreview()
    {
        CacheComponent();

        if (_animator == null || string.IsNullOrEmpty(animatorControllerKey))
        {
            ClearEditorPreview();
            return;
        }

        AnimatorControllerRegistry registry = FindRegistry<AnimatorControllerRegistry>();    // 编辑器预览使用的控制器注册表。
        _animator.runtimeAnimatorController = registry != null ? registry.GetAsset(animatorControllerKey) : null;
    }

    /// <summary>
    /// 清除编辑器预览写入的动画控制器。
    /// </summary>
    private void ClearEditorPreview()
    {
        CacheComponent();

        if (_animator != null)
        {
            _animator.runtimeAnimatorController = null;
        }
    }

    /// <summary>
    /// 在编辑器资产数据库中查找指定类型的第一个资源注册表。
    /// </summary>
    /// <typeparam name="T">需要查找的注册表 ScriptableObject 类型。</typeparam>
    /// <returns>找到的第一个注册表资产；没有匹配资产时返回 <see langword="null"/>。</returns>
    private static T FindRegistry<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");                    // 匹配指定类型的资产 GUID。
        if (guids.Length == 0)
        {
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);                               // 第一个匹配注册表的资产路径。
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }
    #endregion
#endif
}
