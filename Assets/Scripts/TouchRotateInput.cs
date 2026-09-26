using UnityEngine;
using UnityEngine.EventSystems;

namespace Mountains
{
    // 조이스틱(이동)과는 별개로, 화면을 터치 드래그하면 플레이어를 Y축 기준으로 제자리
    // 회전시킨다. 화면 전체를 덮는 투명 UI Image(레이캐스트 타겟만 켜둠)에 붙여서 쓴다 —
    // 조이스틱은 자기 영역의 레이캐스트를 먼저 가로채므로(하이어라키상 이 오브젝트보다
    // 뒤에 옴) 조이스틱을 누른 드래그는 여기로 넘어오지 않는다.
    public class TouchRotateInput : MonoBehaviour, IDragHandler
    {
        public Transform player;
        [Tooltip("드래그 1픽셀당 회전 각도.")]
        public float sensitivity = 0.2f;

        float _pendingYaw;

        public void OnDrag(PointerEventData eventData)
        {
            // 이벤트/대화 중에는 회전도 막는다 — 누적해두면 잠금이 풀리는 순간 그동안의
            // 드래그가 한꺼번에 적용되므로, 아예 받지 않고 버린다.
            if (InputBlocker.IsBlocked)
            {
                return;
            }

            _pendingYaw += eventData.delta.x * sensitivity;
        }

        // CharacterMovement의 자동 회전(이동 방향으로 도는 것)은 Update()에서 일어난다.
        // Unity는 스크립트 실행 순서 설정과 무관하게 모든 Update()가 끝난 뒤에만
        // LateUpdate()를 실행하므로, 여기서 추가 회전을 적용하면 항상 그 자동 회전
        // "위에 얹히는" 결과가 된다.
        void LateUpdate()
        {
            if (player == null || Mathf.Approximately(_pendingYaw, 0f))
            {
                return;
            }

            player.Rotate(Vector3.up, _pendingYaw, Space.World);
            _pendingYaw = 0f;
        }
    }
}
