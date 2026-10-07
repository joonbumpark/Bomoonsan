using Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Mountains
{
    // 조이스틱(이동)과는 별개로, 화면을 터치 드래그하면 카메라만 플레이어 주위로 돌려
    // 둘러본다(가로 = 좌우 공전, 세로 = 위아래 각도). 화면 전체를 덮는 투명 UI Image
    // (레이캐스트 타겟만 켜둠)에 붙여서 쓴다 — 조이스틱은 자기 영역의 레이캐스트를 먼저
    // 가로채므로(하이어라키상 이 오브젝트보다 뒤에 옴) 조이스틱을 누른 드래그는 여기로
    // 넘어오지 않는다.
    //
    // 카메라 리그(Transposer)는 여전히 플레이어 회전을 따라간다(LockToTargetWithWorldUp) —
    // 여기서는 기본 오프셋을 둘러본 각도만큼 돌려서 덮어쓸 뿐이라, 조이스틱으로 돌면
    // 카메라가 지금처럼 등 뒤로 따라붙고 둘러본 시점도 같이 따라 돈다. 바인딩 모드를
    // WorldSpace로 바꿔 카메라를 완전히 떼어내면 조이스틱 조작감까지 바뀐다.
    public class TouchRotateInput : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Tooltip("드래그 1픽셀당 회전 각도.")]
        public float sensitivity = 0.2f;
        [Tooltip("기본 카메라 각도 기준으로 위아래로 돌릴 수 있는 범위(도). x는 카메라가 " +
            "낮아지는 쪽(음수), y는 높아지는 쪽 — 땅 밑이나 머리 위로 넘어가지 않게 막는다.")]
        public Vector2 pitchLimits = new Vector2(-20f, 40f);
        [Tooltip("이동을 시작한 뒤 시점이 등 뒤로 돌아오는 데 걸리는 대략적인 시간(초).")]
        public float recenterTime = 0.4f;

        // 둘러본 좌우 각도(도, -180~180). CharacterMovement가 이동 기준 방향에서 이만큼을
        // 되돌려서 쓴다 — 안 그러면 옆을 보는 동안 조이스틱을 밀었을 때 플레이어가 카메라
        // 쪽으로 돌고, 카메라가 그걸 또 따라 도는 무한 회전이 생긴다.
        public float LookYaw => _yaw;

        CinemachineTransposer _transposer;
        CharacterMovement _movement;
        Vector3 _baseOffset;
        float _yaw;
        float _pitch;
        float _yawVelocity;
        float _pitchVelocity;
        bool _dragging;

        // 플레이어를 만들 때 CharacterManager가 부른다. 다시 불리면 이전 카메라의 오프셋을
        // 원래대로 돌려놓고 새 카메라의 현재 오프셋을 기본값으로 잡는다.
        public void Bind(CinemachineVirtualCamera virtualCamera, CharacterMovement movement)
        {
            RestoreBaseOffset();

            _movement = movement;
            _transposer = virtualCamera != null ? virtualCamera.GetCinemachineComponent<CinemachineTransposer>() : null;
            if (_transposer != null)
            {
                _baseOffset = _transposer.m_FollowOffset;
            }
            else if (virtualCamera != null)
            {
                Debug.LogWarning("[TouchRotateInput] 카메라 Body가 Transposer가 아니라 시점 회전을 건너뜁니다.", this);
            }

            _yaw = 0f;
            _pitch = 0f;
            _yawVelocity = 0f;
            _pitchVelocity = 0f;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = true;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            // 이벤트/대화 중에는 회전도 막는다 — 누적해두면 잠금이 풀리는 순간 그동안의
            // 드래그가 한꺼번에 적용되므로, 아예 받지 않고 버린다.
            if (InputBlocker.IsBlocked)
            {
                return;
            }

            _yaw = Mathf.DeltaAngle(0f, _yaw + eventData.delta.x * sensitivity);
            // 위로 드래그하면 위를 올려다보도록(= 카메라가 낮아지도록) 뺀다.
            _pitch = Mathf.Clamp(_pitch - eventData.delta.y * sensitivity, pitchLimits.x, pitchLimits.y);
        }

        void LateUpdate()
        {
            if (_transposer == null)
            {
                return;
            }

            // 걷는 동안 앞이 안 보이지 않게 이동을 시작하면 등 뒤로 되돌린다. 걸으면서
            // 드래그하는 중(조이스틱 + 다른 손가락)이면 둘러보는 쪽을 우선한다. 이벤트
            // 중에도 되돌려서, 연출이 끝나고 조작이 돌아왔을 때 기본 시점에서 시작한다.
            bool recenter = InputBlocker.IsBlocked
                || (!_dragging && _movement != null && _movement.HasMoveInput);
            if (recenter)
            {
                _yaw = Mathf.DeltaAngle(0f, Mathf.SmoothDampAngle(_yaw, 0f, ref _yawVelocity, recenterTime));
                _pitch = Mathf.SmoothDamp(_pitch, 0f, ref _pitchVelocity, recenterTime);
            }
            else
            {
                _yawVelocity = 0f;
                _pitchVelocity = 0f;
            }

            // 피치(로컬 X) 먼저, 그다음 요(Y) — 기본 오프셋이 플레이어 뒤쪽(-Z)이라 X축으로
            // 돌리면 카메라가 플레이어를 중심으로 위아래로 원을 그린다.
            _transposer.m_FollowOffset = Quaternion.Euler(_pitch, _yaw, 0f) * _baseOffset;
        }

        void OnDisable()
        {
            RestoreBaseOffset();
        }

        void RestoreBaseOffset()
        {
            if (_transposer != null)
            {
                _transposer.m_FollowOffset = _baseOffset;
            }
        }
    }
}
