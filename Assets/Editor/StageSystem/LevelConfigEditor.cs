using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(LevelConfig))]
public class LevelConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI(); // 绘制原始的数据列表（方便查阅）

        EditorGUILayout.Space(20);

        if (GUILayout.Button("加载关卡", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("加载关卡预览", 
                "这将会清除当前场景中所有未标记为“常驻物品”的对象，确定要继续吗？\n(如果有未保存的内容请先保存)", "确定", "取消"))
            {
                LoadLevelToScene((LevelConfig)target);
            }
        }
    }

    private void LoadLevelToScene(LevelConfig config)
    {
        // 1. 清理场景中非“常驻”的根节点对象
        var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
        int destroyedCount = 0;
        
        foreach (var rootObj in rootObjects)
        {
            // 如果身上没有常驻标记，直接抹除
            if (rootObj.GetComponent<PermanentObjectMarker>() == null)
            {
                Undo.DestroyObjectImmediate(rootObj);
                destroyedCount++;
            }
        }

        // 2. 根据数据加载预制体并注入参数
        int loadedCount = 0;
        foreach (var objData in config.objects)
        {
            // 通过 AssetDatabase 在全工程中根据名字搜索对应的 Prefab (因为我们目前 key 就是 prefab.name)
            string[] guids = AssetDatabase.FindAssets($"{objData.prefabKey} t:Prefab");
            GameObject prefabAsset = null;
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset != null && asset.name == objData.prefabKey)
                {
                    prefabAsset = asset;
                    break;
                }
            }

            if (prefabAsset == null)
            {
                Debug.LogError($"[加载失败] 无法在工程中找到名为 '{objData.prefabKey}' 的预制体！");
                continue;
            }

            // 以保持 Prefab 链接的方式实例化
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
            Undo.RegisterCreatedObjectUndo(instance, "Load Level Object");

            // 还原 Transform
            instance.transform.position = objData.transform.position;
            instance.transform.eulerAngles = objData.transform.rotation;
            instance.transform.localScale = objData.transform.scale;

            // 还原组件参数
            var levelComponents = instance.GetComponentsInChildren<ILevelComponent>(true);
            foreach (var savedComponentData in objData.components)
            {
                foreach (var comp in levelComponents)
                {
                    // 匹配类型并注入覆盖数据
                    if (comp.DataType == savedComponentData.GetType())
                    {
                        comp.ApplyData(savedComponentData);
                        break;
                    }
                }
            }

            // 补充标记：让它在场景里依然被认为是个合法关卡物品，方便二次导出修改
            if (instance.GetComponent<LevelObjectMarker>() == null)
            {
                var marker = instance.AddComponent<LevelObjectMarker>();
                marker.hideFlags = HideFlags.HideInInspector;
            }

            loadedCount++;
        }

        Debug.Log($"<color=green><b>关卡加载完毕！</b></color> 清理了 {destroyedCount} 个对象，成功还原了 {loadedCount} 个物品。");
    }
}
