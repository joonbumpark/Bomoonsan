using UnityEditor;

namespace Mountains
{
    public static class GrassTextureSetup
    {
        [MenuItem("Mountains/Assign Grass Textures")]
        public static void AssignGrassTextures()
        {
            TerrainTextureAssign.AssignLayer("GrassTextureSetup",
                "Assets/Textures/GrassAlbedo.png", "Assets/Textures/GrassNormal.png",
                "_FlatAlbedo", "_FlatNormal");
        }
    }
}
