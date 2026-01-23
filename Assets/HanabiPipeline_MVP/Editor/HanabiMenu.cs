using UnityEditor;
using UnityEngine;

public static class HanabiMenu
{
    [MenuItem("Hanabi/Compile Selected Blueprint")]
    public static void CompileSelected()
    {
        var bp = Selection.activeObject as FireworkBlueprint;
        if (bp == null)
        {
            Debug.LogWarning("[Hanabi] Select a FireworkBlueprint asset first.");
            return;
        }

        var db = HanabiDatabaseEditorUtil.GetOrCreateDefault();

        string bpPath = AssetDatabase.GetAssetPath(bp);
        string dir = System.IO.Path.GetDirectoryName(bpPath);
        string csPath = System.IO.Path.Combine(dir, $"CS_{bp.name}.asset").Replace("\\","/");

        var cs = AssetDatabase.LoadAssetAtPath<CompiledShowAsset>(csPath);
        if (cs == null)
        {
            cs = ScriptableObject.CreateInstance<CompiledShowAsset>();
            cs.version = 2;
            AssetDatabase.CreateAsset(cs, csPath);
        }

        HanabiCompiler_MVP.Compile(bp, db, out uint seed, out BurstEvent[] bursts, out ParticleInitV2[] inits, out LaunchParams launchParams);
        cs.blob = CompiledShowSerializer.Write(seed, bursts, inits, launchParams, version: 2);
        cs.version = 2;

        EditorUtility.SetDirty(cs);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Hanabi] Compiled {bp.name} -> {csPath}  bursts={bursts.Length} inits={inits.Length}");
    }
}
