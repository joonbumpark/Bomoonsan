using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// 프로젝트 폴더에 요청 파일(Builds/build_request.txt)을 두면, 에디터가 스크립트를 다시 읽거나
    /// 열릴 때 그 안에 적힌 빌드를 차례로 실행하고 결과를 Builds/build_result.txt에 남긴다.
    /// 에디터 화면을 직접 조작하지 않고 파일만으로 빌드를 요청하기 위한 장치다.
    ///
    /// 요청 파일 형식: 한 줄에 하나씩 android / webgl / windows (순서대로 실행). 요청 파일은 빌드를 시작하기
    /// 전에 지운다 — 빌드 중 플랫폼 전환으로 스크립트가 다시 로드돼도 같은 빌드를 반복하지 않는다.
    /// </summary>
    [InitializeOnLoad]
    public static class BuildRequestWatcher
    {
        const string RequestPath = "Builds/build_request.txt";
        const string ResultPath = "Builds/build_result.txt";

        static BuildRequestWatcher()
        {
            // 에디터가 완전히 준비된 뒤(임포트/컴파일이 끝난 뒤)에 확인한다.
            EditorApplication.delayCall += CheckRequest;
        }

        [MenuItem("Bomoonsan/Run Pending Build Request")]
        static void CheckRequest()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string requestFile = Path.Combine(projectRoot, RequestPath);
            if (!File.Exists(requestFile))
            {
                return;
            }

            string[] targets = File.ReadAllLines(requestFile)
                .Select(line => line.Trim().ToLowerInvariant())
                .Where(line => line.Length > 0 && !line.StartsWith("#"))
                .ToArray();
            File.Delete(requestFile);

            var log = new StringBuilder();
            log.AppendLine($"요청 시각: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            foreach (string target in targets)
            {
                var started = DateTime.Now;
                string outcome;
                try
                {
                    switch (target)
                    {
                        case "android":
                            AndroidBuildScript.Build();
                            outcome = ReadLastResult("Builds/Android", "*.apk");
                            break;
                        case "webgl":
                            WebGLBuildScript.Build();
                            outcome = ReadLastResult("Builds/WebGL", "index.html");
                            break;
                        case "windows":
                            WindowsBuildScript.Build();
                            outcome = ReadLastResult("Builds/Windows", "*.exe");
                            break;
                        default:
                            outcome = "알 수 없는 대상 — 건너뜀";
                            break;
                    }
                }
                catch (Exception exception)
                {
                    outcome = "예외: " + exception.Message;
                    Debug.LogException(exception);
                }

                log.AppendLine($"[{target}] {outcome} ({(DateTime.Now - started).TotalSeconds:0}초)");
            }

            Directory.CreateDirectory(Path.Combine(projectRoot, "Builds"));
            File.WriteAllText(Path.Combine(projectRoot, ResultPath), log.ToString(), Encoding.UTF8);
            Debug.Log("[BuildRequestWatcher] 빌드 요청 처리 완료\n" + log);
        }

        // 빌드 스크립트는 결과를 Debug.Log로만 남기므로, 출력물이 생겼는지로 성공 여부를 판단한다.
        static string ReadLastResult(string folder, string pattern)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string directory = Path.Combine(projectRoot, folder);
            if (!Directory.Exists(directory))
            {
                return "실패 — 출력 폴더 없음";
            }

            var file = Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly).FirstOrDefault();
            return file != null
                ? $"성공 — {Path.GetFileName(file)} ({new FileInfo(file).Length / 1024 / 1024}MB)"
                : "실패 — 출력물 없음 (Console/Editor.log 확인)";
        }
    }
}
