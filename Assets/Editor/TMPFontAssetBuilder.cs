using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Match3.EditorTools
{
    /// <summary>
    /// UIFactory.KoreanFont가 쓰는 나눔고딕 TTF로부터 TextMeshPro용 SDF 폰트 애셋을 만든다.
    /// 한글은 완성형 음절만 11,172자라 Static Atlas로 미리 다 구워두면 텍스처가
    /// 지나치게 커지므로, Dynamic Atlas Population Mode로 만들어서 실제 화면에
    /// 나온 글자만 그때그때 아틀라스에 채워 넣는다 - 대신 소스 TTF importer의
    /// "Include Font Data"가 켜져 있어야 런타임에 새 글리프를 렌더링할 수 있다
    /// (NanumGothic-Regular.ttf.meta에 이미 includeFontData: 1로 켜져 있음).
    ///
    /// 에디터가 이미 이 프로젝트를 열고 있지 않을 때는
    /// -executeMethod Match3.EditorTools.TMPFontAssetBuilder.GenerateNanumGothicFontAsset
    /// 로 배치 모드에서 호출할 수도 있다 (WebGLBuildScript.cs 참고).
    /// </summary>
    public static class TMPFontAssetBuilder
    {
        private const string SourceFontPath = "Assets/Resources/Fonts/NanumGothic-Regular.ttf";
        private const string OutputFolder = "Assets/Resources/Fonts";
        private const string OutputAssetName = "NanumGothic-Regular SDF";

        [MenuItem("Bomoonsan/Generate NanumGothic TMP Font Asset")]
        public static void GenerateNanumGothicFontAsset()
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                Debug.LogError($"[TMPFontAssetBuilder] 소스 폰트를 찾을 수 없음: {SourceFontPath}");
                return;
            }

            string assetPath = $"{OutputFolder}/{OutputAssetName}.asset";

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                samplingPointSize: 90,
                atlasPadding: 9,
                renderMode: GlyphRenderMode.SDFAA,
                atlasWidth: 1024,
                atlasHeight: 1024,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            if (fontAsset == null)
            {
                Debug.LogError("[TMPFontAssetBuilder] TMP_FontAsset 생성 실패 - TTF Import Settings의 Include Font Data를 확인할 것.");
                return;
            }

            fontAsset.name = OutputAssetName;

            // 이미 만들어둔 애셋이 있으면 지우고 다시 만든다 - CreateAsset은 같은
            // 경로에 애셋이 이미 있으면 실패한다.
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath);

            AssetDatabase.CreateAsset(fontAsset, assetPath);

            fontAsset.atlasTextures[0].name = OutputAssetName + " Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);

            fontAsset.material.name = OutputAssetName + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TMPFontAssetBuilder] TMP 폰트 애셋 생성 완료: {assetPath}");
        }
    }
}
