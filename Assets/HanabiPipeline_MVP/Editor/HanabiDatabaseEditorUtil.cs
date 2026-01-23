using System.IO;
using UnityEditor;
using UnityEngine;

public static class HanabiDatabaseEditorUtil
{
    public const string DefaultPath = "Assets/Data/HanabiDatabase_Default.asset";

    public static HanabiDatabase GetOrCreateDefault()
    {
        // Try find any existing
        string[] guids = AssetDatabase.FindAssets("t:HanabiDatabase");
        if (guids != null && guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var db = AssetDatabase.LoadAssetAtPath<HanabiDatabase>(path);
            if (db != null)
            {
                db.EnsureDefaultsIfEmpty();
                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssetIfDirty(db);
                return db;
            }
        }

        // Create at default path
        var existing = AssetDatabase.LoadAssetAtPath<HanabiDatabase>(DefaultPath);
        if (existing != null)
        {
            existing.EnsureDefaultsIfEmpty();
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssetIfDirty(existing);
            return existing;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(DefaultPath));
        var created = ScriptableObject.CreateInstance<HanabiDatabase>();
        created.EnsureDefaultsIfEmpty();
        AssetDatabase.CreateAsset(created, DefaultPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[HanabiDB] Created default DB at {DefaultPath}");
        return created;
    }
}
