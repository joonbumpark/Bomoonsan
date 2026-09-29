using UnityEditor;
using UnityEngine;

namespace Mountains
{
    // Gemini로 생성한 "GAME READY TEXTURE MAPS" 시트에서 잘라낸 Albedo/Normal 텍스처를
    // 올바른 임포트 설정으로 맞추고 TerrainMountain 머티리얼에 연결하는 공용 로직.
    // Cliff/Grass 등 레이어별 메뉴 항목(CliffTextureSetup, GrassTextureSetup)이
    // 이 헬퍼를 각자의 경로/프로퍼티 이름으로 얇게 감싸서 재사용한다.
    public static class TerrainTextureAssign
    {
        const string MaterialPath = "Assets/Materials/TerrainMountain.mat";

        public static void AssignLayer(string logTag, string albedoPath, string normalPath,
            string albedoProperty, string normalProperty)
        {
            SetupImport(albedoPath, logTag, TextureImporterType.Default);
            SetupImport(normalPath, logTag, TextureImporterType.NormalMap);

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Debug.LogWarning($"[{logTag}] {MaterialPath} 를 찾을 수 없습니다. 먼저 Mountains > Create Procedural Terrain을 실행하세요.");
                return;
            }

            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

            if (material.HasProperty(albedoProperty) && albedo != null)
            {
                material.SetTexture(albedoProperty, albedo);
            }
            if (material.HasProperty(normalProperty) && normal != null)
            {
                material.SetTexture(normalProperty, normal);
            }

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            Debug.Log($"[{logTag}] Albedo/Normal 텍스처를 TerrainMountain 머티리얼의 {albedoProperty}/{normalProperty}에 연결했습니다.");
        }

        static void SetupImport(string path, string logTag, TextureImporterType type)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[{logTag}] {path} 를 찾을 수 없습니다.");
                return;
            }

            importer.textureType = type;
            importer.sRGBTexture = type == TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }
}
