using UnityEditor;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Assets/Resources/Sprites 아래에 넣는 텍스처는 항상 UI 스프라이트로 임포트되게
    /// 강제한다. 매번 인스펙터에서 Texture Type을 손으로 Sprite로 바꿀 필요 없이,
    /// 파일을 넣기만 하면 TileArt.cs/UIArt.cs가 Resources.Load&lt;Sprite&gt;로 바로 쓸 수 있게 한다.
    ///
    /// Sprites/UI/popup_panel처럼 모서리 장식이 있는 프레임 이미지는 9-slice(Border)를
    /// 설정해서, Image 컴포넌트를 Sliced 타입으로 쓸 때 모서리 장식이 늘어나지 않고
    /// 가운데 영역만 늘어나게 한다.
    /// </summary>
    public class TileSpriteImporter : AssetPostprocessor
    {
        private const string TargetFolder = "Assets/Resources/Sprites";

        private void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(TargetFolder))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Compressed;

            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (fileName == "popup_panel")
            {
                // 1536x1024 원본. 보더(L+R, T+B)는 이 스프라이트를 쓰는 가장 작은 카드
                // 크기(매칭 팝업 800x500)보다 작아야 한다 - 안 그러면 9-slice 코너끼리
                // 겹치면서 가운데 스트레치 영역이 찌그러지거나 코너 안쪽의 배경 잔여
                // 픽셀이 그대로 늘어나 보이는 문제가 생긴다.
                importer.spriteBorder = new Vector4(180, 140, 180, 140); // L, B, R, T
                importer.maxTextureSize = 1024;
            }
            else if (fileName == "button")
            {
                // 버튼은 9-slice 없이 Simple로 두고, 실제 사용처에서 preserveAspect 없이
                // 버튼 크기에 맞춰 그대로 채워 쓴다.
                importer.maxTextureSize = 512;
            }
            else if (path.Contains("/Jigsaw/"))
            {
                // 직소 퍼즐 사진은 완성 미리보기로 크게 보이기도 하고 16조각으로 잘게
                // 잘리기도 해서, 다른 타일류(256px)보다 훨씬 높은 해상도가 필요하다.
                importer.maxTextureSize = 1024;
            }
            else
            {
                // 타일류: 실제 표시 크기(셀 ~110px)에 비해 원본이 커서 그대로 두면
                // 빌드 용량만 커진다.
                importer.maxTextureSize = 256;
            }
        }
    }
}
