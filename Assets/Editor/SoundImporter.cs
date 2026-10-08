using UnityEditor;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Assets/Resources/Sounds 아래 소리 파일의 임포트 설정을 자동으로 맞춘다.
    ///   Bgm/ - 2분 안팎 곡이라 메모리에 통째로 풀지 않고 재생하면서 읽고(Streaming), 압축
    ///          품질을 낮춰 빌드 용량을 줄인다 (배경음악은 효과음보다 음질 손실이 덜 들린다).
    ///   Sfx/ - 짧은 효과음은 바로 재생돼야 하니 로드할 때 미리 풀어둔다(DecompressOnLoad).
    /// </summary>
    public class SoundImporter : AssetPostprocessor
    {
        private const string Root = "Assets/Resources/Sounds/";

        private void OnPreprocessAudio()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(Root))
                return;

            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;

            if (path.StartsWith(Root + "Bgm/"))
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.quality = 0.4f;
            }
            else
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.quality = 0.7f;
            }

            importer.defaultSampleSettings = settings;
        }
    }
}
