using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

public class CodePackageToolWindow : EditorWindow
{
    private string inputDirectory = "";
    private string outputDirectory = "";
    private string outputFileName = "CodePackage.txt";

    [MenuItem("Tools/代码打包工具")]
    public static void ShowWindow()
    {
        var window = GetWindow<CodePackageToolWindow>("代码打包");
        window.minSize = new Vector2(400, 220);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("配置参数", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        // 1. 输入目录
        EditorGUILayout.BeginHorizontal();
        inputDirectory = EditorGUILayout.TextField("输入目录 (.cs所在)", inputDirectory);
        if (GUILayout.Button("浏览...", GUILayout.Width(60)))
        {
            // 使用资源管理器弹窗选择文件夹
            string path = EditorUtility.OpenFolderPanel("选择要打包的C#代码目录", inputDirectory, "");
            if (!string.IsNullOrEmpty(path))
            {
                inputDirectory = path;
            }
        }
        EditorGUILayout.EndHorizontal();

        // 2. 输出目录
        EditorGUILayout.BeginHorizontal();
        outputDirectory = EditorGUILayout.TextField("输出目录", outputDirectory);
        if (GUILayout.Button("浏览...", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFolderPanel("选择TXT文件的保存目录", outputDirectory, "");
            if (!string.IsNullOrEmpty(path))
            {
                outputDirectory = path;
            }
        }
        EditorGUILayout.EndHorizontal();

        // 3. 输出文件名
        outputFileName = EditorGUILayout.TextField("输出文件名", outputFileName);
        
        if (!string.IsNullOrEmpty(outputFileName) && !outputFileName.EndsWith(".txt"))
        {
            EditorGUILayout.HelpBox("建议输出文件名以 .txt 结尾以便于阅读", MessageType.Info);
        }

        EditorGUILayout.Space(20);

        // 执行按钮
        if (GUILayout.Button("开始合并导出", GUILayout.Height(40)))
        {
            GenerateCodePackage();
        }
    }

    private void GenerateCodePackage()
    {
        // 校验输入
        if (string.IsNullOrEmpty(inputDirectory) || !Directory.Exists(inputDirectory))
        {
            EditorUtility.DisplayDialog("错误", "请输入有效的输入目录！", "确定");
            return;
        }

        if (string.IsNullOrEmpty(outputDirectory) || !Directory.Exists(outputDirectory))
        {
            EditorUtility.DisplayDialog("错误", "请输入有效的输出目录！", "确定");
            return;
        }

        if (string.IsNullOrEmpty(outputFileName))
        {
            EditorUtility.DisplayDialog("错误", "输出文件名不能为空！", "确定");
            return;
        }

        // 确保文件名有 .txt 后缀
        string finalFileName = outputFileName;
        if (!finalFileName.EndsWith(".txt"))
        {
            finalFileName += ".txt";
        }

        string fullOutputPath = Path.Combine(outputDirectory, finalFileName);
        
        // 递归查找所有的 .cs 文件
        string[] csFiles = Directory.GetFiles(inputDirectory, "*.cs", SearchOption.AllDirectories);

        if (csFiles.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "在指定的输入目录中没有找到任何 .cs 文件！", "确定");
            return;
        }

        // 使用 StringBuilder 拼接内容（软著申请专用：剔除所有工具生成的辅助注释）
        StringBuilder sb = new StringBuilder();

        foreach (string file in csFiles)
        {
            try
            {
                // 读取纯粹的代码文本
                string content = File.ReadAllText(file);
                
                // 去除可能导致的冗余空行，如果不需要可以不Trim
                sb.AppendLine(content.TrimEnd());
            }
            catch (System.Exception e)
            {
                Debug.LogError($"读取代码文件失败: {file}\n错误信息: {e.Message}");
            }
        }

        // 写入到最终文件
        try
        {
            File.WriteAllText(fullOutputPath, sb.ToString(), Encoding.UTF8);
            
            // 如果输出的目录在 Unity 工程内，刷新一下让它立马出现在 Project 窗口里
            if (fullOutputPath.Replace('\\', '/').Contains("/Assets/"))
            {
                AssetDatabase.Refresh();
            }

            EditorUtility.DisplayDialog("导出成功", $"代码打包完成！\n共合并了 {csFiles.Length} 个文件。\n\n输出路径:\n{fullOutputPath}", "确定");
            
            // 选中生成的 txt 文件高亮显示
            if (fullOutputPath.Replace('\\', '/').Contains("/Assets/"))
            {
                string relativeAssetPath = "Assets" + fullOutputPath.Replace('\\', '/').Split(new string[] { "/Assets" }, System.StringSplitOptions.None)[1];
                Object textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(relativeAssetPath);
                if (textAsset != null)
                {
                    EditorGUIUtility.PingObject(textAsset);
                }
            }
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("写入错误", $"写入 txt 文件时发生错误:\n{e.Message}", "确定");
        }
    }
}
