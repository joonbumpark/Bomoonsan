using UnityEngine;
using UnityEngine.AI;

namespace Mountains
{
    // NavMesh 위에서 플레이어를 거리 기반으로 따라다니는 가장 기본적인 NPC. 나중에
    // "특정 위치로 플레이어를 유도" 같은 모드를 얹어 고도화할 예정이라, 지금은 팔로우
    // 하나만 확실하게 동작하게 만든다.
    [RequireComponent(typeof(NavMeshAgent))]
    public class NpcFollower : MonoBehaviour, INpcBrain
    {
        [Tooltip("비워두면 태그로 자동 탐색한다.")]
        public Transform player;

        [Tooltip("플레이어를 기준으로 한 로컬 오프셋(플레이어가 바라보는 방향 기준으로 " +
            "회전해서 적용됨). 예: (0,0,-2.5)면 플레이어 바로 뒤 2.5m. 여러 NPC가 같은 " +
            "플레이어를 따라올 때 이 값을 서로 다르게 주면 한 점에 몰리지 않고 각자 " +
            "자기 자리를 따라간다.")]
        public Vector3 followOffset = Vector3.zero;

        [Header("추적")]
        [Tooltip("플레이어가 이 거리 안에 들어오면 따라가기 시작한다.")]
        public float followRadius = 10f;
        [Tooltip("따라가는 중에 이 거리보다 멀어지면 멈춘다. followRadius보다 커야 " +
            "두 경계 사이를 오갈 때 따라가기가 켜졌다 꺼졌다 하지 않는다.")]
        public float stopFollowRadius = 14f;
        [Tooltip("목적지 갱신 주기(초). 매 프레임 SetDestination을 부르면 경로 재계산 " +
            "비용이 낭비된다.")]
        public float updateInterval = 0.3f;

        NavMeshAgent _agent;
        bool _isFollowing;
        float _nextUpdateTime;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();

            if (player == null)
            {
                var found = GameObject.FindGameObjectWithTag("Player");
                if (found != null)
                {
                    player = found.transform;
                }
            }
        }

        void Update()
        {
            if (player == null)
            {
                // Awake 시점엔 아직 GameManager가 플레이어를 스폰하기 전일 수 있다
                // (Player도 CharacterManager가 런타임에 만든다) — player가 비어 있으면
                // 매 프레임 값싸게 재시도한다.
                var found = GameObject.FindGameObjectWithTag("Player");
                if (found != null)
                {
                    player = found.transform;
                }
                if (player == null)
                {
                    return;
                }
            }

            if (Time.time < _nextUpdateTime)
            {
                return;
            }
            _nextUpdateTime = Time.time + updateInterval;

            // 따라가기 시작/중단 판정은 플레이어 본인과의 거리로 하고, 실제 목적지는
            // followOffset만큼 떨어진 "내 자리"로 잡는다 — 그래야 여러 NPC가 플레이어의
            // 정확히 같은 좌표로 몰려들어 서로 밀어내지 않는다.
            float distance = Vector3.Distance(transform.position, player.position);
            Vector3 targetPosition = player.position + player.rotation * followOffset;

            if (_isFollowing)
            {
                if (distance > stopFollowRadius)
                {
                    _isFollowing = false;
                    _agent.isStopped = true;
                    _agent.ResetPath();
                }
                else
                {
                    _agent.isStopped = false;
                    _agent.SetDestination(targetPosition);
                }
            }
            else if (distance <= followRadius)
            {
                _isFollowing = true;
                _agent.isStopped = false;
                _agent.SetDestination(targetPosition);
            }
        }
    }
}
