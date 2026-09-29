using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

namespace Mountains
{
    // 플레이어를 지정한 Transform의 위치/방향으로 즉시 순간이동시키는 액션 — NavMesh를
    // 걸어서 가는 MovePlayerToPointAction과 달리 그 자리에서 바로 옮긴다.
    [Serializable]
    public class TeleportPlayerAction : TriggerAction
    {
        [Tooltip("비워두면 Player 태그로 자동 탐색한다.")]
        public Transform player;
        [Tooltip("이동할 위치/방향.")]
        [GizmoTarget("순간이동")] public Transform destination;
        [Tooltip("destination의 회전(방향)도 같이 적용할지. 꺼두면 위치만 옮기고 방향은 그대로 둔다.")]
        public bool applyRotation = true;

        public override UniTask ExecuteAsync(TriggerContext context, CancellationToken cancellationToken)
        {
            var target = PlayerLocator.Resolve(player);
            if (target == null || destination == null)
            {
                Debug.LogWarning("[TeleportPlayerAction] player 또는 destination이 없어 이동을 건너뜁니다.");
                return UniTask.CompletedTask;
            }

            // transform.position을 직접 바꾸면 NavMeshAgent 내부 상태(현재 위치한 삼각형 등)와
            // 어긋난다 — Warp는 NavMesh 위 가장 가까운 지점으로 안전하게 순간이동시켜준다.
            var agent = target.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                if (!agent.Warp(destination.position))
                {
                    target.position = destination.position;
                    Debug.LogWarning("[TeleportPlayerAction] NavMesh 위로 정확히 놓지 못했습니다.");
                }
            }
            else
            {
                target.position = destination.position;
            }

            if (applyRotation)
            {
                target.rotation = destination.rotation;
            }

            return UniTask.CompletedTask;
        }
    }
}
