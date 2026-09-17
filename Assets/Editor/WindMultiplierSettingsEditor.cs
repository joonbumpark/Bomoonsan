using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mountains
{
    [CustomEditor(typeof(WindMultiplierSettings))]
    public class WindMultiplierSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("여기 값은 모든 씬이 공유하는 원본(baseline)이다 — 실제 바람 " +
                "세기 조절은 씬에 놓은 WindMultiplierApplier 컴포넌트의 multiplier로 한다.",
                MessageType.Info);

            if (GUILayout.Button("Capture Baseline (프로젝트에서 _WindStrength 있는 머티리얼 스캔)"))
            {
                CaptureBaseline((WindMultiplierSettings)target);
            }
        }

        // 이미 캡처된 머티리얼의 baseline은 절대 덮어쓰지 않는다 — 새로 추가된 식생
        // 머티리얼만 찾아서 그 원본(현재) 값을 baseline으로 새로 등록한다.
        static void CaptureBaseline(WindMultiplierSettings settings)
        {
            WarnIfMaterialsAreOverridden();

            var list = new List<WindMultiplierSettings.Entry>(settings.entries);
            var existing = new HashSet<Material>();
            foreach (var entry in list)
            {
                if (entry.material != null)
                {
                    existing.Add(entry.material);
                }
            }

            int added = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || existing.Contains(mat) || !mat.HasProperty("_WindStrength"))
                {
                    continue;
                }

                var entry = new WindMultiplierSettings.Entry
                {
                    material = mat,
                    baseWindStrength = mat.GetFloat("_WindStrength"),
                    hasSecondaryLayer = mat.HasProperty("_WindStrength1"),
                };
                if (entry.hasSecondaryLayer)
                {
                    entry.baseWindStrength1 = mat.GetFloat("_WindStrength1");
                }

                list.Add(entry);
                existing.Add(mat);
                added++;
            }

            settings.entries = list.ToArray();
            EditorUtility.SetDirty(settings);
            Debug.Log($"[WindMultiplierSettings] 머티리얼 {added}개를 새로 캡처했습니다. (전체 {settings.entries.Length}개)");
        }

        // 이미 등록된 머티리얼은 위에서 건너뛰므로 평소엔 안전하지만, 엔트리를 지우고 다시
        // 캡처하는 경우엔 지금 씬의 multiplier가 곱해진 값이 새 baseline으로 굳어버린다.
        // (예: 0.4배가 적용된 상태에서 재캡처 → 다음 적용은 0.16배)
        static void WarnIfMaterialsAreOverridden()
        {
            foreach (var applier in Object.FindObjectsOfType<WindMultiplierApplier>())
            {
                if (Mathf.Approximately(applier.multiplier, 1f))
                {
                    continue;
                }

                Debug.LogWarning($"[WindMultiplierSettings] 씬의 {applier.name}이(가) multiplier " +
                    $"{applier.multiplier}를 적용 중이라, 지금 머티리얼에 들어있는 값은 원본이 아닙니다. " +
                    "이미 등록된 머티리얼은 건너뛰므로 보통은 문제없지만, 엔트리를 지웠다가 다시 캡처하는 " +
                    "중이라면 multiplier를 1로 두고 캡처하세요.");
            }
        }
    }
}
