using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.AI;

namespace Mountains
{
    // TeleportPlayerAction과 같은 대상(플레이어를 destination 위치/방향으로 이동)이지만,
    // 즉시 순간이동 대신 DOTween으로 먼저 위치를 이동시키고, 그게 끝난 뒤에 방향을
    // 회전시킨다 — 두 동작이 순서대로 자연스럽게 이어지는 연출용.
    public class MoveThenRotatePlayerAction : TriggerAction
    {
        [Tooltip("비워두면 태그로 자동 탐색한다.")]
        public Transform player;
        [Tooltip("이동할 위치/방향.")]
        public Transform destination;

        [Header("이동")]
        public float moveDuration = 1f;
        public Ease moveEase = Ease.OutQuad;

        [Header("회전")]
        public float rotateDuration = 0.5f;
        public Ease rotateEase = Ease.OutQuad;

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

            if (target == null || destination == null)
            {
                Debug.LogWarning("[MoveThenRotatePlayerAction] player 또는 destination이 없어 이동을 건너뜁니다.");
                onComplete?.Invoke();
                return;
            }

            // NavMeshAgent가 활성 상태면 매 프레임 transform.position을 자기 내부 상태로
            // 끌어당기려 해서 트윈과 서로 어긋난다 — 트윈 동안은 에이전트의 위치 갱신을
            // 꺼두고, 끝나면 Warp로 최종 위치에 다시 동기화한다.
            var agent = target.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.isStopped = true;
                agent.updatePosition = false;
            }

            target.DOMove(destination.position, moveDuration)
                .SetEase(moveEase)
                .OnComplete(() =>
                {
                    target.DORotateQuaternion(destination.rotation, rotateDuration)
                        .SetEase(rotateEase)
                        .OnComplete(() =>
                        {
                            if (agent != null)
                            {
                                agent.Warp(target.position);
                                agent.updatePosition = true;
                                agent.isStopped = false;
                            }
                            onComplete?.Invoke();
                        });
                });
        }
    }
}
