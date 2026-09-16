using UnityEngine;

namespace Mountains
{
    // ProceduralTerrainMesh의 생성 파라미터를 담는 에셋. 컴포넌트 필드로 두면
    // TerrainSceneSetup이 지형을 재생성할 때마다 하드코딩된 기본값으로 덮어써서
    // Inspector에서 튜닝한 값이 매번 날아갔다 — 별도 에셋으로 분리해서 한 번 만들어진
    // 뒤에는 재생성해도 값이 유지되게 한다. paths(길 웨이포인트 좌표)는 지형마다
    // 고유한 공간 데이터라 여기 포함하지 않고 ProceduralTerrainMesh에 그대로 남아있다.
    [CreateAssetMenu(menuName = "Mountains/Terrain Generation Settings")]
    public class TerrainGenerationSettings : ScriptableObject
    {
        [Header("Grid")]
        [Min(2)] public int width = 100;
        [Min(2)] public int length = 100;
        public float cellSize = 10f;

        [Header("Noise (fBm)")]
        public float noiseScale = 28f;
        [Range(1, 8)] public int octaves = 3;
        [Range(0f, 1f)] public float persistence = 0.45f;
        [Min(1f)] public float lacunarity = 2f;
        public float heightMultiplier = 60f;
        public int seed;

        [Header("Smoothing")]
        [Tooltip("생성 후 이웃 정점끼리 평균 내는 횟수. 높일수록 뾰족한 봉우리가 둥글게 깎인다.")]
        [Range(0, 8)] public int smoothingIterations = 2;

        [Header("Coloring")]
        public Gradient heightGradient = CreateDefaultGradient();

        [Header("Path Tuning")]
        [Tooltip("길 중심에서 완전히 평탄/포장되는 폭의 절반 (월드 단위).")]
        public float pathWidth = 4f;
        [Tooltip("가장자리가 원래 지형으로 부드럽게 섞이는 폭 (월드 단위).")]
        public float pathBlendWidth = 3f;
        [Tooltip("평탄화 강도. 0=원래 지형 그대로, 1=완전 평탄.")]
        [Range(0f, 1f)] public float pathFlattenStrength = 1f;
        [Tooltip("웨이포인트 사이를 곡선으로 얼마나 촘촘히 리샘플링할지. 높일수록 부드럽다.")]
        [Range(2, 24)] public int pathCurveSamplesPerSegment = 8;
        [Tooltip("길 마스크를 계산하기 전에 좌표 자체를 노이즈로 뒤트는 세기 (월드 단위, Domain Warping). " +
            "0이면 매끈한 경계. pathBlendWidth와 비슷하거나 더 큰 값을 줘야 core 깊숙이 파고드는 " +
            "얼룩이 생긴다 — 너무 작으면 가장자리만 살짝 흔들리는 정도에 그친다.")]
        public float pathEdgeJitter = 5f;
        [Tooltip("길 마스크 텍스처의 해상도. 높일수록 길이 더 얇고 선명하게 나온다(정점 간격과 무관).")]
        [Range(64, 1024)] public int pathMaskResolution = 512;

        [Header("Water Tuning")]
        [Tooltip("호수 중심이 림(경계) 높이보다 얼마나 깊이 파이는지 (월드 단위).")]
        public float waterDepth = 8f;
        [Tooltip("호안에서 완전히 깊어지기까지의 폭 (월드 단위). 지형 카빙과 물 마스크 텍스처 양쪽에 쓰인다.")]
        public float waterShoreBlendWidth = 12f;
        [Tooltip("호안 경계를 계산하기 전에 좌표 자체를 노이즈로 뒤트는 세기 (월드 단위, Domain Warping). " +
            "pathEdgeJitter와 같은 개념 — 0이면 매끈한 호안.")]
        public float waterEdgeJitter = 6f;
        [Tooltip("웨이포인트 사이를 곡선으로 얼마나 촘촘히 리샘플링할지. 높일수록 호안이 부드럽다.")]
        [Range(2, 24)] public int waterCurveSamplesPerSegment = 8;
        [Tooltip("호수 하나당 마스크 텍스처의 해상도. 호수 bbox만 덮으므로 길 마스크보다 낮아도 충분하다.")]
        [Range(64, 1024)] public int waterMaskResolution = 256;
        [Tooltip("호안 절벽 경사. 1이면 waterShoreBlendWidth 전체에 걸쳐 완만하게 깊어지고, " +
            "커질수록 호안 근처(좁은 폭)에서만 급격히 깊어진 뒤 안쪽은 평평한 바닥으로 남는다 " +
            "— 절벽처럼 가파른 호안 + 평평한 바닥을 원하면 값을 높인다.")]
        [Min(1f)] public float waterCliffSharpness = 4f;

        [Header("Edge Walls")]
        [Tooltip("지형 바깥 테두리에 보이지 않는 벽(BoxCollider)을 세워서 플레이어가 " +
            "지형 밖으로 떨어지지 않게 한다.")]
        public bool buildEdgeWalls = true;
        [Tooltip("지형 최고점보다 얼마나 더 높이 벽을 세울지 (월드 단위).")]
        [Min(0f)] public float edgeWallHeight = 50f;
        [Tooltip("벽 두께 (월드 단위). 지형 바깥쪽으로 붙기 때문에 걸어다닐 수 있는 범위는 " +
            "줄지 않는다. 너무 얇으면 빠르게 움직일 때 뚫고 나갈 수 있다.")]
        [Min(0.01f)] public float edgeWallThickness = 5f;

        static Gradient CreateDefaultGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.30f, 0.45f, 0.24f), 0f),
                    new GradientColorKey(new Color(0.36f, 0.40f, 0.22f), 0.35f),
                    new GradientColorKey(new Color(0.45f, 0.42f, 0.38f), 0.55f),
                    new GradientColorKey(new Color(0.55f, 0.53f, 0.50f), 0.8f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }
    }
}
