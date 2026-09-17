using UnityEngine;
using UnityEngine.AI;

namespace Mountains
{
    [RequireComponent(typeof(NavMeshAgent))]
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
        public Transform cameraTransform;

        [Tooltip("입력 방향이 지금 바라보는 방향에서 이 각도 이내로만 흔들리면 회전을 " +
            "건드리지 않는다. 카메라가 플레이어 회전을 그대로 따라가므로, 데드존이 없으면 " +
            "조이스틱의 작은 방향 흔들림에도 매 프레임 조금씩 돌아서 카메라가 어지럽게 흔들린다.")]
        public float rotationDeadzoneAngle = 15f;

        [Tooltip("모바일 캔버스의 조이스틱(Fixed/Floating 등). 비워두면 키보드 입력만 쓴다.")]
        public Joystick moveJoystick;

        NavMeshAgent _agent;
        float _yawAngularVelocity;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            // 회전은 지금처럼 SmoothDampAngle로 직접 제어한다(아래) — NavMeshAgent가 이동
            // 속도 기준으로 자체 회전까지 해버리면 이 회전 데드존/스무딩과 충돌한다. 화면
            // 드래그로 직접 도는 TouchRotateInput도 이걸 꺼야 방해받지 않는다.
            _agent.updateRotation = false;

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        void Update()
        {
            // 대화/이벤트 연출 중에는 입력을 완전히 무시한다(InputBlocker에 잠금을 건 쪽이
            // 누구든). 터치 자체는 DialogUI의 전체화면 캐처가 UI 레이캐스트 우선순위로
            // 가로채주지만, 키보드 입력(Input.GetAxisRaw)은 UI를 거치지 않고, 이벤트 중
            // 강제 이동은 아예 UI를 안 거치므로 여기서 한 번에 막는다.
            if (InputBlocker.IsBlocked)
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
                if (Vector3.Dot(transform.forward, moveDirection) < 0.1f)
                {
                    moveDirection = Vector3.zero;
                }
            }

            // NavMeshAgent.Move는 주어진 이동량만큼 옮기되 NavMesh 밖으로는 못 나가게
            // 막아준다 — 나무/바위/가장자리벽은 물론, 호수(WaterNavObstacles로 Not
            // Walkable 처리됨) 안으로도 애초에 들어갈 수가 없어서, 예전에 따로 짜뒀던
            // 물 회피(축 분리 슬라이드) 코드가 통째로 필요 없어졌다. 지형 표면 높이도
            // NavMeshAgent가 알아서 따라가므로 중력 시뮬레이션도 더 이상 필요 없다.
            _agent.Move(moveDirection * moveSpeed * Time.deltaTime);
        }
    }
}
