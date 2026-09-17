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
    ///
    /// maxTextureSize는 폴더별로 다르게 준다 - Tiles/Tetris는 실제 표시 크기(셀
    /// ~110px)에 비해 원본이 훨씬 커서 작게 눌러도 티가 안 나지만, UI 밑의 팝업
    /// 배경/버튼 이미지(수백~2000px대)를 똑같이 256으로 누르면 원본의 1/5~1/10
    /// 크기로 뭉개져 보인다. 새 UI 이미지를 넣을 땐 이 else 분기(UiMaxTextureSize)에
    /// 걸리는지, 필요하면 UiMaxTextureSize를 올려야 하는지 확인할 것.
    /// </summary>
    public class TileSpriteImporter : AssetPostprocessor
    {
        private const string TargetFolder = "Assets/Resources/Sprites";
        private const int UiMaxTextureSize = 2048;
        private const int TileMaxTextureSize = 256;

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
            else if (path.Contains("/Jigsaw/"))
            {
                // 직소 퍼즐 사진은 완성 미리보기로 크게 보이기도 하고 16조각으로 잘게
                // 잘리기도 해서, 다른 타일류(256px)보다 훨씬 높은 해상도가 필요하다.
                importer.maxTextureSize = 1024;
            }
            else if (path.Contains("/Tiles/") || path.Contains("/Tetris/"))
            {
                // 타일류: 실제 표시 크기(셀 ~110px)에 비해 원본이 커서 그대로 두면
                // 빌드 용량만 커진다.
                importer.maxTextureSize = TileMaxTextureSize;
            }
            else
            {
                // 나머지(주로 Sprites/UI) - 팝업 배경/버튼처럼 화면에 크게 보이는
                // 이미지라 원본 해상도를 최대한 살려둔다.
                importer.maxTextureSize = UiMaxTextureSize;
            }
        }

        /// <summary>
        /// OnPreprocessTexture()는 텍스처를 "새로 임포트할 때"만 실행되므로, 이미
        /// 임포트되어 .meta에 낮은 maxTextureSize가 박혀있는 기존 이미지는 이 파일의
        /// 규칙을 고쳐도 저절로 다시 적용되지 않는다. 그런 기존 이미지들에 새 규칙을
        /// 소급 적용하고 싶을 때 이 메뉴로 강제 재임포트한다 (.meta의 guid는 그대로라
        /// 참조는 안 깨진다).
        /// </summary>
        [MenuItem("Bomoonsan/Reimport Sprites With Current Rules")]
        public static void ReimportAllSprites()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { TargetFolder });
            int count = 0;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                count++;
            }

            Debug.Log($"[TileSpriteImporter] {TargetFolder} 밑 텍스처 {count}개 재임포트 완료.");
        }
    }
}
