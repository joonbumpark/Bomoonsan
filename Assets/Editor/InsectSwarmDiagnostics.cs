using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mountains
{
    // 나비 떼가 안 보이거나 색이 이상할 때, 지금 씬(또는 프리팹 편집 화면)에 떠 있는
    // 개체가 실제로 무엇을 쓰고 어떤 색을 만드는지 콘솔에 찍는다. 디스크의 프리팹은
    // 멀쩡한데 화면만 이상한 경우가 있어서(연결이 끊긴 복사본, 오버라이드, 셰이더 문제)
    // 파일을 들여다보는 것만으로는 원인을 가릴 수 없다 — 이걸로 셋을 구분한다.
    static class InsectSwarmDiagnostics
    {
        [MenuItem("Mountains/나비 떼 진단 (콘솔 출력)")]
        static void Diagnose()
        {
            var meshAsset = AssetDatabase.LoadAssetAtPath<Mesh>(InsectSwarmSetup.MeshPath);
            var systems = new HashSet<ParticleSystem>();

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                systems.UnionWith(stage.prefabContentsRoot.GetComponentsInChildren<ParticleSystem>(true));
            }
            systems.UnionWith(Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None));

            int reported = 0;
            foreach (var ps in systems)
            {
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                bool isSwarm = ps.name.Contains("InsectSwarm")
                    || (renderer != null && meshAsset != null && renderer.mesh == meshAsset);
                if (!isSwarm)
                {
                    continue;
                }

                Debug.Log(Describe(ps, simulate: true));
                reported++;
            }

            if (reported == 0)
            {
                Debug.LogWarning("[나비 진단] 씬/프리팹 편집 화면에서 나비 떼를 찾지 못했습니다. 씬에 배치하거나 프리팹을 연 상태에서 실행하세요.");
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InsectSwarmSetup.PrefabPath);
            if (prefab != null)
            {
                Debug.Log("[나비 진단] (비교용) 프리팹 에셋 원본:\n" + Describe(prefab.GetComponent<ParticleSystem>(), simulate: false));
            }
        }

        static string Describe(ParticleSystem ps, bool simulate)
        {
            var sb = new StringBuilder();
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            var material = renderer != null ? renderer.sharedMaterial : null;

            sb.AppendLine($"[나비 진단] {ps.name} (씬: {ps.gameObject.scene.name})");
            sb.AppendLine($"  프리팹 상태: {PrefabUtility.GetPrefabInstanceStatus(ps.gameObject)}");

            var modifications = PrefabUtility.GetPropertyModifications(ps.gameObject);
            if (modifications != null)
            {
                foreach (var m in modifications)
                {
                    if (m.target != null && !(m.target is Transform))
                    {
                        sb.AppendLine($"  오버라이드: {m.target.GetType().Name}.{m.propertyPath} = {m.value}");
                    }
                }
            }

            if (renderer != null)
            {
                var streams = new List<ParticleSystemVertexStream>();
                renderer.GetActiveVertexStreams(streams);
                sb.AppendLine($"  렌더 모드: {renderer.renderMode} / 메시: {(renderer.mesh != null ? renderer.mesh.name : "없음")} / GPU 인스턴싱: {renderer.enableGPUInstancing}");
                sb.AppendLine($"  머티리얼: {(material != null ? AssetDatabase.GetAssetPath(material) : "없음")}");
                sb.AppendLine($"  셰이더: {(material != null && material.shader != null ? material.shader.name : "없음")}");
                sb.AppendLine($"  정점 스트림: {string.Join(", ", streams)}");
            }

            var main = ps.main;
            sb.AppendLine($"  시작 색: {main.startColor.mode} / {main.startColor.colorMin} ~ {main.startColor.colorMax}");

            if (simulate)
            {
                ps.Simulate(1.5f, true, true);
                var particles = new ParticleSystem.Particle[main.maxParticles];
                int count = ps.GetParticles(particles);
                sb.AppendLine($"  1.5초 시뮬레이션 후 입자 {count}개");
                for (int i = 0; i < Mathf.Min(count, 3); i++)
                {
                    sb.AppendLine($"    #{i} 현재 색 {(Color)particles[i].GetCurrentColor(ps)}");
                }
                ps.Play();
            }

            return sb.ToString();
        }
    }
}
