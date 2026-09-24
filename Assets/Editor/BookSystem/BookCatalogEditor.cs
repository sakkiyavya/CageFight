using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// BookCatalog 自定义 Inspector：
/// 条目按派系（label 页）折叠分组，每个派系可单独新增；
/// categoryId（label 页）与 raceId（Race 种族图标）为下拉选择，
/// star 为 1-7 下拉；头像与立绘为拖拽 Sprite + 自动反查 SpriteRegistry 资源键，
/// Explain 为多行文本框。
/// </summary>
[CustomEditor(typeof(BookCatalog))]
public sealed class BookCatalogEditor : Editor
{
    private SerializedProperty _categories;
    private SerializedProperty _raceOptions;
    private SerializedProperty _entries;

    private SpriteRegistry _registry;
    private Dictionary<Sprite, string> _spriteToKey;   // 拖入的 Sprite -> 注册表资源键（编辑器强引用相等）。

    private static readonly int[] StarValues = { 1, 2, 3, 4, 5, 6, 7 };
    private static readonly string[] StarLabels = { "1", "2", "3", "4", "5", "6", "7" };

    private void OnEnable()
    {
        _categories = serializedObject.FindProperty("categories");
        _raceOptions = serializedObject.FindProperty("raceOptions");
        _entries = serializedObject.FindProperty("entries");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_categories, new GUIContent("派系页签（label 顺序）"), true);
        EditorGUILayout.PropertyField(_raceOptions, new GUIContent("种族图标选项（对应 Card 的 Race 子对象名）"), true);
        EditorGUILayout.Space();

        EnsureRegistryIndex();

        List<string> categoryChoices = ReadStrings(_categories);
        List<string> raceChoices = ReadStrings(_raceOptions);

        EditorGUILayout.LabelField("图鉴条目（按派系分组）", EditorStyles.boldLabel);

        int deleteIndex = -1;

        for (int ci = 0; ci < categoryChoices.Count; ci++)
        {
            string cat = categoryChoices[ci];
            int count = CountEntries(cat);
            bool fold = EditorPrefs.GetBool("BookCatalogEditor.fold." + cat, true);
            fold = EditorGUILayout.Foldout(fold, "派系 [" + cat + "]（" + count + " 条）", true);
            EditorPrefs.SetBool("BookCatalogEditor.fold." + cat, fold);
            if (!fold)
                continue;

            EditorGUI.indentLevel++;
            for (int i = 0; i < _entries.arraySize; i++)
            {
                SerializedProperty e = _entries.GetArrayElementAtIndex(i);
                string eCat = e.FindPropertyRelative("categoryId").stringValue;
                if (eCat != cat)
                    continue;

                EditorGUILayout.BeginVertical("box");
                DrawEntry(e, i, categoryChoices, raceChoices);
                if (GUILayout.Button("删除此条目"))
                    deleteIndex = i;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            if (GUILayout.Button("＋ 新增到 " + cat))
                AddEntry(cat, raceChoices);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // 未分类（categoryId 为空或不在列表里的条目）
        int unassigned = CountEntries(string.Empty);
        for (int i = 0; i < _entries.arraySize; i++)
        {
            string eCat = _entries.GetArrayElementAtIndex(i).FindPropertyRelative("categoryId").stringValue;
            if (!string.IsNullOrEmpty(eCat) && categoryChoices.Contains(eCat))
                continue;
            unassigned++;
        }
        bool foldUn = EditorPrefs.GetBool("BookCatalogEditor.fold.UNASSIGNED", true);
        foldUn = EditorGUILayout.Foldout(foldUn, "未分类（" + unassigned + " 条）", true);
        EditorPrefs.SetBool("BookCatalogEditor.fold.UNASSIGNED", foldUn);
        if (foldUn)
        {
            EditorGUI.indentLevel++;
            for (int i = 0; i < _entries.arraySize; i++)
            {
                SerializedProperty e = _entries.GetArrayElementAtIndex(i);
                string eCat = e.FindPropertyRelative("categoryId").stringValue;
                if (!string.IsNullOrEmpty(eCat) && categoryChoices.Contains(eCat))
                    continue;

                EditorGUILayout.BeginVertical("box");
                DrawEntry(e, i, categoryChoices, raceChoices);
                if (GUILayout.Button("删除此条目"))
                    deleteIndex = i;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }
            EditorGUI.indentLevel--;
        }

        if (deleteIndex >= 0)
            _entries.DeleteArrayElementAtIndex(deleteIndex);

        EditorGUILayout.Space();
        if (GUILayout.Button("＋ 新增条目（默认第一派系）", GUILayout.Height(24)))
            AddEntry(categoryChoices.Count > 0 ? categoryChoices[0] : string.Empty, raceChoices);

        serializedObject.ApplyModifiedProperties();
        SortEntriesByStar();
        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// 按（所属派系, 星级）稳定升序整理条目列表：同一派系内 1 星在前、7 星在后，
    /// 未分类条目排在最后。已有序时跳过，保证每次绘制开销为 O(n)。
    /// </summary>
    private void SortEntriesByStar()
    {
        int n = _entries.arraySize;
        if (n < 2)
            return;

        List<string> cats = ReadStrings(_categories);
        var data = new List<KeyValuePair<int, int>>(n);
        for (int i = 0; i < n; i++)
        {
            SerializedProperty e = _entries.GetArrayElementAtIndex(i);
            string cat = e.FindPropertyRelative("categoryId").stringValue;
            int ci = cats.IndexOf(cat);
            if (ci < 0)
                ci = int.MaxValue;   // 未分类排最后。
            int star = e.FindPropertyRelative("star").intValue;
            data.Add(new KeyValuePair<int, int>(ci, star));
        }

        bool sorted = true;
        for (int i = 1; i < n; i++)
        {
            if (CompareEntry(data[i - 1], data[i]) > 0)
            {
                sorted = false;
                break;
            }
        }
        if (sorted)
            return;

        for (int i = 1; i < n; i++)
        {
            int j = i;
            while (j > 0 && CompareEntry(data[j - 1], data[j]) > 0)
            {
                _entries.MoveArrayElement(j, j - 1);
                KeyValuePair<int, int> tmp = data[j];
                data[j] = data[j - 1];
                data[j - 1] = tmp;
                j--;
            }
        }
    }

    private static int CompareEntry(KeyValuePair<int, int> a, KeyValuePair<int, int> b)
    {
        if (a.Key != b.Key)
            return a.Key.CompareTo(b.Key);
        return a.Value.CompareTo(b.Value);
    }

    private void DrawEntry(SerializedProperty e, int index, List<string> categoryChoices, List<string> raceChoices)
    {
        SerializedProperty displayName = e.FindPropertyRelative("displayName");
        string title = displayName != null && !string.IsNullOrEmpty(displayName.stringValue)
            ? displayName.stringValue
            : "条目 " + (index + 1);
        EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);

        Field(e, "id", "稳定 ID（唯一，解锁记录用）");
        Field(e, "displayName", "角色名");

        Popup(e, "categoryId", "所属 label 页面", categoryChoices);
        Popup(e, "raceId", "Race 种族图标（Card 的 back1 下 Race）", raceChoices);

        SerializedProperty avatar = e.FindPropertyRelative("avatarSprite");
        SerializedProperty avatarKey = e.FindPropertyRelative("avatarSpriteKey");
        if (avatar != null && avatarKey != null)
            DrawSpriteWithKey(avatar, avatarKey, "格子头像");

        SerializedProperty role = e.FindPropertyRelative("roleSprite");
        SerializedProperty roleKey = e.FindPropertyRelative("roleSpriteKey");
        if (role != null && roleKey != null)
            DrawSpriteWithKey(role, roleKey, "Role 立绘");

        SerializedProperty star = e.FindPropertyRelative("star");
        if (star != null)
            star.intValue = EditorGUILayout.IntPopup("star 星级（1-7，只亮所选一颗）", star.intValue, StarLabels, StarValues);

        Field(e, "prefabKey", "单位预制体键（遇见/获得判定，可空）");

        EditorGUILayout.LabelField("Card 数值文本", EditorStyles.miniBoldLabel);
        Field(e, "repelText", "Repel 文本");
        Field(e, "quantityText", "Quiantity 文本");
        Field(e, "scopeText", "Scope 文本");
        Field(e, "timeText", "time 文本");
        Field(e, "weightText", "weight 文本");
        Field(e, "coinText", "coin 文本");

        SerializedProperty explain = e.FindPropertyRelative("explainText");
        if (explain != null)
            explain.stringValue = EditorGUILayout.TextArea(explain.stringValue, GUILayout.MinHeight(48));
    }

    /// <summary>绘制拖拽 Sprite 字段，并自动从 SpriteRegistry 反查资源键填充。</summary>
    private void DrawSpriteWithKey(SerializedProperty spriteProp, SerializedProperty keyProp, string label)
    {
        EditorGUILayout.PropertyField(spriteProp, new GUIContent(label + "（拖拽 Sprite，编辑器预览）"));

        if (spriteProp.objectReferenceValue is Sprite dragged && _spriteToKey != null &&
            _spriteToKey.TryGetValue(dragged, out string key))
        {
            if (keyProp.stringValue != key)
                keyProp.stringValue = key;
        }

        EditorGUILayout.PropertyField(keyProp, new GUIContent(label + "资源键（运行时，自动反查）"));
    }

    private void AddEntry(string categoryId, List<string> raceChoices)
    {
        _entries.arraySize++;
        SerializedProperty e = _entries.GetArrayElementAtIndex(_entries.arraySize - 1);

        SerializedProperty cat = e.FindPropertyRelative("categoryId");
        if (cat != null)
            cat.stringValue = categoryId;

        SerializedProperty race = e.FindPropertyRelative("raceId");
        if (race != null && (string.IsNullOrEmpty(race.stringValue)))
        {
            if (!string.IsNullOrEmpty(categoryId) && raceChoices.Contains(categoryId))
                race.stringValue = categoryId;
            else if (raceChoices.Count > 0)
                race.stringValue = raceChoices[0];
        }
    }

    private int CountEntries(string categoryId)
    {
        int count = 0;
        for (int i = 0; i < _entries.arraySize; i++)
        {
            string eCat = _entries.GetArrayElementAtIndex(i).FindPropertyRelative("categoryId").stringValue;
            if (eCat == categoryId)
                count++;
        }
        return count;
    }

    /// <summary>加载 SpriteRegistry 并建立“拖入 Sprite -> 资源键”反查表。</summary>
    private void EnsureRegistryIndex()
    {
        if (_registry != null)
            return;

        _registry = AssetDatabase.LoadAssetAtPath<SpriteRegistry>("Assets/RemoteResource/SpriteRegistry.asset");
        if (_registry == null)
            return;

        _spriteToKey = new Dictionary<Sprite, string>();
        foreach (SpriteMapping mapping in _registry.mappings)
        {
            if (mapping == null || string.IsNullOrEmpty(mapping.key) || mapping.sprite == null)
                continue;
            if (!_spriteToKey.ContainsKey(mapping.sprite))
                _spriteToKey[mapping.sprite] = mapping.key;
        }
    }

    private static List<string> ReadStrings(SerializedProperty listProp)
    {
        var result = new List<string>();
        for (int i = 0; i < listProp.arraySize; i++)
            result.Add(listProp.GetArrayElementAtIndex(i).stringValue);
        return result;
    }

    private static void Field(SerializedProperty parent, string fieldName, string label)
    {
        SerializedProperty prop = parent.FindPropertyRelative(fieldName);
        if (prop != null)
            EditorGUILayout.PropertyField(prop, new GUIContent(label));
    }

    private static void Popup(SerializedProperty parent, string fieldName, string label, List<string> choices)
    {
        SerializedProperty prop = parent.FindPropertyRelative(fieldName);
        if (prop == null || choices.Count == 0)
            return;

        int current = choices.IndexOf(prop.stringValue);
        if (current < 0)
        {
            // 空值或旧值不在选项里：自动落到第一项，避免“下拉显示第一项但实际值为空”导致条目不显示。
            prop.stringValue = choices[0];
            current = 0;
        }

        int selected = EditorGUILayout.Popup(label, current, choices.ToArray());
        prop.stringValue = choices[selected];
    }
}
