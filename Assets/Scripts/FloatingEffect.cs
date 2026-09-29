using UnityEngine;

namespace Mountains
{
    public class FloatingEffect : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("위아래로 움직이는 높이 (진폭)")]
        public float height = 0.5f;

        [Tooltip("위아래로 움직이는 속도")]
        public float speed = 2f;

        [Tooltip("지그재그나 랜덤한 시작점을 원할 때 시간 오프셋")]
        public float randomOffset = 0f;

        private Vector3 startLocalPos;

        void Start()
        {
            // 시작했을 때의 로컬 위치를 기준점으로 저장
            startLocalPos = transform.localPosition;
            
            // 오브젝트마다 둥둥 떠다니는 타이밍이 엇갈리게 하고 싶다면 랜덤 값 부여 가능
            // randomOffset = UnityEngine.Random.Range(0f, 10f);
        }

        void Update()
        {
            // 사인(Sin) 함수를 이용해 부드러운 왕복 운동 계산
            float newY = startLocalPos.y + Mathf.Sin((Time.time + randomOffset) * speed) * height;
            
            // 위치 적용
            transform.localPosition = new Vector3(startLocalPos.x, newY, startLocalPos.z);
        }
    }
}