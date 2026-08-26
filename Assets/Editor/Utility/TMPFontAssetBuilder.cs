using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// 编辑器工具：从字体文件 + 常用字表 + 项目实际用字扫描，生成 TextMeshPro 静态中文字体资产。
/// 菜单：Tools → TMP → 生成中文字体资产（SourceHanSansSC）。
/// 用途：抖音小游戏包体受限，字体文件不进包；用静态 SDF 字库只收录游戏实际用到的字符。
/// 以后文案新增了没收录的字符，重新点一次本菜单即可自动补全。
/// 注意（TMP 3.0.7 行为）：TryAddCharacters 仅支持 Dynamic 模式，因此流程为
/// Dynamic 创建 → 补源字体引用 → 加字 → 清源字体引用 → 切回 Static 保存。
/// </summary>
public static class TMPFontAssetBuilder
{
    private const string FontPath = "Assets/Resource/LocalResource/Font/SourceHanSansSC-Bold.otf";
    private const string CharTablePath = "Assets/Resource/LocalResource/Font/常用字表.txt";
    private const string OutputPath = "Assets/Resource/LocalResource/Font/SourceHanSansSC-Bold SDF.asset";
    private const int SamplingPointSize = 36;
    private const int AtlasPadding = 9;
    private const int AtlasWidth = 2048;
    private const int AtlasHeight = 2048;

    [MenuItem("Tools/TMP/生成中文字体资产（SourceHanSansSC）")]
    public static void Build()
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font == null)
        {
            Debug.LogError($"[TMPFontAssetBuilder] 未找到字体文件：{FontPath}");
            return;
        }

        // 1. 常用字表（手工维护的基础集）
        var charSet = new HashSet<char>();
        if (File.Exists(CharTablePath))
        {
            foreach (char c in File.ReadAllText(CharTablePath, Encoding.UTF8))
                charSet.Add(c);
        }

        // 2. 项目实际用字扫描（自动维护：代码/资产/场景文本里出现的字符全部收录）
        CollectProjectCharacters(charSet);

        // 3. 生成字体资产（先用 Dynamic，因为 TryAddCharacters 只支持 Dynamic）
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            font, SamplingPointSize, AtlasPadding, GlyphRenderMode.SDFAA,
            AtlasWidth, AtlasHeight, AtlasPopulationMode.Dynamic, true);
        if (fontAsset == null)
        {
            Debug.LogError("[TMPFontAssetBuilder] 字体资产创建失败。");
            return;
        }

        fontAsset.name = "SourceHanSansSC-Bold SDF";

        // 4. API 创建的资产 m_SourceFontFile 为空，而 TryAddCharacters 通过它加载字形，必须先补上。
        SetSourceFontFile(fontAsset, font);

        char[] characters = new char[charSet.Count];
        charSet.CopyTo(characters);
        string missing = string.Empty;
        bool added = fontAsset.TryAddCharacters(new string(characters), out missing);
        if (!added)
        {
            int shown = missing.Length > 200 ? 200 : missing.Length;
            Debug.LogWarning($"[TMPFontAssetBuilder] 字体缺以下字符（字形缺失）：{missing.Substring(0, shown)}…共 {missing.Length} 个");
        }

        // 5. 加字完成后清掉源字体引用（避免 16MB 字体被带进构建），并切回 Static（运行时不做动态扩容）。
        SetSourceFontFile(fontAsset, null);
        SetAtlasPopulationMode(fontAsset, AtlasPopulationMode.Static);

        // 6. 覆盖保存（重新生成后 GUID 会变，需要重新拖到 TMP 组件的 Font Asset 上）
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputPath) != null)
            AssetDatabase.DeleteAsset(OutputPath);
        AssetDatabase.CreateAsset(fontAsset, OutputPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[TMPFontAssetBuilder] 完成：{OutputPath}（收录 {charSet.Count} 字符，实际字形 {fontAsset.characterTable.Count} 个；" +
                  $"缺失 {missing.Length} 个）。请把新资产重新拖到 TMP 组件的 Font Asset。");
    }

    /// <summary>通过 SerializedObject 写入 m_SourceFontFile（TMP 内部字段，外部程序集不可直接访问）。</summary>
    private static void SetSourceFontFile(TMP_FontAsset fontAsset, Font font)
    {
        SerializedObject so = new SerializedObject(fontAsset);
        SerializedProperty prop = so.FindProperty("m_SourceFontFile");
        if (prop != null)
        {
            prop.objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>通过 SerializedObject 写入 m_AtlasPopulationMode。</summary>
    private static void SetAtlasPopulationMode(TMP_FontAsset fontAsset, AtlasPopulationMode mode)
    {
        SerializedObject so = new SerializedObject(fontAsset);
        SerializedProperty prop = so.FindProperty("m_AtlasPopulationMode");
        if (prop != null)
        {
            prop.enumValueIndex = (int)mode;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>扫描项目文本资产（代码/配置/预制体/场景），收集所有非 ASCII 可见字符。</summary>
    private static void CollectProjectCharacters(HashSet<char> charSet)
    {
        string[] assetPaths = AssetDatabase.GetAllAssetPaths();
        foreach (string assetPath in assetPaths)
        {
            string ext = Path.GetExtension(assetPath).ToLowerInvariant();
            if (ext != ".cs" && ext != ".asset" && ext != ".prefab" &&
                ext != ".unity" && ext != ".txt" && ext != ".json" && ext != ".md")
                continue;

            string text;
            try
            {
                text = File.ReadAllText(assetPath, Encoding.UTF8);
            }
            catch
            {
                continue;
            }

            foreach (char c in text)
            {
                int v = c;
                bool useful =
                    (v >= 0x4E00 && v <= 0x9FFF) ||   // CJK 统一表意文字
                    (v >= 0x3000 && v <= 0x303F) ||   // CJK 标点
                    (v >= 0xFF00 && v <= 0xFFEF) ||   // 全角形式
                    (v >= 0x2000 && v <= 0x206F) ||   // 通用标点
                    (v >= 0x2190 && v <= 0x21FF) ||   // 箭头
                    (v >= 0x2460 && v <= 0x24FF) ||   // 带圈数字
                    (v >= 0x2500 && v <= 0x257F) ||   // 制表符
                    (v >= 0x25A0 && v <= 0x25FF) ||   // 几何图形
                    (v >= 0x2600 && v <= 0x26FF) ||   // 杂项符号
                    (v >= 0x00A0 && v <= 0x00FF) ||   // 拉丁补充
                    (v >= 0x20 && v <= 0x7E);         // ASCII 可打印
                if (useful)
                    charSet.Add(c);
            }
        }
    }
}
