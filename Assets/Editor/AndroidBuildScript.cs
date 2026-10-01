using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Build Settings에 켜져 있는 씬 전체를 Android APK로 빌드한다. 에디터 메뉴에서 직접 실행할 수도 있고,
    /// (에디터가 이미 이 프로젝트를 열고 있지 않을 때) 배치 모드에서
    /// -buildTarget Android -executeMethod Match3.EditorTools.AndroidBuildScript.Build 로 호출할 수도 있다.
    /// 키스토어를 지정하지 않았으면 디버그 키로 서명된다 - 사내 테스트 설치용.
    /// </summary>
    public static class AndroidBuildScript
    {
        // 프로젝트 루트 기준 출력 경로 (.gitignore의 /[Bb]uilds/ 규칙에 걸려 커밋되지 않는다).
        private const string OutputDir = "Builds/Android";

        [MenuItem("Bomoonsan/Build Android APK")]
        public static void BuildFromMenu()
        {
            Build();
        }

        public static void Build()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outputDir = Path.Combine(projectRoot, OutputDir);

            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
            Directory.CreateDirectory(outputDir);

            EditorUserBuildSettings.buildAppBundle = false;

            string apkPath = Path.Combine(outputDir, $"Bomoonsan_{PlayerSettings.bundleVersion}.apk");

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes
                    .Where(s => s.enabled)
                    .Select(s => s.path)
                    .ToArray(),
                locationPathName = apkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            bool success = report.summary.result == BuildResult.Succeeded;

            if (success)
                Debug.Log($"[AndroidBuildScript] 빌드 성공: {apkPath}");
            else
                Debug.LogError($"[AndroidBuildScript] 빌드 실패: {report.summary.result} (에러 {report.summary.totalErrors}개)");

            if (Application.isBatchMode)
                EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
