using UnityEngine;

[CreateAssetMenu(menuName = "Hanabi/CompiledShow", fileName = "CS_CompiledShow")]
public class CompiledShowAsset : ScriptableObject
{
    [Header("Build Metadata")]
    public int version = 1;
    public string sourceGuid;     // Blueprint GUID used to compile
    public string sourceHash;     // content hash (optional)

    [Header("Binary Data")]
    public byte[] blob;
}
