using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// When you save a FireworkBlueprint asset, auto-compile it to a CompiledShowAsset next to it.
/// Naming: CS_<BlueprintName>.asset
/// </summary>
public class FireworkBlueprintAutoCompiler : AssetModificationProcessor
{
    static string[] OnWillSaveAssets(string[] paths)
    {
        foreach (var path in paths)
        {
            var bp = AssetDatabase.LoadAssetAtPath<FireworkBlueprint>(path);
            if (bp == null) continue;

            Compile(bp);
        }
        return paths;
    }

    [MenuItem("Hanabi/Compile Selected Blueprint")]
    static void CompileSelected()
    {
        var bp = Selection.activeObject as FireworkBlueprint;
        if (bp == null)
        {
            Debug.LogWarning("[Hanabi] Select a FireworkBlueprint asset first.");
            return;
        }
        Compile(bp);
    }

    static void Compile(FireworkBlueprint bp)
    {
        string bpPath = AssetDatabase.GetAssetPath(bp);
        if (string.IsNullOrEmpty(bpPath)) return;

        string folder = Path.GetDirectoryName(bpPath).Replace("\\", "/");
        string csPath = $"{folder}/CS_{bp.name}.asset";

        var cs = AssetDatabase.LoadAssetAtPath<CompiledShowAsset>(csPath);
        if (cs == null)
        {
            cs = UnityEngine.ScriptableObject.CreateInstance<CompiledShowAsset>();
            cs.version = 1;
            AssetDatabase.CreateAsset(cs, csPath);
        }

        var db = HanabiDatabaseEditorUtil.GetOrCreateDefault();
        HanabiCompiler_MVP.Compile(bp, db, out uint seed, out BurstEvent[] bursts, out ParticleInitV2[] inits);
        cs.blob = CompiledShowSerializer.Write(seed, bursts, inits, version: 1);

        EditorUtility.SetDirty(cs);
        

        Debug.Log($"[Hanabi] Compiled {bp.name} -> {Path.GetFileName(csPath)}  bursts={bursts.Length} particles={inits.Length} blob={cs.blob?.Length ?? 0}");
    }
}
