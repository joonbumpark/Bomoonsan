using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// InGameScene을 WebGL로 빌드한다. 에디터 메뉴에서 직접 실행할 수도 있고,
    /// (에디터가 이미 이 프로젝트를 열고 있지 않을 때) 배치 모드에서
    /// -executeMethod Match3.EditorTools.WebGLBuildScript.Build 로 호출할 수도 있다.
    /// 배포 자동화는 Tools/deploy_webgl.sh 참고.
    /// </summary>
    public static class WebGLBuildScript
    {
        // 프로젝트 루트 기준 출력 경로 (.gitignore의 /[Bb]uilds/ 규칙에 걸려 커밋되지 않는다).
        private const string OutputDir = "Builds/WebGL";

        private static readonly string[] Scenes =
        {
            "Assets/Scenes/InGameScene.unity",
        };

        [MenuItem("Bomoonsan/Build WebGL")]
        public static void BuildFromMenu()
        {
            Build();
        }

        public static void Build()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outputPath = Path.Combine(projectRoot, OutputDir);

            if (Directory.Exists(outputPath))
                Directory.Delete(outputPath, true);
            Directory.CreateDirectory(outputPath);

            // 일반 정적 웹 서버(시놀로지 Web Station 등)에 특별한 헤더 설정 없이
            // 바로 올려서 돌아가도록 압축을 끈다. 나중에 서버에 Brotli/Gzip 헤더를
            // 설정할 수 있으면 WebGLCompressionFormat.Brotli 등으로 바꿔도 된다.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;

            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            bool success = report.summary.result == BuildResult.Succeeded;

            if (success)
                Debug.Log($"[WebGLBuildScript] 빌드 성공: {outputPath}");
            else
                Debug.LogError($"[WebGLBuildScript] 빌드 실패: {report.summary.result} (에러 {report.summary.totalErrors}개)");

            if (Application.isBatchMode)
                EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
