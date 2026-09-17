using UnityEngine;

namespace Mountains
{
    // 대화에 등장하는 캐릭터 하나의 고정 정보. 이름/초상화/대화창 색상은 화자가 누구든
    // 항상 같으므로, DialogLine마다 매번 입력하지 않고 이 에셋 하나를 참조해서 재사용한다.
    [CreateAssetMenu(menuName = "Mountains/Character Data")]
    public class CharacterData : ScriptableObject
    {
        public string characterName;

        [Tooltip("초상화 모델이 없는 캐릭터에만 쓰이는 폴백 이미지. 모델이 있으면 " +
            "CharacterPortraitStage가 실시간으로 그린 초상화가 우선한다.")]
        public Sprite portrait;

        [Tooltip("NPC/플레이어 스폰에 쓰이는 모델 프리팹. portraitPrefab이 비어 있으면 " +
            "대화창 초상화도 이 모델로 그린다.")]
        public GameObject modelPrefab;

        [Tooltip("이 캐릭터가 말할 때 대화창 배경/테두리에 적용되는 색상.")]
        public Color dialogColor = Color.black;

        [Header("Portrait")]
        [Tooltip("초상화 전용 프리팹. 비워두면 modelPrefab을 그대로 쓴다 — 상반신만 따로 " +
            "만들었거나 초상화용 포즈/의상이 다를 때만 채운다.")]
        public GameObject portraitPrefab;

        [Tooltip("자동 프레이밍 기준에서 얼마나 더 당겨 찍을지. 1=모델 전체가 딱 들어오는 " +
            "거리, 2=두 배로 확대(얼굴 위주).")]
        [Min(0.01f)] public float portraitZoom = 1f;

        [Tooltip("초상화에서 캐릭터를 Y축으로 몇 도 돌려 보여줄지. 모델이 +Z를 보고 있으면 " +
            "180이 카메라를 정면으로 마주 보는 각도다.")]
        public float portraitYaw = 180f;

        [Tooltip("자동으로 잡은 초점(모델 경계 중심)에서의 미세 조정. 얼굴을 잡으려면 " +
            "Y를 올린다(월드 단위).")]
        public Vector3 portraitOffset;

        // 초상화용 프리팹이 따로 없으면 모델 프리팹을 그대로 쓴다 — 캐릭터를 만들 때
        // 초상화를 위해 추가로 준비할 게 없게 한다.
        public GameObject PortraitPrefab => portraitPrefab != null ? portraitPrefab : modelPrefab;
    }
}
