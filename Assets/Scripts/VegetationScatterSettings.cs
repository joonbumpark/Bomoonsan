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

        [Tooltip("모든 그룹의 개수(Count)에 곱해지는 전체 배율. 1=설계된 기본 개수 그대로, " +
            "0.5=절반, 2=두 배.")]
        [Range(0f, 3f)] public float densityMultiplier = 1f;
    }
}
