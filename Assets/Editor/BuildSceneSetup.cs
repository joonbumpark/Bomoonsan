using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mountains
{
    // Build Settings의 씬 목록을 정리하는 도구.
    //
    // 이 파일(ProjectSettings/EditorBuildSettings.asset)은 에디터가 열려 있는 동안 손으로
    // 고치면 Unity가 자기 메모리 값으로 덮어써버린다 — 그래서 텍스트로 편집하지 않고
    // EditorBuildSettings API를 통해 바꾼다.
    static class BuildSceneSetup
    {
        const string SceneFolder = "Assets/Scenes";

        [MenuItem("Mountains/빌드 씬 목록 정리")]
        static void SyncBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            var removed = new List<string>();

            // 기존 항목은 순서를 유지한다 — 목록의 첫 씬이 빌드의 시작 씬이라 순서가 의미를 갖는다.
            // 다만 파일이 사라진 항목(예: 지워진 SampleScene)은 버린다.
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (File.Exists(scene.path))
                {
                    scenes.Add(scene);
                }
                else
                {
                    removed.Add(scene.path);
                }
            }

            var added = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { SceneFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (scenes.Any(s => s.path == path))
                {
                    continue;
                }

                scenes.Add(new EditorBuildSettingsScene(path, true));
                added.Add(path);
            }

            EditorBuildSettings.scenes = scenes.ToArray();

            var report = new System.Text.StringBuilder("[BuildSceneSetup] 빌드 씬 목록을 정리했습니다.\n");
            if (removed.Count > 0)
            {
                report.AppendLine("  제거(파일 없음): " + string.Join(", ", removed));
            }
            if (added.Count > 0)
            {
                report.AppendLine("  추가: " + string.Join(", ", added));
            }
            report.AppendLine("  최종 목록 (맨 위가 시작 씬):");
            for (int i = 0; i < scenes.Count; i++)
            {
                report.AppendLine($"    {i}. {(scenes[i].enabled ? "[O]" : "[ ]")} {scenes[i].path}");
            }
            report.Append("  빌드에 넣고 싶지 않은 씬은 Build Settings에서 체크만 해제하면 됩니다.");
            Debug.Log(report.ToString());
        }
    }
}
