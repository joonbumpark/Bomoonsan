using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Mountains
{
    // DOTween으로 target을 destination 위치까지 부드럽게 이동시키는 액션.
    // TweenTransformAction의 Move와 같지만 예전 씬에서 쓰던 단순 형태를 그대로 유지한다.
    [Serializable]
    public class TweenMoveToAction : TriggerAction
    {
        [Tooltip("비워두면 트리거 자신을 움직인다.")]
        public Transform target;
        [Tooltip("이동할 목표 위치.")]
        [GizmoTarget("이동 목표")] public Transform destination;
        public float duration = 1f;
        public Ease ease = Ease.OutQuad;

        public override UniTask ExecuteAsync(TriggerContext context, CancellationToken cancellationToken)
        {
            var moving = target != null ? target : context.Transform;
            if (moving == null || destination == null)
            {
                Debug.LogWarning("[TweenMoveToAction] destination이 없어 이동을 건너뜁니다.");
                return UniTask.CompletedTask;
            }

            return moving.DOMove(destination.position, duration).SetEase(ease)
                .AwaitKill(cancellationToken);
        }
    }
}
