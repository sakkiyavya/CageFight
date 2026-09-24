using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// EnemyTeamConfig 自定义绘制：Race Id 做成下拉选择。
/// 选项由项目里全部 RaceDefinition 资产自动收集——新增种族后自动出现在下拉中，无需改代码；
/// 未选择或原值已失效时自动默认第一个种族。其余字段保持默认绘制。
/// </summary>
[CustomPropertyDrawer(typeof(EnemyTeamConfig))]
public sealed class EnemyTeamConfigDrawer : PropertyDrawer
{
    private static readonly List<string> RaceIds = new List<string>();
    private static readonly List<string> RaceLabels = new List<string>();
    private static bool _cacheDirty = true;

    [InitializeOnLoadMethod]
    private static void HookProjectChanged()
    {
        EditorApplication.projectChanged += () => _cacheDirty = true;
    }

    /// <summary>重新收集项目里的全部种族资产（ID + 显示名）。</summary>
    private static void RefreshRaces()
    {
        _cacheDirty = false;
        RaceIds.Clear();
        RaceLabels.Clear();

        string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            RaceDefinition race = AssetDatabase.LoadAssetAtPath<RaceDefinition>(path);
            if (race == null || string.IsNullOrEmpty(race.Id))
                continue;

            RaceIds.Add(race.Id);
            RaceLabels.Add($"{race.DisplayName} ({race.Id})");
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        SerializedProperty end = property.GetEndProperty();
        SerializedProperty child = property.Copy();
        if (child.NextVisible(true))
        {
            do
            {
                if (SerializedProperty.EqualContents(child, end))
                    break;
                if (child.name == "teamId" || child.name == "raceId")
                    continue;

                height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
            } while (child.NextVisible(false));
        }

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty teamId = property.FindPropertyRelative("teamId");
        SerializedProperty raceId = property.FindPropertyRelative("raceId");

        // 第一行：队伍编号 + 种族下拉。
        Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        Rect teamRect = new Rect(line.x, line.y, line.width * 0.3f, line.height);
        Rect raceRect = new Rect(line.x + line.width * 0.32f, line.y, line.width * 0.68f, line.height);

        EditorGUI.PropertyField(teamRect, teamId, GUIContent.none);
        DrawRacePopup(raceRect, raceId);

        // 其余字段默认绘制。
        float y = line.yMax + EditorGUIUtility.standardVerticalSpacing;
        SerializedProperty end = property.GetEndProperty();
        SerializedProperty child = property.Copy();
        if (child.NextVisible(true))
        {
            do
            {
                if (SerializedProperty.EqualContents(child, end))
                    break;
                if (child.name == "teamId" || child.name == "raceId")
                    continue;

                float h = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, h), child, true);
                y += h + EditorGUIUtility.standardVerticalSpacing;
            } while (child.NextVisible(false));
        }

        EditorGUI.EndProperty();
    }

    /// <summary>种族下拉：选项自动来自项目里的 RaceDefinition 资产；空值自动默认第一个。</summary>
    private static void DrawRacePopup(Rect rect, SerializedProperty raceId)
    {
        if (_cacheDirty)
            RefreshRaces();

        if (RaceIds.Count == 0)
        {
            EditorGUI.PropertyField(rect, raceId, new GUIContent("Race Id"));
            return;
        }

        int index = RaceIds.IndexOf(raceId.stringValue);
        if (index < 0)
        {
            index = 0;
            raceId.stringValue = RaceIds[0];
        }

        int newIndex = EditorGUI.Popup(rect, "Race Id", index, RaceLabels.ToArray());
        if (newIndex != index && newIndex >= 0 && newIndex < RaceIds.Count)
            raceId.stringValue = RaceIds[newIndex];
    }
}
