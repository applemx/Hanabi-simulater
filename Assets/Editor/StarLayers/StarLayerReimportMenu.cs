using UnityEditor;

public static class StarLayerReimportMenu
{
    [MenuItem("Hanabi/Reimport/Star Layers Data + Prefabs")]
    public static void ReimportStarLayers()
    {
        AssetDatabase.ImportAsset("Assets/Data/StarLayers", ImportAssetOptions.ImportRecursive);
        AssetDatabase.ImportAsset("Assets/Prefabs/StarLayers", ImportAssetOptions.ImportRecursive);
        AssetDatabase.Refresh();
    }
}
