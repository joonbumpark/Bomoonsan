using UnityEditor;

namespace Mountains
{
    public static class CliffTextureSetup
    {
        [MenuItem("Mountains/Assign Cliff Textures")]
        public static void AssignCliffTextures()
        {
            TerrainTextureAssign.AssignLayer("CliffTextureSetup",
                "Assets/Textures/CliffAlbedo.png", "Assets/Textures/CliffNormal.png",
                "_CliffAlbedo", "_CliffNormal");
        }
    }
}
