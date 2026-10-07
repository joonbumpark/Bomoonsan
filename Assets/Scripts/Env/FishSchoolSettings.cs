using UnityEngine;
using UnityEngine.Rendering;

namespace Mountains.Env
{
    // 물고기 무리의 설정. VegetationScatterSettings와 같은 이유로 에셋으로 뺀다 —
    // 씬을 다시 만들거나 오브젝트를 새로 놓아도 튜닝한 값이 살아남고, 씬마다 다른 무리를
    // 두면서 같은 컴포넌트를 쓸 수 있다.
    //
    // 씬 오브젝트를 가리키는 값(포식자 Transform)과 무리의 중심 위치는 에셋에 담을 수
    // 없으므로 FishSchool 쪽에 남는다.
    [CreateAssetMenu(menuName = "Mountains/Fish School Settings")]
    public class FishSchoolSettings : ScriptableObject
    {
        [Tooltip("끄면 물고기를 스폰하지 않고, 이미 있으면 정리한다. Play 중에 켜고 끌 수 있다.")]
        public bool spawnFish = true;

        [Header("종류")]
        [Tooltip("weight 비중대로 섞여 스폰된다. 프리팹은 Animator 없는 정적 메시여야 한다.")]
        public FishSpecies[] species = new FishSpecies[0];
        [Min(0)] public int spawnCount = 80;

        [Header("영역")]
        [Tooltip("FishSchool 오브젝트 위치를 중심으로 한 사각 영역의 (가로 X, 세로 Z) 크기.")]
        public Vector2 boundsSize = new Vector2(40f, 25f);
        [Tooltip("켜면 FishSchool 오브젝트의 Y를, 끄면 아래 waterHeight를 헤엄칠 높이로 쓴다.")]
        public bool useTransformHeight = true;
        public float waterHeight;

        [Header("이동")]
        public float maxSpeed = 5f;
        public float minSpeed = 1.5f;
        public float maxForce = 3f;
        [Tooltip("진행 방향으로 돌아가는 속도.")]
        public float turnSpeed = 6f;

        [Header("무리")]
        [Tooltip("이 반경 안의 이웃만 본다. 격자 셀 크기도 이 값을 쓴다.")]
        public float perceptionRadius = 6f;
        [Tooltip("이 반경보다 가까우면 밀어낸다. 보통 인지 반경의 절반 이하.")]
        public float separationRadius = 2.4f;
        [Range(10f, 360f)] public float fovAngle = 270f;
        public float alignWeight = 1f;
        public float cohereWeight = 1f;
        public float separateWeight = 1.5f;
        public float boundsWeight = 4f;

        [Header("렌더링")]
        public ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off;
        public bool receiveShadows = false;
    }
}
