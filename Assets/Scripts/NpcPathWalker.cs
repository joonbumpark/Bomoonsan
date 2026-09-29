using UnityEngine;
using UnityEngine.AI;

namespace Mountains
{
    // NpcFollower(플레이어를 계속 쫓아옴)와 반대로, 평소엔 제자리에 머물다가 플레이어가
    // detectRadius 안에 들어오면 pathPoints를 순서대로 따라 걷는 NPC — 안내원/순찰 NPC 등에 쓴다.
    [RequireComponent(typeof(NavMeshAgent))]
    public class NpcPathWalker : MonoBehaviour, INpcBrain
    {
        [Tooltip("비워두면 태그로 자동 탐색한다.")]
        public Transform player;

        [Header("출발 조건")]
        [Tooltip("켜두면 플레이어가 detectRadius 안에 들어와야 출발하고, 멀어지면 멈춘다. " +
            "끄면 플레이어와 무관하게 스폰 즉시 걷기 시작하고 도중에 멈추지 않는다(순찰/배경 NPC).")]
        public bool waitForPlayer = true;

        [Tooltip("서 있는 동안 플레이어 쪽을 바라볼지. waitForPlayer 모드에서만 의미가 있다 — " +
            "안내 NPC가 플레이어가 다가오는 것을 지켜보는 느낌을 준다.")]
        public bool lookAtPlayerWhenIdle = true;
        [Tooltip("돌아보는 속도(도/초). 0이면 즉시 돌아본다.")]
        [Min(0f)] public float lookAtSpeed = 180f;

        [Header("감지")]
        [Tooltip("waitForPlayer가 켜져 있을 때만 쓰인다. 플레이어가 이 거리 안에 들어오면 이동을 시작한다.")]
        public float detectRadius = 10f;
        [Tooltip("이동 중에 플레이어가 이 거리보다 멀어지면 멈춘다. detectRadius보다 커야 " +
            "두 경계 사이를 오갈 때 켜졌다 꺼졌다 하지 않는다.")]
        public float stopDetectRadius = 14f;
        [Tooltip("거리 판정 주기(초).")]
        public float updateInterval = 0.3f;

        [Header("경로")]
        [Tooltip("순서대로 따라갈 지점들.")]
        public Transform[] pathPoints;
        [Tooltip("경로를 몇 바퀴 돌지. -1이면 무한 반복, 1이면 한 바퀴 돌고 마지막 지점에서 " +
            "멈춘다(0도 한 바퀴 — 출발한 이상 경로 끝까지는 간다). 다 돌면 destroyOnArrival이 적용된다.")]
        [Min(-1)] public int loopCount = -1;
        [Tooltip("경로를 다 돈 뒤 이 NPC를 파괴할지. loopCount가 -1(무한)이면 쓰이지 않는다.")]
        public bool destroyOnArrival = false;

        NavMeshAgent _agent;
        int _currentIndex;
        int _completedLoops;
        // 경로를 다 돈 상태. 이게 없으면 멈춘 뒤에도 플레이어가 다가올 때마다 처음부터
        // 다시 걷기 시작한다.
        bool _finished;
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
            // 플레이어와 무관한 모드: 첫 Update에 바로 출발하고 거리로 멈추지 않는다.
            // Awake가 아니라 Update에서 출발시키는 이유 — 스폰 직후에는 CharacterManager가
            // 아직 NavMesh 위로 Warp하기 전이라(에이전트가 NavMesh에 안 붙은 상태) 그 시점의
            // SetDestination은 무시된다.
            if (!waitForPlayer)
            {
                if (!_isMoving && !_finished)
                {
                    _isMoving = true;
                    _agent.isStopped = false;
                    GoToPoint(_currentIndex);
                }

                if (Time.time < _nextUpdateTime)
                {
                    return;
                }
                _nextUpdateTime = Time.time + updateInterval;

                CheckArrival();
                return;
            }

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

            // 회전은 매 프레임 돌아야 부드럽다 — 아래 주기 게이트(updateInterval)보다 앞에 둔다.
            // _isMoving 자체는 주기마다만 바뀌므로 판정 비용이 늘지는 않는다.
            if (!_isMoving && lookAtPlayerWhenIdle)
            {
                FaceTowards(player.position);
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
            else if (!_finished && distance <= detectRadius)
            {
                _isMoving = true;
                _agent.isStopped = false;
                GoToPoint(_currentIndex);
            }
            else
            {
                return;
            }

            CheckArrival();
        }

        // 목적지 도착 판정. velocity가 거의 0인지까지 같이 보는 방식도 고려했지만,
        // Obstacle Avoidance(다른 NavMeshAgent 근처)가 켜져 있으면 도착해서 멈춘 뒤에도
        // 회피 때문에 속도가 완전히 0으로 안 떨어지고 계속 미세하게 흔들려서 다음 지점으로
        // 영영 못 넘어가는 문제가 있었다 — remainingDistance만으로 판정한다.
        void CheckArrival()
        {
            if (pathPoints == null || pathPoints.Length == 0)
            {
                return;
            }

            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
            {
                AdvanceToNextPoint();
            }
        }

        // 수평으로만 돌린다 — y를 남겨두면 플레이어가 언덕 위에 있을 때 NPC가 고개를 젖힌다.
        // NavMeshAgent는 경로를 따라 움직일 때만 회전을 건드리므로, 멈춰 있는 지금은
        // transform.rotation을 직접 써도 서로 싸우지 않는다.
        void FaceTowards(Vector3 worldPosition)
        {
            Vector3 direction = worldPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = lookAtSpeed > 0f
                ? Quaternion.RotateTowards(transform.rotation, targetRotation, lookAtSpeed * Time.deltaTime)
                : targetRotation;
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
                _completedLoops++;

                // -1이면 무한, 0 이상이면 그 바퀴 수를 채웠을 때 끝낸다.
                // 0은 1로 취급한다 — 경로 중간에서 멈추는 상태를 만들지 않기 위해서다
                // (아예 움직이지 않게 하려면 pathPoints를 비우거나 이 컴포넌트를 쓰지 않는다).
                bool finished = loopCount >= 0 && _completedLoops >= Mathf.Max(1, loopCount);
                if (finished)
                {
                    _finished = true;
                    _isMoving = false;
                    _agent.isStopped = true;
                    if (destroyOnArrival)
                    {
                        // 직접 Destroy하지 않고 공통 진입점을 거친다 — NPC 프리팹에
                        // NpcDespawnEffect가 붙어 있으면 트리거로 치울 때와 똑같은
                        // 소멸 연출(FX/트윈)이 여기서도 나온다.
                        NpcDespawner.Despawn(gameObject);
                    }
                    return;
                }
                _currentIndex = 0;
            }
            GoToPoint(_currentIndex);
        }
    }
}
