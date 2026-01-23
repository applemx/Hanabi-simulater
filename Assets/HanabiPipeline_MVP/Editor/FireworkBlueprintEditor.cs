using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FireworkBlueprint))]
public class FireworkBlueprintEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(iterator, true);
                continue;
            }

            switch (iterator.propertyPath)
            {
                case "starProfileTag":
                    TagPopupDrawer.DrawTagPopup(iterator, "Star Profile Tag", TagOptionsCache.GetStarTags(iterator.stringValue));
                    break;
                case "paletteTag":
                    TagPopupDrawer.DrawTagPopup(iterator, "Palette Tag", TagOptionsCache.GetPaletteTags(iterator.stringValue));
                    break;
                case "waruyakuTag":
                    TagPopupDrawer.DrawTagPopup(iterator, "Waruyaku Tag", TagOptionsCache.GetWaruyakuTags(iterator.stringValue));
                    break;
                case "washiTag":
                    TagPopupDrawer.DrawTagPopup(iterator, "Washi Tag", TagOptionsCache.GetWashiTags(iterator.stringValue));
                    break;
                default:
                    EditorGUILayout.PropertyField(iterator, true);
                    break;
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}

[CustomPropertyDrawer(typeof(IgniterSpec))]
public class IgniterSpecDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float line = EditorGUIUtility.singleLineHeight;
        float gap = EditorGUIUtility.standardVerticalSpacing;

        var posProp = property.FindPropertyRelative("posLocal");
        var fuseProp = property.FindPropertyRelative("fuseTag");
        var delayProp = property.FindPropertyRelative("startDelay");

        var rect = new Rect(position.x, position.y, position.width, line);
        EditorGUI.PropertyField(rect, posProp);

        rect.y += line + gap;
        string fuseTag = SafeString(fuseProp);
        TagPopupDrawer.DrawTagPopup(rect, fuseProp, "Fuse Tag", TagOptionsCache.GetFuseTags(fuseTag));

        rect.y += line + gap;
        EditorGUI.PropertyField(rect, delayProp);

        bool custom = TagPopupDrawer.IsCustomTag(fuseTag, TagOptionsCache.GetFuseTags(fuseTag));
        if (custom)
        {
            rect.y += line + gap;
            EditorGUI.indentLevel++;
            fuseProp.stringValue = EditorGUI.TextField(rect, "Custom Tag", fuseProp.stringValue);
            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float gap = EditorGUIUtility.standardVerticalSpacing;

        var fuseProp = property.FindPropertyRelative("fuseTag");
        string fuseTag = SafeString(fuseProp);
        bool custom = TagPopupDrawer.IsCustomTag(fuseTag, TagOptionsCache.GetFuseTags(fuseTag));

        int lines = custom ? 4 : 3;
        return lines * line + (lines - 1) * gap;
    }

    static string SafeString(SerializedProperty prop)
    {
        if (prop == null) return string.Empty;
        try
        {
            return prop.stringValue ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}

static class TagOptionsCache
{
    public static List<string> GetStarTags(string current) => CollectTags(GetDb()?.starProfiles, current, def => def.tag);
    public static List<string> GetPaletteTags(string current) => CollectTags(GetDb()?.palettes, current, def => def.tag);
    public static List<string> GetWaruyakuTags(string current) => CollectTags(GetDb()?.waruyakuDefs, current, def => def.tag);
    public static List<string> GetWashiTags(string current) => CollectTags(GetDb()?.washiDefs, current, def => def.tag);
    public static List<string> GetFuseTags(string current) => CollectTags(GetDb()?.fuseDefs, current, def => def.tag);

    static HanabiDatabase GetDb()
    {
        // Avoid mutating assets while drawing inspectors.
        string[] guids = AssetDatabase.FindAssets("t:HanabiDatabase");
        if (guids != null && guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var db = AssetDatabase.LoadAssetAtPath<HanabiDatabase>(path);
            if (db != null) return db;
        }

        return AssetDatabase.LoadAssetAtPath<HanabiDatabase>(HanabiDatabaseEditorUtil.DefaultPath);
    }

    static List<string> CollectTags<T>(IList<T> defs, string current, Func<T, string> getTag)
    {
        var tags = new List<string>(8);
        if (defs != null)
        {
            for (int i = 0; i < defs.Count; i++)
            {
                var tag = getTag(defs[i]);
                if (string.IsNullOrWhiteSpace(tag)) continue;
                if (!tags.Contains(tag)) tags.Add(tag);
            }
        }

        if (!string.IsNullOrWhiteSpace(current) && !tags.Contains(current))
            tags.Add(current);

        return tags;
    }
}

static class TagPopupDrawer
{
    const string CustomLabel = "<Custom>";

    public static void DrawTagPopup(SerializedProperty prop, string label, List<string> tags)
    {
        int selected = GetSelectedIndex(prop.stringValue, tags, out var options);
        int next = EditorGUILayout.Popup(label, selected, options);

        if (next >= 0 && next < options.Length - 1)
        {
            prop.stringValue = options[next];
        }
        else if (IsCustomIndex(options, next))
        {
            prop.stringValue = EditorGUILayout.TextField("Custom Tag", prop.stringValue);
        }
    }

    public static void DrawTagPopup(Rect rect, SerializedProperty prop, string label, List<string> tags)
    {
        int selected = GetSelectedIndex(prop.stringValue, tags, out var options);
        int next = EditorGUI.Popup(rect, label, selected, options);

        if (next >= 0 && next < options.Length - 1)
        {
            prop.stringValue = options[next];
        }
    }

    public static bool IsCustomTag(string current, List<string> tags)
    {
        GetSelectedIndex(current, tags, out var options);
        return current == null || Array.IndexOf(options, current) < 0;
    }

    static int GetSelectedIndex(string current, List<string> tags, out string[] options)
    {
        var list = new List<string>(tags ?? new List<string>());
        if (list.Count == 0) list.Add(CustomLabel);
        if (!list.Contains(CustomLabel)) list.Add(CustomLabel);

        options = list.ToArray();
        int idx = Array.IndexOf(options, current);
        if (idx < 0) idx = options.Length - 1;
        return idx;
    }

    static bool IsCustomIndex(string[] options, int index)
    {
        return index == options.Length - 1;
    }
}
