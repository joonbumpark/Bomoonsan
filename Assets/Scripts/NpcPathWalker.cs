using UnityEngine;
using UnityEngine.AI;

namespace Mountains
{
    // NpcFollower(플레이어를 계속 쫓아옴)와 반대로, 평소엔 제자리에 머물다가 플레이어가
    // detectRadius 안에 들어오면 pathPoints를 순서대로 따라 걷는 NPC — 안내원/순찰 NPC 등에 쓴다.
    [RequireComponent(typeof(NavMeshAgent))]
    public class NpcPathWalker : MonoBehaviour
    {
        [Tooltip("비워두면 태그로 자동 탐색한다.")]
        public Transform player;

        [Header("감지")]
        [Tooltip("플레이어가 이 거리 안에 들어오면 경로를 따라 이동하기 시작한다.")]
        public float detectRadius = 10f;
        [Tooltip("이동 중에 플레이어가 이 거리보다 멀어지면 멈춘다. detectRadius보다 커야 " +
            "두 경계 사이를 오갈 때 켜졌다 꺼졌다 하지 않는다.")]
        public float stopDetectRadius = 14f;
        [Tooltip("거리 판정 주기(초).")]
        public float updateInterval = 0.3f;

        [Header("경로")]
        [Tooltip("순서대로 따라갈 지점들.")]
        public Transform[] pathPoints;
        [Tooltip("마지막 지점에 도착하면 처음으로 돌아가 반복할지. 꺼두면 마지막 지점에서 멈춘다.")]
        public bool loop = true;
        [Tooltip("loop가 꺼져 있을 때, 마지막 지점에 도착하면 이 NPC를 파괴할지.")]
        public bool destroyOnArrival = false;

        NavMeshAgent _agent;
        int _currentIndex;
        bool _isMoving;
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
                // Awake 시점엔 아직 플레이어가 스폰되기 전일 수 있다(Player도 CharacterManager가
                // 런타임에 만든다) — player가 비어 있으면 매 프레임 값싸게 재시도한다.
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

            float distance = Vector3.Distance(transform.position, player.position);

            if (_isMoving)
            {
                if (distance > stopDetectRadius)
                {
                    _isMoving = false;
                    _agent.isStopped = true;
                    _agent.ResetPath();
                    return;
                }
            }
            else if (distance <= detectRadius)
            {
                _isMoving = true;
                _agent.isStopped = false;
                GoToPoint(_currentIndex);
            }
            else
            {
                return;
            }

            if (pathPoints == null || pathPoints.Length == 0)
            {
                return;
            }

            // 목적지 도착 판정. velocity가 거의 0인지까지 같이 보는 방식도 고려했지만,
            // Obstacle Avoidance(다른 NavMeshAgent 근처)가 켜져 있으면 도착해서 멈춘
            // 뒤에도 회피 때문에 속도가 완전히 0으로 안 떨어지고 계속 미세하게 흔들려서
            // 다음 지점으로 영영 못 넘어가는 문제가 있었다 — remainingDistance만으로 판정한다.
            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
            {
                AdvanceToNextPoint();
            }
        }

        void GoToPoint(int index)
        {
            if (pathPoints == null || index < 0 || index >= pathPoints.Length || pathPoints[index] == null)
            {
                return;
            }
            _agent.SetDestination(pathPoints[index].position);
        }

        void AdvanceToNextPoint()
        {
            if (pathPoints == null || pathPoints.Length == 0)
            {
                return;
            }

            _currentIndex++;
            if (_currentIndex >= pathPoints.Length)
            {
                if (!loop)
                {
                    _isMoving = false;
                    _agent.isStopped = true;
                    if (destroyOnArrival)
                    {
                        Destroy(gameObject);
                    }
                    return;
                }
                _currentIndex = 0;
            }
            GoToPoint(_currentIndex);
        }
    }
}
