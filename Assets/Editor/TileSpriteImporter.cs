using UnityEditor;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Assets/Resources/Sprites/Tiles 아래에 넣는 텍스처는 항상 UI 스프라이트로 임포트되게
    /// 강제한다. 매번 인스펙터에서 Texture Type을 손으로 Sprite로 바꿀 필요 없이,
    /// 파일을 넣기만 하면 TileArt.cs가 Resources.Load<Sprite>로 바로 쓸 수 있게 한다.
    /// </summary>
    public class TileSpriteImporter : AssetPostprocessor
    {
        private const string TargetFolder = "Assets/Resources/Sprites/Tiles";

        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(TargetFolder))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Compressed;
            // 실제 표시 크기(타일 셀 ~110px)에 비해 원본이 1024px라 그대로 두면 빌드 용량만 커진다.
            importer.maxTextureSize = 256;
        }
    }
}
