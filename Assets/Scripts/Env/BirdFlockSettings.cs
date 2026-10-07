using UnityEngine;
using UnityEngine.Rendering;

namespace Mountains.Env
{
    // 새 떼의 설정. FishSchoolSettings와 같은 이유로 에셋으로 뺀다 — 씬을 다시 만들거나
    // 오브젝트를 새로 놓아도 튜닝한 값이 살아남고, 씬마다 다른 무리를 두면서 같은
    // 컴포넌트를 쓸 수 있다.
    //
    // FishSchoolSettings와 달리 종류 배열 없이 프리팹 하나만 쓴다. 프리팹에서는 메시와
    // 텍스처만 꺼내 GPU 인스턴싱으로 그리므로, 프리팹의 뼈대 애니메이션(Animator)은
    // 재생되지 않고 날갯짓은 셰이더가 흉내 낸다(BirdFlock 참고).
    [CreateAssetMenu(menuName = "Mountains/Bird Flock Settings")]
    public class BirdFlockSettings : ScriptableObject
    {
        [Tooltip("끄면 새를 스폰하지 않고, 이미 있으면 정리한다. Play 중에 켜고 끌 수 있다.")]
        public bool spawnBirds = true;

        [Header("모델")]
        [Tooltip("메시와 텍스처를 꺼내 쓸 새 프리팹. SkinnedMeshRenderer든 MeshRenderer든 " +
            "상관없다 — 기본 자세 메시만 쓰고 Animator는 무시한다.")]
        public GameObject birdPrefab;
        [Tooltip("모델이 +Z(진행 방향)를 바라보지 않을 때 보정할 회전(도). 새가 옆이나 " +
            "뒤로 날면 이 값을 돌린다.")]
        public Vector3 modelRotationOffset;

        [Header("개체수")]
        [Min(0)] public int spawnCount = 24;
        [Tooltip("개체마다 이 범위에서 크기를 뽑는다.")]
        public Vector2 scaleRange = new Vector2(0.8f, 1.3f);

        [Header("영역")]
        [Tooltip("BirdFlock 오브젝트 위치를 중심으로 한 사각 영역의 (가로 X, 세로 Z) 크기.")]
        public Vector2 boundsSize = new Vector2(60f, 60f);
        [Tooltip("BirdFlock 오브젝트 Y를 기준으로 한 최저 고도(더한 값).")]
        public float minAltitude = 10f;
        [Tooltip("BirdFlock 오브젝트 Y를 기준으로 한 최고 고도(더한 값).")]
        public float maxAltitude = 22f;

        [Header("이동")]
        public float maxSpeed = 6f;
        public float minSpeed = 3f;
        public float maxForce = 4f;
        [Tooltip("진행 방향으로 돌아가는(피치가 바뀌는) 속도.")]
        public float turnSpeed = 3f;
        [Tooltip("오르내리는 최대 각도(도). 수평 기준으로 이 각도보다 가파르게 상승/하강하지 " +
            "않는다 — 고도 경계 회피나 분리력이 수직으로 밀어도 급강하/급상승하지 않게 " +
            "막는다. 90이면 제한 없음.")]
        [Range(0f, 90f)] public float maxPitchAngle = 25f;

        [Header("무리")]
        [Tooltip("이 반경 안의 이웃만 본다. 격자 셀 크기도 이 값을 쓴다.")]
        public float perceptionRadius = 10f;
        [Tooltip("이 반경보다 가까우면 밀어낸다. 물고기(수면 2D)와 달리 새는 고도까지 " +
            "자유로운 3D라, 인지 반경 대비 너무 작으면(예: 절반 이하) 무리가 시간이 " +
            "지날수록 하나의 빽빽한 덩어리로 뭉친다 — 응집력은 항상 이웃을 향해 끌어당기는데 " +
            "분리력은 아주 가까울 때만 반응하기 때문이다. 인지 반경의 80~90% 정도로 " +
            "넓게 잡아야 서로 가까워지는 족족 밀어내서 느슨한 무리로 유지된다.")]
        public float separationRadius = 9f;
        [Range(10f, 360f)] public float fovAngle = 300f;
        public float alignWeight = 1f;
        [Tooltip("너무 높이면(separateWeight 대비) 무리가 뭉친다 — separationRadius 주석 참고.")]
        public float cohereWeight = 0.4f;
        public float separateWeight = 3f;
        [Tooltip("영역(수평 경계 + 고도 밴드) 밖으로 나가면 안으로 되돌리는 힘.")]
        public float boundsWeight = 3f;

        [Header("날개짓")]
        public float flapSpeed = 6f;
        [Tooltip("날개 끝이 몸통에서 떨어진 거리 대비 얼마나 위아래로 움직일지(비율). " +
            "모델 크기와 무관하게 같은 각도로 펄럭인다.")]
        [Range(0f, 1f)] public float flapAmplitude = 0.4f;
        [Tooltip("날개 폭(반) 중 이 비율 안쪽은 몸통으로 보고 움직이지 않는다.")]
        [Range(0f, 0.9f)] public float bodyWidthRatio = 0.15f;
        [Range(0f, 1f)] public float phaseSpread = 1f;

        [Header("렌더링")]
        [Tooltip("프리팹 머티리얼의 색/텍스처에 곱할 색. 흰색이면 원본 그대로.")]
        public Color tint = Color.white;
        public ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off;
        public bool receiveShadows = false;
        [Tooltip("Custom/URP_BirdDisplacement 셰이더 + Enable GPU Instancing을 켠 기반 머티리얼. " +
            "런타임에 복제해서 텍스처/날갯짓 값을 채운다. 빌드에는 에셋이 참조하는 셰이더와, " +
            "인스턴싱을 켠 머티리얼이 쓰는 인스턴싱 변형만 들어가므로 이 칸이 비면 에디터에선 " +
            "보여도 빌드에서는 새가 안 보인다.")]
        public Material material;
    }
}
