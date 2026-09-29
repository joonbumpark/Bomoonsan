using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Mountains
{
    // TerrainControlPanel이 버튼으로 호출하는 지형 조작/진단 기능들. 창(UI)과 실제 동작을
    // 나눠두면 패널은 배치와 버튼만 다루면 되고, 이 기능들은 나중에 메뉴나 다른 도구에서도
    // 그대로 부를 수 있다.
    static class TerrainTools
    {
        // 식생 배치 조건(minHeight/maxHeight, minSlope/maxSlope)은 0~1 정규화 값이라 실제
        // 지형에 그 구간이 존재하는지 눈으로 알 수 없다 — 예컨대 경사는 1 - normal.y라서
        // 0.35면 50도가 넘는 급경사고, 완만한 지형엔 아예 없는 구간이다. 그런 대역에 그룹을
        // 두면 조용히 0개가 배치된다. 실제 분포를 보고 대역을 정하라고 만든 진단이다.
        public static string DescribeDistribution(ProceduralTerrainMesh terrain)
        {
            int width = terrain.width;
            int length = terrain.length;

            var heights = new List<float>(width * length);
            var slopes = new List<float>(width * length);
            for (int z = 0; z < length; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    heights.Add(terrain.GetNormalizedHeightAt(x, z));
                    slopes.Add(terrain.GetSlopeAt(x, z));
                }
            }

            heights.Sort();
            slopes.Sort();

            float maxSlope = slopes[slopes.Count - 1];
            var report = new StringBuilder();
            report.AppendLine($"[TerrainTools] {terrain.name} 분포 (정점 {heights.Count}개 기준)");
            report.AppendLine($"  고도: {FormatPercentiles(heights)}");
            report.AppendLine($"  경사: {FormatPercentiles(slopes)}");
            // 경사는 1 - normal.y라 각도로 바꿔 보여줘야 직관적이다.
            report.AppendLine($"  경사 최대값 {maxSlope:0.00} ≈ " +
                $"{Mathf.Acos(Mathf.Clamp01(1f - maxSlope)) * Mathf.Rad2Deg:0}도");
            return report.ToString();
        }

        static string FormatPercentiles(List<float> sorted)
        {
            return $"최소 {sorted[0]:0.00} / 25% {Percentile(sorted, 0.25f):0.00} / 중앙 {Percentile(sorted, 0.5f):0.00}" +
                $" / 75% {Percentile(sorted, 0.75f):0.00} / 95% {Percentile(sorted, 0.95f):0.00}" +
                $" / 최대 {sorted[sorted.Count - 1]:0.00}";
        }

        static float Percentile(List<float> sorted, float ratio)
        {
            int index = Mathf.Clamp(Mathf.RoundToInt((sorted.Count - 1) * ratio), 0, sorted.Count - 1);
            return sorted[index];
        }

        // cellSize를 바꾸면 식생 배치 조건이 평가되는 단위도 같이 정밀해진다. 다만 길/호수
        // 웨이포인트가 격자 좌표라 그냥 두면 전부 엉뚱한 위치로 옮겨가고, 노이즈도 격자
        // 좌표로 샘플링해서 산 배치가 통째로 달라진다 — 셋을 한 번에 보정한다.
        // 지형 재생성은 호출한 쪽이 한다(재생성 전후에 할 일이 더 있을 수 있어서).
        public static void ChangeResolution(ProceduralTerrainMesh terrain, float targetCellSize,
            int newWidth, int newLength, float ratio)
        {
            var settings = terrain.settings;

            Undo.RecordObject(settings, "Change Terrain Resolution");
            Undo.RecordObject(terrain, "Change Terrain Resolution");

            foreach (var path in terrain.paths ?? System.Array.Empty<TerrainPath>())
            {
                ScaleWaypoints(path?.waypoints, ratio);
            }
            foreach (var area in terrain.waterAreas ?? System.Array.Empty<WaterArea>())
            {
                ScaleWaypoints(area?.waypoints, ratio);
            }

            settings.width = newWidth;
            settings.length = newLength;
            settings.cellSize = targetCellSize;
            // 노이즈는 격자 좌표로 샘플링되므로 주기(noiseScale)와 위상(noiseOffsetScale)을
            // 같은 비율로 늘려야 월드 공간에서 같은 지형이 나온다.
            settings.noiseScale *= ratio;
            settings.noiseOffsetScale *= ratio;

            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(terrain);
        }

        static void ScaleWaypoints(Vector2[] waypoints, float ratio)
        {
            if (waypoints == null)
            {
                return;
            }

            for (int i = 0; i < waypoints.Length; i++)
            {
                waypoints[i] *= ratio;
            }
        }

        // 지형 파라미터(smoothingIterations, heightMultiplier 등)를 바꾸면 표면 높이가 달라져서,
        // 씬에 배치해둔 이벤트 트리거/NPC/프롭이 땅에 묻히거나 공중에 뜬다. 묻힌 트리거는
        // 그나마 동작하지만, 떠버린 트리거는 플레이어가 그 아래로 지나가 아예 발동하지 않는다.
        //
        // 그냥 지면에 붙이면(Snap To Terrain Y) 일부러 띄워둔 배치의 의도가 사라지므로,
        // 재생성 전에 "지면으로부터 얼마나 떠 있었는지"를 기억해뒀다가 그대로 복원한다.
        public class HeightOffsetSnapshot
        {
            readonly ProceduralTerrainMesh _terrain;
            readonly Transform[] _targets;
            readonly float[] _offsets;

            public int Count => _targets.Length;

            public HeightOffsetSnapshot(ProceduralTerrainMesh terrain)
            {
                _terrain = terrain;
                _targets = CollectHeightFollowingObjects(terrain).ToArray();
                _offsets = new float[_targets.Length];

                for (int i = 0; i < _targets.Length; i++)
                {
                    _offsets[i] = _targets[i].position.y - SampleTerrainHeight(terrain, _targets[i].position);
                    Undo.RecordObject(_targets[i], "Regenerate Terrain (Keep Heights)");
                }
            }

            public void Restore()
            {
                for (int i = 0; i < _targets.Length; i++)
                {
                    if (_targets[i] == null)
                    {
                        continue;
                    }

                    var position = _targets[i].position;
                    position.y = SampleTerrainHeight(_terrain, position) + _offsets[i];
                    _targets[i].position = position;
                }
            }
        }

        static float SampleTerrainHeight(ProceduralTerrainMesh terrain, Vector3 worldPosition)
        {
            Vector2 grid = terrain.WorldToGrid(worldPosition);
            return terrain.GetWorldPositionAt(grid.x, grid.y).y;
        }

        // 지형이 소유한 것(메시/가장자리벽/물/식생 컨테이너)은 재생성이 알아서 다시 만들고,
        // UI(RectTransform)는 월드 높이와 무관하다 — 둘 다 건드리면 안 된다. 씬 루트만
        // 대상으로 삼아 자식은 부모를 따라 함께 움직이게 둔다.
        static List<Transform> CollectHeightFollowingObjects(ProceduralTerrainMesh terrain)
        {
            var result = new List<Transform>();

            foreach (var root in terrain.gameObject.scene.GetRootGameObjects())
            {
                if (root.transform == terrain.transform || root.transform is RectTransform)
                {
                    continue;
                }
                result.Add(root.transform);
            }

            return result;
        }
    }
}
