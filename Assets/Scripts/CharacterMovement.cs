using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

namespace Mountains
{
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMovement : MonoBehaviour
    {
        public float moveSpeed = 5f;
        [Tooltip("회전 각속도의 상한(초당 도). 실제로는 이 속도로 즉시 도는 게 아니라, " +
            "rotationSmoothTime에 따라 이 속도까지 부드럽게 가속했다가 목표 각도에 " +
            "가까워지면 다시 부드럽게 감속한다.")]
        public float rotationSpeed = 360f;
        [Tooltip("회전 각속도가 가속/감속하는 데 걸리는 대략적인 시간(초). 작을수록 더 " +
            "즉각적으로 반응하고, 클수록 더 느긋하게 붙는다.")]
        public float rotationSmoothTime = 0.15f;
        public float gravity = -20f;
        public Transform cameraTransform;

        [Tooltip("입력 방향이 지금 바라보는 방향에서 이 각도 이내로만 흔들리면 회전을 " +
            "건드리지 않는다. 카메라가 플레이어 회전을 그대로 따라가므로, 데드존이 없으면 " +
            "조이스틱의 작은 방향 흔들림에도 매 프레임 조금씩 돌아서 카메라가 어지럽게 흔들린다.")]
        public float rotationDeadzoneAngle = 15f;

        [Tooltip("모바일 캔버스의 Fixed Joystick. 비워두면 키보드 입력만 쓴다.")]
        public Joystick moveJoystick;

        [Tooltip("호수 영역 판정을 위한 지형 참조. 비워두면 물 차단만 꺼지고 나머지는 " +
            "그대로 동작한다. 호수는 사각형이 아니라 임의의 폴리곤이라 콜라이더 대신 " +
            "ProceduralTerrainMesh.IsInsideWaterArea 판정으로 막는다.")]
        public ProceduralTerrainMesh terrain;

        CharacterController _controller;
        Animator _animator;
        float _verticalVelocity;
        float _yawAngularVelocity;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            _animator = GetComponentInChildren<Animator>(); // Animator 컴포넌트 가져오기
        }

        void Update()
        {
            // 대화창 등 이벤트가 재생 중일 때는 입력을 완전히 무시한다. 터치 자체는
            // DialogUI의 전체화면 캐처가 UI 레이캐스트 우선순위로 이미 가로채지만,
            // 키보드 입력(Input.GetAxisRaw)은 UI를 거치지 않아 별도로 막아야 한다.
            if (DialogUI.Instance != null && DialogUI.Instance.IsShowing)
            {
                return;
            }

            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            if (moveJoystick != null)
            {
                horizontal += moveJoystick.Horizontal;
                vertical += moveJoystick.Vertical;
            }
            Vector3 input = new Vector3(horizontal, 0f, vertical);

            Vector3 moveDirection = Vector3.zero;
            if (input.sqrMagnitude > 0.0001f)
            {
                Vector3 forward = cameraTransform != null
                    ? Vector3.Scale(cameraTransform.forward, new Vector3(1f, 0f, 1f)).normalized
                    : Vector3.forward;
                Vector3 right = cameraTransform != null
                    ? Vector3.Scale(cameraTransform.right, new Vector3(1f, 0f, 1f)).normalized
                    : Vector3.right;

                moveDirection = (forward * vertical + right * horizontal).normalized;

                // 데드존 밖으로 벗어난 경우에만 회전을 갱신한다. 이동 방향(moveDirection)
                // 자체는 입력을 그대로 따르므로 이동 정확도는 그대로 유지되고, 캐릭터가
                // "바라보는" 방향(그리고 그걸 따라가는 카메라)만 작은 흔들림에 안 흔들린다.
                float angleFromFacing = Vector3.Angle(transform.forward, moveDirection);
                if (angleFromFacing > rotationDeadzoneAngle)
                {
                    // SmoothDampAngle은 RotateTowards처럼 각속도를 rotationSpeed로 상한
                    // 걸어두면서도(그래서 남은 각도와 무관하게 동일한 최고 속도로 도는,
                    // 뒤돌 때 유독 빨리 도는 문제를 그대로 안 일으키면서), 내부적으로
                    // 임계 감쇠 스프링을 써서 회전이 시작될 때 부드럽게 가속하고 목표
                    // 각도에 가까워질수록 부드럽게 감속한다 — RotateTowards의 "즉시 최고
                    // 속도로 돌다가 목표에서 뚝 멈추는" 느낌 대신 자연스러운 가감속이 된다.
                    Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                    float currentYaw = transform.eulerAngles.y;
                    float targetYaw = targetRotation.eulerAngles.y;
                    float smoothedYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref _yawAngularVelocity,
                        rotationSmoothTime, rotationSpeed, Time.deltaTime);
                    transform.rotation = Quaternion.Euler(0f, smoothedYaw, 0f);
                }

                // 정면 기준으로 90도(내적 0)보다 더 뒤쪽 방향을 가리키면, 그 방향으로 미끄러지듯
                // 움직이는 대신 제자리에서 계속 회전만 하다가(위 SmoothDampAngle) 다시 앞쪽
                // (내적 0 이상)으로 들어오면 그때부터 이동을 재개한다 — 회전이 이동을 못 따라가서
                // 옆걸음/뒷걸음처럼 미끄러지는 걸 막는다.
                if (Vector3.Dot(transform.forward, moveDirection) < 0f)
                {
                    moveDirection = Vector3.zero;
                }
            }

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }
            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = moveDirection * moveSpeed + Vector3.up * _verticalVelocity;
            motion = BlockWaterMotion(motion);

            var dtMotion = motion * Time.deltaTime;
            _controller.Move(dtMotion);

            if (math.abs(moveDirection.z) > 0.5f)
            {
                _animator.SetFloat("speed", 1);
            }
            else
            {
                _animator.SetFloat("speed", 0);
            }
        }

        // 호수는 임의의 폴리곤이라 사각형 콜라이더로 못 막고, 물 표면 메시도 호수 bbox를
        // 덮는 평평한 쿼드 하나뿐이라 그대로 콜라이더로 쓰면 사각형 전체가 막힌다. 대신
        // 이미 있는 폴리곤 판정(IsInsideWaterArea)으로 이동 단계에서 직접 막는다.
        //
        // PhysX를 안 거치므로 벽을 따라 미끄러지는 처리도 직접 해야 한다 — 전체 이동이
        // 막히면 X만/Z만 따로 시도해서, 막히지 않는 축만 살린다(고전적인 축 분리 방식).
        // 그래야 호안에 닿았을 때 그대로 멈춰 서지 않고 물가를 따라 걸을 수 있다.
        Vector3 BlockWaterMotion(Vector3 motion)
        {
            if (terrain == null)
            {
                return motion;
            }

            Vector3 horizontal = new Vector3(motion.x, 0f, motion.z);
            if (horizontal.sqrMagnitude < 0.0001f || !WouldEnterWater(horizontal))
            {
                return motion;
            }

            if (!WouldEnterWater(new Vector3(motion.x, 0f, 0f)))
            {
                motion.z = 0f;
            }
            else if (!WouldEnterWater(new Vector3(0f, 0f, motion.z)))
            {
                motion.x = 0f;
            }
            else
            {
                motion.x = 0f;
                motion.z = 0f;
            }
            return motion;
        }

        bool WouldEnterWater(Vector3 horizontalMotion)
        {
            Vector3 next = transform.position + horizontalMotion * Time.deltaTime;
            Vector2 grid = terrain.WorldToGrid(next);
            return terrain.IsInsideWaterArea(grid.x, grid.y);
        }

        // 지형을 다시 생성하면서 플레이어를 순간이동시킨 직후에 호출한다. 순간이동 전에
        // 누적돼있던 낙하 속도가 남아있으면, 새 위치에 착지한 것처럼 보여도 다음 프레임에
        // 그 속도가 그대로 적용돼 다시 훅 꺼지듯 떨어지는 것처럼 보인다.
        public void ResetVelocity()
        {
            _verticalVelocity = 0f;
        }
    }
}
