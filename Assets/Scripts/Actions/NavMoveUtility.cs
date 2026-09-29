using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

namespace Mountains
{
    // NavMeshAgent로 한 지점까지 걸어가고 도착할 때까지 기다리는 공통 코드.
    //
    // 두 액션(MovePlayerToPointAction, MoveThenRotatePlayerAction)이 같은 "걸어가서 도착을
    // 기다린다"를 필요로 하는데, 도착 판정에 미묘한 조건이 여럿 있어(경로 계산 대기, 감속
    // 중 오판, 목적지 초기화) 각자 구현하면 한쪽만 고치기 쉽다.
    public static class NavMoveUtility
    {
        // arrivalDistance가 0보다 크면 에이전트의 stoppingDistance도 그 값으로 맞춘다.
        //
        // faceMoveDirection: 이동 중에 진행 방향을 바라보게 할지. 플레이어는 평소 회전을
        // 드래그 입력이 담당해서 CharacterMovement가 agent.updateRotation을 꺼둔다 — 그대로
        // 두면 걸어가는 동안 몸이 안 돌아 옆으로 미끄러지듯 보인다. 이동 동안만 켰다가
        // 원래 값으로 돌려놓는다(회전 속도는 에이전트의 angularSpeed를 그대로 쓴다).
        public static async UniTask MoveAsync(NavMeshAgent agent, Vector3 destination,
            float arrivalDistance, float timeout, CancellationToken cancellationToken,
            bool faceMoveDirection = true)
        {
            if (agent == null)
            {
                return;
            }

            if (arrivalDistance > 0f)
            {
                agent.stoppingDistance = arrivalDistance;
            }

            bool previousUpdateRotation = agent.updateRotation;
            if (faceMoveDirection)
            {
                agent.updateRotation = true;
            }

            agent.isStopped = false;
            agent.SetDestination(destination);

            // 경로 계산이 끝날 때까지는(pathPending) remainingDistance가 아직 유효하지 않다.
            while (agent != null && agent.pathPending)
            {
                await UniTask.Yield(cancellationToken);
            }

            float elapsed = 0f;
            while (agent != null && !HasArrived(agent))
            {
                elapsed += Time.deltaTime;
                if (timeout > 0f && elapsed >= timeout)
                {
                    break;
                }
                await UniTask.Yield(cancellationToken);
            }

            // 목적지를 그대로 두면 NavMeshAgent가 계속 그쪽으로 끌어당겨 이후 수동 조작이
            // 이상해진다 — 도착(혹은 타임아웃) 후에는 반드시 경로를 지운다.
            // 회전 권한도 원래 주인(드래그 입력)에게 돌려준다.
            if (agent != null)
            {
                agent.ResetPath();
                agent.updateRotation = previousUpdateRotation;
            }
        }

        // remainingDistance만 보면 아직 감속 중인데도 도착으로 처리되는 경우가 있어
        // 속도가 거의 0인 것까지 함께 확인한다.
        public static bool HasArrived(NavMeshAgent agent)
        {
            return agent.remainingDistance <= agent.stoppingDistance
                && (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f);
        }
    }
}
