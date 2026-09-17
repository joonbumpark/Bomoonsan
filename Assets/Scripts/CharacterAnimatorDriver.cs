using UnityEngine;
using UnityEngine.AI;

namespace Mountains
{
    // 캐릭터 루트(Player/Npc 프리팹)에 붙어서, Root 밑에 런타임으로 생성되는 모델 프리팹의
    // Animator에 이동 상태를 흘려보낸다.
    //
    // 속도를 NavMeshAgent.velocity가 아니라 transform 위치 변화로 재는 이유: 플레이어는
    // agent.Move(), NPC는 SetDestination, 이벤트 연출은 DOTween(MoveThenRotatePlayerAction,
    // TweenMoveToAction)으로 각각 다르게 움직인다. 트윈으로 움직이는 동안은 agent.velocity가
    // 0이라, 캐릭터는 미끄러지는데 애니메이션만 Idle로 남는다. 위치 변화는 이동 수단과
    // 무관하게 항상 맞는다.
    public class CharacterAnimatorDriver : MonoBehaviour
    {
        [Header("Animator 파라미터")]
        [Tooltip("이동 속도를 넣을 float 파라미터 이름. 모델의 Animator에 없으면 건너뛴다.")]
        public string speedParameter = "Speed";
        [Tooltip("이동 중인지 넣을 bool 파라미터 이름. 없으면 건너뛴다.")]
        public string movingParameter = "IsMoving";

        [Header("튜닝")]
        [Tooltip("켜면 속도를 0~1로 정규화해서 넣는다(블렌드 트리가 0~1을 쓰는 모델용). " +
            "기준 속도는 NavMeshAgent.speed, 없으면 CharacterMovement.moveSpeed.")]
        public bool normalizeSpeed = true;
        [Tooltip("이 속도(m/s) 미만이면 멈춘 것으로 본다.")]
        [Min(0f)] public float movingThreshold = 0.1f;
        [Tooltip("speed 파라미터가 목표값까지 따라가는 데 걸리는 시간(초). 0이면 즉시 반영 — " +
            "블렌드 트리가 툭툭 튀면 이 값을 올린다.")]
        [Min(0f)] public float speedDampTime = 0.1f;
        [Tooltip("한 프레임에 이 거리(월드 단위)를 넘게 움직이면 순간이동으로 보고 속도를 0으로 둔다. " +
            "TeleportPlayerAction이나 NavMeshAgent.Warp가 애니메이션을 한 번 튀게 하는 걸 막는다.")]
        [Min(0.01f)] public float teleportDistance = 2f;

        Animator _animator;
        int _speedHash;
        int _movingHash;
        bool _hasSpeed;
        bool _hasMoving;
        float _referenceSpeed = 1f;
        Vector3 _lastPosition;

        public Animator Animator => _animator;

        void Awake()
        {
            _lastPosition = transform.position;
            _referenceSpeed = ResolveReferenceSpeed();
        }

        // 정규화 기준이 될 "이 캐릭터의 최고 속도". 플레이어는 NavMeshAgent를 갖고 있지만
        // 실제 이동은 agent.Move(방향 * moveSpeed * dt)라서 agent.speed가 아니라
        // CharacterMovement.moveSpeed가 최고 속도다 — 조작으로 움직이는 쪽을 먼저 본다.
        // NPC는 CharacterMovement가 없고 agent가 직접 움직이므로 agent.speed가 맞는다.
        float ResolveReferenceSpeed()
        {
            var movement = GetComponent<CharacterMovement>();
            if (movement != null)
            {
                return movement.moveSpeed;
            }

            var agent = GetComponent<NavMeshAgent>();
            return agent != null ? agent.speed : 1f;
        }

        // 씬에 직접 배치해둔 캐릭터(CharacterManager를 안 거친 경우)를 위한 폴백.
        // CharacterManager는 모델을 만든 직후 Bind를 명시적으로 부르므로 여기선 아무것도 안 한다.
        void Start()
        {
            if (_animator == null)
            {
                Bind(gameObject);
            }
        }

        // 모델 프리팹이 생성된 직후 호출한다. 모델 안 어디에 Animator가 있든 찾아서 물린다.
        public void Bind(GameObject modelRoot)
        {
            _hasSpeed = false;
            _hasMoving = false;
            _animator = modelRoot != null ? modelRoot.GetComponentInChildren<Animator>(true) : null;
            if (_animator == null)
            {
                return;
            }

            // 이동은 NavMeshAgent(또는 트윈)가 담당한다 — 루트 모션이 켜진 모델을 꽂으면
            // 둘이 서로 밀어서 캐릭터가 엉뚱하게 밀리거나 제자리걸음한다. 모델 팩마다
            // 기본값이 달라서 여기서 못 박는다.
            _animator.applyRootMotion = false;

            CacheParameters();
        }

        void CacheParameters()
        {
            _hasSpeed = TryGetParameterHash(speedParameter, AnimatorControllerParameterType.Float, out _speedHash);
            _hasMoving = TryGetParameterHash(movingParameter, AnimatorControllerParameterType.Bool, out _movingHash);
        }

        // 없는 파라미터에 SetFloat/SetBool을 부르면 매 프레임 에러 로그가 쏟아진다.
        // 모델마다 파라미터 이름이 제각각이라, 실제로 있는 것만 골라서 쓴다.
        bool TryGetParameterHash(string parameterName, AnimatorControllerParameterType type, out int hash)
        {
            hash = 0;
            if (_animator == null || string.IsNullOrEmpty(parameterName))
            {
                return false;
            }

            foreach (var parameter in _animator.parameters)
            {
                if (parameter.type == type && parameter.name == parameterName)
                {
                    hash = parameter.nameHash;
                    return true;
                }
            }

            return false;
        }

        void Update()
        {
            float speed = MeasureSpeed();
            if (_animator == null)
            {
                return;
            }

            if (_hasSpeed)
            {
                float value = normalizeSpeed && _referenceSpeed > 0.01f
                    ? Mathf.Clamp01(speed / _referenceSpeed)
                    : speed;
                _animator.SetFloat(_speedHash, value, speedDampTime, Time.deltaTime);
            }

            if (_hasMoving)
            {
                _animator.SetBool(_movingHash, speed > movingThreshold);
            }
        }

        float MeasureSpeed()
        {
            Vector3 position = transform.position;
            // 수평 이동만 센다 — 경사면을 오르내릴 때 생기는 Y 변화까지 더하면 같은 조작에도
            // 지형에 따라 속도가 달라진다.
            Vector3 delta = position - _lastPosition;
            delta.y = 0f;
            _lastPosition = position;

            if (Time.deltaTime <= 0f)
            {
                return 0f;
            }

            float distance = delta.magnitude;
            return distance > teleportDistance ? 0f : distance / Time.deltaTime;
        }

        // 이벤트 연출(손 흔들기 등)에서 쓰라고 열어둔 통로. 파라미터가 없는 모델에
        // 호출해도 안전하다.
        public void SetTrigger(string triggerName)
        {
            if (!TryGetParameterHash(triggerName, AnimatorControllerParameterType.Trigger, out int hash))
            {
                Debug.LogWarning($"[CharacterAnimatorDriver] {name}의 Animator에 '{triggerName}' 트리거가 없습니다.");
                return;
            }

            _animator.SetTrigger(hash);
        }
    }
}
