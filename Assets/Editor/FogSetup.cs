using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Mountains
{
    // 현재 씬의 안개(Lighting > Environment > Fog)를 켜고 거리/색을 맞춘다.
    //
    // 거리: Linear 안개의 끝을 GPU 인스턴싱 식생의 최대 그리기 거리(instancingMaxDrawDistance)에
    // 맞춘다 — 그 거리에서 풀/돌이 뚝 끊기는 경계가 안개에 묻혀 안 보이게 하는 게 주목적이다.
    // 시작은 끝의 30%로 둬서 가까운 풍경은 선명하고 먼 산만 흐려진다. Linear는 정점에서 계수만
    // 구하면 돼서 모바일에서 가장 싸다.
    //
    // 색: 안개는 스카이박스에는 적용되지 않아서, 안개 색이 지평선 하늘색과 다르면 먼 지형과 하늘
    // 사이에 띠가 생긴다. 씬 스카이박스 머티리얼(PT_Skybox_mat)엔 텍스처가 없어 색을 직접 읽을
    // 수 없으므로, 스카이박스에서 계산된 환경광(ambient probe)을 수평 네 방향으로 평가한 평균을
    // 지평선 색의 근사로 쓴다.
    static class FogSetup
    {
        const float DefaultDrawDistance = 150f;
        const float StartRatio = 0.3f;
        // 환경광이 아직 계산되지 않았거나(검정) 스카이박스가 없을 때 쓰는 옅은 하늘색.
        static readonly Color FallbackColor = new Color(0.72f, 0.80f, 0.86f);

        [MenuItem("Mountains/안개 설정")]
        static void Apply()
        {
            var scatter = Object.FindFirstObjectByType<VegetationScatter>();
            float drawDistance = scatter != null && scatter.settings != null
                ? scatter.settings.instancingMaxDrawDistance
                : DefaultDrawDistance;

            float end = Mathf.Max(1f, drawDistance * VegetationScatterSettings.FogEndMarginOverDrawDistance);
            Color color = EstimateHorizonColor(out bool fromProbe);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = end * StartRatio;
            RenderSettings.fogEndDistance = end;
            RenderSettings.fogColor = color;

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[FogSetup] 안개를 켰습니다 — Linear {RenderSettings.fogStartDistance:0}~{end:0}m, " +
                $"색 {ColorUtility.ToHtmlStringRGB(color)}" +
                (fromProbe ? "(환경광 지평선 색)" : "(환경광이 비어 있어 기본 하늘색)") +
                ". 먼 지형과 하늘 사이에 띠가 보이면 Lighting 창에서 Fog Color를 하늘 지평선 색에 맞추세요.");
        }

        static Color EstimateHorizonColor(out bool fromProbe)
        {
            SphericalHarmonicsL2 probe = RenderSettings.ambientProbe;
            var directions = new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
            var colors = new Color[directions.Length];
            probe.Evaluate(directions, colors);

            Color sum = Color.black;
            foreach (var c in colors)
            {
                sum += c;
            }
            Color average = sum / directions.Length;
            average.a = 1f;

            fromProbe = average.maxColorComponent > 0.02f;
            return fromProbe ? average : FallbackColor;
        }
    }
}
