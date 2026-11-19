using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Build Settings에 켜져 있는 씬 전체를 Windows 실행 파일(.exe)로 빌드한다. 세로(9:16) 창 모드로 고정한다.
    /// 에디터 메뉴에서 직접 실행할 수도 있고, (에디터가 이 프로젝트를 열고 있지 않을 때) 배치 모드에서
    /// -buildTarget Win64 -executeMethod Match3.EditorTools.WindowsBuildScript.Build 로 호출할 수도 있다.
    /// 실행 시 창 크기는 PortraitWindow(런타임)가 모니터 높이에 맞춰 다시 잡는다.
    /// </summary>
    public static class WindowsBuildScript
    {
        // 프로젝트 루트 기준 출력 경로 (.gitignore의 /[Bb]uilds/ 규칙에 걸려 커밋되지 않는다).
        private const string OutputDir = "Builds/Windows";

        [MenuItem("Bomoonsan/Build Windows EXE (Portrait)")]
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

            // 세로 창 고정 — Standalone 전용 설정이라 Android/WebGL 설정에는 영향이 없다.
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.allowFullscreenSwitch = false;
            PlayerSettings.runInBackground = true;

            string exePath = Path.Combine(outputDir, "Bomoonsan.exe");

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes
                    .Where(s => s.enabled)
                    .Select(s => s.path)
                    .ToArray(),
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            bool success = report.summary.result == BuildResult.Succeeded;

            if (success)
                Debug.Log($"[WindowsBuildScript] 빌드 성공: {exePath}");
            else
                Debug.LogError($"[WindowsBuildScript] 빌드 실패: {report.summary.result} (에러 {report.summary.totalErrors}개)");

            if (Application.isBatchMode)
                EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
