using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Mountains
{
    // 플레이어를 NavMesh 위에서 지정한 지점까지 이동시키는 액션. 도착할 때까지(또는
    // timeout이 지날 때까지) onComplete를 미룬다 — 컷씬/튜토리얼에서 "여기로 가라"를
    // 강제로 유도하는 용도로 쓴다.
    public class MovePlayerToPointAction : TriggerAction
    {
        [Tooltip("비워두면 태그로 자동 탐색한다.")]
        public Transform player;
        [Tooltip("이동할 목표 지점.")]
        public Transform destination;
        [Tooltip("0보다 크면 이 거리를 도착 판정 기준으로 쓰고, 플레이어 NavMeshAgent의 " +
            "Stopping Distance도 이 값으로 맞춘다. 0이면 NavMeshAgent에 이미 설정된 값을 그대로 쓴다.")]
        public float arrivalDistance = 0f;
        [Tooltip("경로가 막혀 영원히 도착 못 하는 경우를 대비한 안전장치(초). 0이면 제한 없음.")]
        public float timeout = 10f;

        public override void Execute(Action onComplete)
        {
            var target = player;
            if (target == null)
            {
                var found = GameObject.FindGameObjectWithTag("Player");
                if (found != null)
                {
                    target = found.transform;
                }
            }

            var agent = target != null ? target.GetComponent<NavMeshAgent>() : null;
            if (agent == null || destination == null)
            {
                Debug.LogWarning("[MovePlayerToPointAction] player 또는 destination이 없어 이동을 건너뜁니다.");
                onComplete?.Invoke();
                return;
            }

            if (arrivalDistance > 0f)
            {
                agent.stoppingDistance = arrivalDistance;
            }
            agent.updateRotation = true;
            agent.isStopped = false;
            agent.SetDestination(destination.position);
            StartCoroutine(WaitUntilArrival(agent, onComplete));
        }

        IEnumerator WaitUntilArrival(NavMeshAgent agent, Action onComplete)
        {
            // 경로 계산이 끝날 때까지는(pathPending) remainingDistance가 아직 유효하지 않다.
            while (agent != null && agent.pathPending)
            {
                yield return null;
            }

            float elapsed = 0f;
            while (agent != null && !HasArrived(agent))
            {
                elapsed += Time.deltaTime;
                if (timeout > 0f && elapsed >= timeout)
                {
                    break;
                }
                yield return null;
            }

            // SetDestination으로 잡은 목적지를 그대로 두면 NavMeshAgent가 계속 그 지점을
            // "가야 할 곳"으로 여겨서, 이후 조이스틱으로 움직여도(CharacterMovement의
            // agent.Move()와는 별개로) 내부 경로 추적이 계속 그 지점 쪽으로 끌어당긴다 —
            // 도착(혹은 타임아웃)했으면 경로를 반드시 지워줘야 정상적인 수동 조작으로 돌아간다.
            if (agent != null)
            {
                agent.ResetPath();
            }

            onComplete?.Invoke();
        }

        // remainingDistance만 보면 아직 감속 중(velocity가 남아있음)인데도 도착으로
        // 처리되는 경우가 있어, 속도가 거의 0인 것까지 같이 확인한다.
        static bool HasArrived(NavMeshAgent agent)
        {
            return agent.remainingDistance <= agent.stoppingDistance
                && (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f);
        }
    }
}
