using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class StageTextureData : ComponentData
{
    [ResourceKey(typeof(Sprite))]
    public string spriteKey;
}

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class StageTexture : MonoBehaviour, ILevelComponent
{
    [ResourceKey(typeof(Sprite))]
    [Tooltip("Sprite resource key.")]
    public string spriteKey;

    private SpriteRenderer _spriteRenderer;

    public Type DataType => typeof(StageTextureData);

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
        // CacheComponent();

        // if (Application.isPlaying)
        // {
        //     ApplyRuntimeResource();
        // }
#if UNITY_EDITOR
        // else
        // {
            UpdateEditorPreview();
        // }
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
        return new StageTextureData
        {
            spriteKey = spriteKey
        };
    }

    public void ApplyData(ComponentData data)
    {
        if (data is not StageTextureData textureData) return;

        spriteKey = textureData.spriteKey;

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
        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void ApplyRuntimeResource()
    {
        CacheComponent();

        if (_spriteRenderer == null) return;

        if (string.IsNullOrEmpty(spriteKey))
        {
            _spriteRenderer.sprite = null;
            return;
        }

        Sprite sprite = ResourceManager.Instance != null ? ResourceManager.Instance.GetSprite(spriteKey) : null;
        // Debug.Log("ResourceManager实例：" + ResourceManager.Instance.name);
        if (sprite == null)
        {
            Debug.LogWarning($"[StageTexture] Missing Sprite resource: {spriteKey}", this);
        }

        // Debug.Log(name + " 的sprite: " + sprite.name);
        _spriteRenderer.sprite = sprite;
    }

#if UNITY_EDITOR
    private void UpdateEditorPreview()
    {
        CacheComponent();

        if (_spriteRenderer == null || string.IsNullOrEmpty(spriteKey))
        {
            ClearEditorPreview();
            return;
        }

        SpriteRegistry registry = FindRegistry<SpriteRegistry>();
        _spriteRenderer.sprite = registry != null ? registry.GetAsset(spriteKey) : null;

        if (_spriteRenderer.sprite == null)
        {
            ClearEditorPreview();
        }
    }

    private void ClearEditorPreview()
    {
        CacheComponent();

        if (_spriteRenderer != null)
        {
            _spriteRenderer.sprite = null;
        }
    }

    private static T FindRegistry<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids.Length == 0) return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }
#endif
}
