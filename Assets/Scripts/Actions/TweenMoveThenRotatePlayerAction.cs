using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.AI;

namespace Mountains
{
    // 플레이어를 destination까지 직선으로 보간해 옮긴 뒤 그 방향으로 돈다.
    //
    // NavMesh를 무시하므로 지형이나 장애물을 통과하고 가감속도 평소 이동과 다르다 —
    // NavMesh 밖으로 나가야 하거나(절벽 끝, 배 위) "정확히 N초에 도착"이 필요한 컷씬에만
    // 쓰고, 평범한 이동은 MoveThenRotatePlayerAction을 쓴다.
    [Serializable]
    public class TweenMoveThenRotatePlayerAction : MoveThenRotatePlayerActionBase
    {
        [Header("이동")]
        public float moveDuration = 1f;
        public Ease moveEase = Ease.OutQuad;

        protected override async UniTask MoveAsync(Transform target, NavMeshAgent agent,
            CancellationToken cancellationToken)
        {
            // NavMeshAgent가 활성 상태면 매 프레임 위치를 자기 내부 상태로 끌어당겨 트윈과
            // 어긋난다 — 트윈 동안 위치 갱신을 꺼두고 끝나면 Warp로 다시 맞춘다.
            if (agent != null)
            {
                agent.isStopped = true;
                agent.updatePosition = false;
            }

            try
            {
                await target.DOMove(destination.position, moveDuration).SetEase(moveEase)
                    .AwaitKill(cancellationToken);
            }
            finally
            {
                // 중간에 취소돼도 에이전트를 되돌려놔야 한다 — 안 그러면 플레이어가 영영 못 움직인다.
                if (agent != null && target != null)
                {
                    agent.Warp(target.position);
                    agent.updatePosition = true;
                    agent.isStopped = false;
                }
            }
        }
    }
}
