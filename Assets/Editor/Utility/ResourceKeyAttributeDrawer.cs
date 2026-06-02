using System;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ResourceKeyAttribute))]
public class ResourceKeyAttributeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // 只有 String 类型的字段才应用此绘制逻辑
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        ResourceKeyAttribute attr = attribute as ResourceKeyAttribute;
        Type resourceType = attr != null ? attr.ResourceType : typeof(UnityEngine.Object);

        // 将一行分为左右两块：左侧照常显示输入框，右侧提供拖拽感应区
        float dropAreaWidth = 60f;
        Rect textFieldRect = new Rect(position.x, position.y, position.width - dropAreaWidth - 5f, position.height);
        Rect dropRect = new Rect(position.x + position.width - dropAreaWidth, position.y, dropAreaWidth, position.height);

        // 绘制普通的文本框
        property.stringValue = EditorGUI.TextField(textFieldRect, label, property.stringValue);

        // 绘制拖拽提示框
        GUIStyle dropStyle = new GUIStyle(EditorStyles.helpBox);
        dropStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Box(dropRect, "拖入对象", dropStyle);

        // 处理拖拽事件
        Event evt = Event.current;
        if (dropRect.Contains(evt.mousePosition) || textFieldRect.Contains(evt.mousePosition))
        {
            if (evt.type == EventType.DragUpdated)
            {
                bool canDrop = false;
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    // 验证拖拽物体的类型是否符合 Attribute 的声明要求
                    if (resourceType.IsAssignableFrom(obj.GetType()))
                    {
                        canDrop = true;
                        break;
                    }
                }

                if (canDrop)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    evt.Use();
                }
                else
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                }
            }
            else if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (resourceType.IsAssignableFrom(obj.GetType()))
                    {
                        // 提取名字赋给字符串字段
                        property.stringValue = obj.name;
                        property.serializedObject.ApplyModifiedProperties();
                        break;
                    }
                }
                evt.Use();
            }
        }
    }
}
