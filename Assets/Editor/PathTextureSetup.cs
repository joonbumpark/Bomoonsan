using UnityEditor;

namespace Mountains
{
    public static class PathTextureSetup
    {
        //[MenuItem("Mountains/Assign Path Textures")]
        public static void AssignPathTextures()
        {
            TerrainTextureAssign.AssignLayer("PathTextureSetup",
                "Assets/Textures/PathAlbedo.png", "Assets/Textures/PathNormal.png",
                "_PathAlbedo", "_PathNormal");
        }
    }
}
