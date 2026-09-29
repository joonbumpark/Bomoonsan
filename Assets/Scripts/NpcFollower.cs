using UnityEngine;
using UnityEngine.AI;

namespace Mountains
{
    // NavMesh 위에서 플레이어를 따라다니는 가장 기본적인 NPC. 나중에 "특정 위치로 플레이어를
    // 유도" 같은 모드를 얹어 고도화할 예정이라, 지금은 팔로우 하나만 확실하게 동작하게 만든다.
    //
    // 흐름: 플레이어가 followRadius 안에 한 번 들어오면 합류하고, 그 뒤로는 멀어져도 포기하지
    // 않는다. 합류한 뒤에는 "내 자리"(플레이어 + followOffset)까지의 거리로 두 상태를 오간다.
    //   - 대기(Idle): 자리에서 followStartDistance보다 멀어지면 따라가기로.
    //   - 따라가기(Following): 자리에서 arriveDistance 안에 들어오면 대기로.
    // 두 거리를 따로 두는 이유: 같은 거리 하나로 판정하면 플레이어가 조금만 움직여도 경계를
    // 넘나들며 섰다 걸었다를 반복한다. 사이에 여유를 둬야 도착하면 확실히 서고, 플레이어가
    // 충분히 벗어났을 때만 다시 출발한다.
    //
    // 예전엔 플레이어가 stopFollowRadius 밖으로 벌어지면 따라가기를 포기했는데, NPC(3.5)가
    // 플레이어(4)보다 느려서 오래 걸으면 간격이 계속 벌어지다 결국 멈춰 버렸다. 포기 판정을
    // 없애고, 자리에서 멀수록 속도를 올려(따라잡기) 간격이 일정하게 유지되게 했다.
    [RequireComponent(typeof(NavMeshAgent))]
    public class NpcFollower : MonoBehaviour, INpcBrain
    {
        enum State
        {
            // 아직 플레이어가 followRadius 안에 들어온 적이 없다.
            Waiting,
            Idle,
            Following,
        }

        [Tooltip("비워두면 태그로 자동 탐색한다.")]
        public Transform player;

        [Tooltip("플레이어를 기준으로 한 로컬 오프셋(플레이어가 바라보는 방향 기준으로 " +
            "회전해서 적용됨). 예: (0,0,-2.5)면 플레이어 바로 뒤 2.5m. 여러 NPC가 같은 " +
            "플레이어를 따라올 때 이 값을 서로 다르게 주면 한 점에 몰리지 않고 각자 " +
            "자기 자리를 따라간다.")]
        public Vector3 followOffset = Vector3.zero;

        [Header("합류")]
        [Tooltip("플레이어가 이 거리 안에 한 번 들어오면 합류한다. 합류한 뒤로는 아무리 " +
            "멀어져도 계속 따라온다.")]
        public float followRadius = 10f;

        [Header("따라가기 / 대기")]
        [Tooltip("대기 중에 내 자리(플레이어 + followOffset)에서 이 거리보다 멀어지면 따라가기 시작한다.")]
        [Min(0.1f)] public float followStartDistance = 2.5f;
        [Tooltip("따라가는 중에 내 자리에서 이 거리 안에 들어오면 멈추고 대기한다. " +
            "followStartDistance보다 작아야 섰다 걸었다를 반복하지 않는다.")]
        [Min(0.05f)] public float arriveDistance = 0.8f;
        [Tooltip("따라가는 중 목적지 갱신 주기(초). 매 프레임 SetDestination을 부르면 경로 재계산 " +
            "비용이 낭비되고, 너무 길면 움직이는 플레이어의 옛 위치를 쫓느라 뒤처진다.")]
        [Min(0.02f)] public float updateInterval = 0.2f;

        [Header("따라잡기")]
        [Tooltip("내 자리에서 이 거리만큼 떨어지면 최고 배율로 달린다. 그 사이는 거리에 비례해 올라간다.")]
        [Min(0.1f)] public float catchUpDistance = 6f;
        [Tooltip("기본 속도에 곱하는 최대 배율. 1이면 따라잡기를 끈다.")]
        [Min(1f)] public float maxSpeedMultiplier = 1.6f;

        NavMeshAgent _agent;
        State _state = State.Waiting;
        float _nextUpdateTime;
        // 에이전트 원래 속도. 따라잡기로 바꾼 속도를 꺼질 때 되돌리는 데도 쓴다 — 등장/퇴장
        // 연출(NpcTweenUtility)이 이 컴포넌트를 끄고 NavMoveUtility로 걷게 할 때 빨라진
        // 속도가 남아 있으면 안 된다.
        float _agentBaseSpeed;
        CharacterMovement _playerMovement;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agentBaseSpeed = _agent.speed;
            TryFindPlayer();
        }

        void OnEnable()
        {
            // 연출로 꺼졌다 켜지면 그동안 자리가 바뀌었을 수 있다 — 대기로 두고 거리부터 다시
            // 잰다(합류 여부는 유지). 멀어졌으면 다음 프레임에 바로 따라가기로 넘어간다.
            if (_state == State.Following)
            {
                _state = State.Idle;
            }
        }

        void OnDisable()
        {
            if (_agent != null)
            {
                _agent.speed = _agentBaseSpeed;
            }
        }

        void OnValidate()
        {
            // 두 거리가 뒤집히면 도착하자마자 다시 출발 조건이 되어 제자리에서 떤다.
            arriveDistance = Mathf.Min(arriveDistance, followStartDistance * 0.9f);
            catchUpDistance = Mathf.Max(catchUpDistance, followStartDistance);
        }

        void Update()
        {
            // Awake 시점엔 아직 GameManager가 플레이어를 스폰하기 전일 수 있다
            // (Player도 CharacterManager가 런타임에 만든다) — 비어 있으면 매 프레임 재시도한다.
            if (player == null && !TryFindPlayer())
            {
                return;
            }

            Vector3 slot = player.position + player.rotation * followOffset;
            float slotDistance = HorizontalDistance(transform.position, slot);

            switch (_state)
            {
                case State.Waiting:
                    if (HorizontalDistance(transform.position, player.position) <= followRadius)
                    {
                        _state = State.Idle;
                    }
                    break;

                case State.Idle:
                    if (slotDistance > followStartDistance)
                    {
                        StartFollowing(slot);
                    }
                    break;

                case State.Following:
                    if (slotDistance <= arriveDistance)
                    {
                        StopFollowing();
                        break;
                    }

                    _agent.speed = CatchUpSpeed(slotDistance);
                    if (Time.time >= _nextUpdateTime)
                    {
                        _nextUpdateTime = Time.time + updateInterval;
                        _agent.SetDestination(slot);
                    }
                    break;
            }
        }

        void StartFollowing(Vector3 slot)
        {
            _state = State.Following;
            // 도착 판정은 arriveDistance로 여기서 한다. 에이전트의 stoppingDistance(프리팹 1.5)가
            // 그보다 크면 에이전트가 먼저 서 버려 arriveDistance 안으로 못 들어오고, 선 채로
            // 따라가기 상태에 갇힌다 — 그보다 작게 맞춘다.
            _agent.stoppingDistance = arriveDistance * 0.5f;
            _agent.isStopped = false;
            _agent.SetDestination(slot);
            _nextUpdateTime = Time.time + updateInterval;
        }

        void StopFollowing()
        {
            _state = State.Idle;
            _agent.speed = _agentBaseSpeed;
            _agent.isStopped = true;
            _agent.ResetPath();
        }

        // 자리 근처에선 플레이어 걸음에 맞추고, 벌어질수록 maxSpeedMultiplier까지 빨라진다.
        // 기본 속도를 플레이어 이동 속도에 맞추는 이유: 에이전트 속도(3.5)가 플레이어(4)보다
        // 느리면 따라잡기가 있어도 "빨라진 속도 = 플레이어 속도"가 되는 거리까지 항상 뒤처진 채
        // 걷는다. 기준을 맞춰두면 자리 가까이에서 따라온다.
        float CatchUpSpeed(float slotDistance)
        {
            float baseSpeed = _playerMovement != null
                ? Mathf.Max(_agentBaseSpeed, _playerMovement.moveSpeed)
                : _agentBaseSpeed;
            float t = Mathf.InverseLerp(arriveDistance, catchUpDistance, slotDistance);
            return baseSpeed * Mathf.Lerp(1f, maxSpeedMultiplier, t);
        }

        bool TryFindPlayer()
        {
            if (player == null)
            {
                var found = GameObject.FindGameObjectWithTag("Player");
                if (found == null)
                {
                    return false;
                }
                player = found.transform;
            }

            _playerMovement = player.GetComponent<CharacterMovement>();
            return true;
        }

        // 경사진 지형에서 높이 차이가 거리로 잡히면 자리 바로 옆인데도 도착 판정이 안 난다.
        static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
