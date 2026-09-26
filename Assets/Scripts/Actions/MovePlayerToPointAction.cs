using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

namespace Mountains
{
    // 플레이어를 NavMesh 위에서 지정한 지점까지 이동시키는 액션. 도착할 때까지(또는
    // timeout이 지날 때까지) 다음 스텝으로 넘어가지 않는다.
    [Serializable]
    public class MovePlayerToPointAction : TriggerAction
    {
        [Tooltip("비워두면 Player 태그로 자동 탐색한다.")]
        public Transform player;
        [Tooltip("이동할 목표 지점.")]
        [GizmoTarget("플레이어 이동")] public Transform destination;
        [Tooltip("0보다 크면 이 거리를 도착 판정 기준으로 쓰고, NavMeshAgent의 Stopping Distance도 맞춘다.")]
        public float arrivalDistance = 0f;
        [Tooltip("경로가 막혀 영원히 도착 못 하는 경우를 대비한 안전장치(초). 0이면 제한 없음.")]
        public float timeout = 10f;
        [Tooltip("이동하는 동안 진행 방향을 바라볼지. 끄면 현재 방향을 유지한 채 이동한다.")]
        public bool faceMoveDirection = true;

        public override UniTask ExecuteAsync(TriggerContext context, CancellationToken cancellationToken)
        {
            var target = PlayerLocator.Resolve(player);
            var agent = target != null ? target.GetComponent<NavMeshAgent>() : null;
            if (agent == null || destination == null)
            {
                Debug.LogWarning("[MovePlayerToPointAction] player 또는 destination이 없어 이동을 건너뜁니다.");
                return UniTask.CompletedTask;
            }

            return NavMoveUtility.MoveAsync(agent, destination.position, arrivalDistance, timeout,
                cancellationToken, faceMoveDirection);
        }
    }
}
