using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.AI;

namespace Mountains
{
    // "플레이어를 destination으로 옮긴 뒤 그 방향으로 돌린다"의 공통 뼈대.
    // 이동 방식만 파생 클래스가 정한다 — NavMesh로 걸어가거나(MoveThenRotatePlayerAction),
    // 직선으로 보간하거나(TweenMoveThenRotatePlayerAction).
    [Serializable]
    public abstract class MoveThenRotatePlayerActionBase : TriggerAction
    {
        [Tooltip("비워두면 Player 태그로 자동 탐색한다.")]
        public Transform player;
        [Tooltip("이동할 위치/방향.")]
        [GizmoTarget("플레이어 이동")] public Transform destination;

        [Header("회전")]
        public float rotateDuration = 0.5f;
        public Ease rotateEase = Ease.OutQuad;

        public override async UniTask ExecuteAsync(TriggerContext context, CancellationToken cancellationToken)
        {
            var target = PlayerLocator.Resolve(player);
            if (target == null || destination == null)
            {
                Debug.LogWarning($"[{GetType().Name}] player 또는 destination이 없어 이동을 건너뜁니다.");
                return;
            }

            var agent = target.GetComponent<NavMeshAgent>();

            await MoveAsync(target, agent, cancellationToken);
            await RotateAsync(target, agent, cancellationToken);
        }

        protected abstract UniTask MoveAsync(Transform target, NavMeshAgent agent,
            CancellationToken cancellationToken);

        // 회전 중에는 에이전트의 자동 회전을 꺼둔다 — 도착 직후 잔여 속도가 남아 있으면
        // 에이전트가 진행 방향으로 돌리려 해서 트윈과 서로 밀어낸다.
        async UniTask RotateAsync(Transform target, NavMeshAgent agent, CancellationToken cancellationToken)
        {
            if (target == null)
            {
                return;
            }

            bool restoreRotation = agent != null && agent.updateRotation;
            if (restoreRotation)
            {
                agent.updateRotation = false;
            }

            try
            {
                await target.DORotateQuaternion(destination.rotation, rotateDuration).SetEase(rotateEase)
                    .AwaitKill(cancellationToken);
            }
            finally
            {
                if (restoreRotation && agent != null)
                {
                    agent.updateRotation = true;
                }
            }
        }
    }
}
