using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 从 Addressables 顺序加载关卡配置（stage1, stage2...），
/// 遇到第一个缺失即停止，将结果交给 LevelButtonLayout。
/// </summary>
public class LevelConfigLoader : MonoBehaviour
{
    [SerializeField] private LevelButtonLayout layout;

    private readonly List<AsyncOperationHandle> _handles = new List<AsyncOperationHandle>();

    private void Start() => StartCoroutine(LoadConfigs());

    private IEnumerator LoadConfigs()
    {
        var configs = new List<LevelConfig>();

        for (int i = 1; ; i++)
        {
            var handle = Addressables.LoadAssetAsync<LevelConfig>($"Stage{i}");
            yield return handle;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Addressables.Release(handle);
                break;
            }

            _handles.Add(handle);
            configs.Add(handle.Result);
        }

        if(layout)
        {
            layout.configs = configs;
            layout.LayoutButtons();
        }
    }

    private void OnDestroy()
    {
        foreach (var h in _handles)
            if (h.IsValid()) Addressables.Release(h);
    }
}
