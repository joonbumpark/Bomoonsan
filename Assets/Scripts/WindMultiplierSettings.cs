using UnityEngine;

namespace Mountains
{
    // Polytope Studio 식생 머티리얼(PT_Vegetation_*)들의 원래 _WindStrength 값을 그대로
    // 보관하는 baseline 데이터베이스. 머티리얼은 씬끼리 공유되는 에셋이라 이 값 자체를
    // 바꾸면 모든 씬에 영향을 주므로, Capture Baseline으로 캡처한 뒤로는 건드리지 않는다.
    // 씬마다 다른 바람 세기를 주고 싶으면 이 에셋이 아니라 WindMultiplierApplier(씬에 놓는
    // 컴포넌트)의 multiplier를 씬별로 다르게 설정한다.
    [CreateAssetMenu(menuName = "Mountains/Wind Multiplier Settings")]
    public class WindMultiplierSettings : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public Material material;
            public float baseWindStrength;

            [Tooltip("나무 트렁크처럼 잎/줄기가 따로 흔들리는 2차 바람 레이어(_WindStrength1)가 있는 머티리얼만 켜진다.")]
            public bool hasSecondaryLayer;
            public float baseWindStrength1;
        }

        public Entry[] entries = new Entry[0];
    }
}
