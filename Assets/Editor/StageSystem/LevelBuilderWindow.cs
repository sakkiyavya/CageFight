using UnityEditor;
using UnityEngine;

public class LevelBuilderWindow : EditorWindow
{
    private string savePath = "Assets/Resource/RemoteResource/Stages";
    private uint levelId = 1;

    [MenuItem("关卡构建/创建新关卡")]
    public static void ShowWindow()
    {
        var window = GetWindow<LevelBuilderWindow>("关卡构建");
        window.minSize = new Vector2(350, 160);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("关卡配置导出设置", EditorStyles.boldLabel);
        
        EditorGUILayout.Space(10);
        savePath = EditorGUILayout.TextField("保存路径", savePath);
        
        // EditorGUILayout.IntField 只支持 int，我们自己做转换保证是非负的 uint 行为
        int inputId = EditorGUILayout.IntField("关卡编号", (int)levelId);
        levelId = (uint)Mathf.Max(0, inputId);

        EditorGUILayout.Space(25);
        if (GUILayout.Button("确认生成关卡配置", GUILayout.Height(40)))
        {
            LevelExporter.ExportLevel(levelId, savePath);
        }
    }
}
