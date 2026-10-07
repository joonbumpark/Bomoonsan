using UnityEngine;

namespace Mountains
{
    // VegetationScatter의 배치 규칙을 담는 에셋. groups를 컴포넌트 필드로 두면
    // TerrainSceneSetup이 식생을 재배치할 때마다 하드코딩된 기본값으로 덮어써서
    // Inspector에서 튜닝한 값(개수, 높이/경사 범위, 스케일 등)이 매번 날아갔다 —
    // 별도 에셋으로 분리해서 한 번 만들어진 뒤에는 재배치해도 값이 유지되게 한다.
    [CreateAssetMenu(menuName = "Mountains/Vegetation Scatter Settings")]
    public class VegetationScatterSettings : ScriptableObject
    {
        public ScatterGroup[] groups = new ScatterGroup[0];
        public int seed;

        [Tooltip("avoidWater가 켜진 그룹에서, 수면보다 이만큼 위까지도 물속으로 쳐서 비운다. " +
            "수면에 닿을락 말락 한 풀이 물결에 묻혀 지저분해 보이는 것을 막는다.")]
        [Min(0f)] public float waterSurfaceMargin = 0.3f;

        [Tooltip("모든 그룹의 개수(Count)에 곱해지는 전체 배율. 1=설계된 기본 개수 그대로, " +
            "0.5=절반, 2=두 배.")]
        [Range(0f, 3f)] public float densityMultiplier = 1f;

        [Header("GPU Instancing")]
        [Tooltip("gpuInstanced 그룹의 인스턴스를 묶는 격자 셀 크기(월드 단위) — 컬링 단위이기도 " +
            "하다. 너무 작으면 셀이 많아져 오버헤드가 늘고, 너무 크면 화면 밖 셀까지 통째로 그려질 수 있다.")]
        [Min(1f)] public float instancingChunkSize = 25f;
        [Tooltip("gpuInstanced 그룹을 카메라로부터 이 거리(월드 단위)까지만 그린다.")]
        [Min(0f)] public float instancingMaxDrawDistance = 150f;
        [Tooltip("모바일 기기(Application.isMobilePlatform)에서 쓰는 그리기 거리. 수만 개짜리 풀은 잎 모양을 " +
            "알파로 잘라내 겹겹이 그려져(오버드로우) GPU 부담과 발열이 크다 — 모바일에선 가까운 것만 그린다. " +
            "GameManager가 안개 끝도 이 거리에 맞춰 당겨서 풀이 끊기는 경계를 가린다.")]
        [Min(0f)] public float mobileInstancingMaxDrawDistance = 80f;

        // 안개 끝을 식생 그리기 거리보다 이만큼 넘겨 잡는다 — 그 거리에서 이미 안개가 거의 다 덮여
        // 풀/돌이 끊기는 경계가 안 보이게. FogSetup(에디터 메뉴)과 모바일 런타임 조정이 같은 값을 쓴다.
        public const float FogEndMarginOverDrawDistance = 1.05f;
    }
}
