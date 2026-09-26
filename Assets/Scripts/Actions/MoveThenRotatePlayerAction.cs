using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

namespace Mountains
{
    // 플레이어가 destination까지 평소처럼 걸어간 뒤(NavMeshAgent 경로 추적) 그 방향으로 돈다.
    //
    // 직선 보간(TweenMoveThenRotatePlayerAction)과 달리 NavMesh를 따르므로 지형이나 장애물을
    // 뚫지 않고, 가감속과 걸음 애니메이션이 평소 이동과 같다 — 이 연출만 튀어 보이지 않는다.
    [Serializable]
    public class MoveThenRotatePlayerAction : MoveThenRotatePlayerActionBase
    {
        [Header("이동")]
        [Tooltip("도착 판정 거리. 0이면 에이전트에 설정된 Stopping Distance를 그대로 쓴다.")]
        public float arrivalDistance = 0f;
        [Tooltip("경로가 막혔을 때를 대비한 안전장치(초). 0이면 제한 없음.")]
        public float timeout = 10f;
        [Tooltip("이동하는 동안 진행 방향을 바라볼지. 끄면 현재 방향을 유지한 채 이동한다.")]
        public bool faceMoveDirection = true;

        protected override UniTask MoveAsync(Transform target, NavMeshAgent agent,
            CancellationToken cancellationToken)
        {
            if (agent == null)
            {
                Debug.LogWarning("[MoveThenRotatePlayerAction] NavMeshAgent가 없어 이동을 건너뜁니다. " +
                    "직선으로 옮기려면 TweenMoveThenRotatePlayerAction을 쓰세요.");
                return UniTask.CompletedTask;
            }

            return NavMoveUtility.MoveAsync(agent, destination.position, arrivalDistance, timeout,
                cancellationToken, faceMoveDirection);
        }
    }
}
