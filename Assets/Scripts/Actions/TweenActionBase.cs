using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Mountains
{
    // DOTween 트윈 하나를 스텝으로 감싸는 액션들의 공통 기반. 대상/시간/ease/반복처럼 모든
    // 트윈이 공유하는 옵션과 "트윈이 끝날 때까지 다음 스텝을 막을지"를 여기서 한 번만 처리한다.
    [Serializable]
    public abstract class TweenActionBase : TriggerAction
    {
        [Tooltip("비워두면 트리거 자신을 대상으로 삼는다.")]
        public Transform target;

        [Header("공통")]
        public float duration = 1f;
        public Ease ease = Ease.OutQuad;
        [Tooltip("시작 전 대기 시간(초).")]
        public float delay = 0f;
        [Tooltip("1이면 한 번, 2 이상이면 그만큼 반복. -1이면 무한 반복 — 무한 반복은 끝나지 " +
            "않으므로 waitForComplete와 상관없이 즉시 완료 처리한다.")]
        public int loops = 1;
        public LoopType loopType = LoopType.Restart;
        [Tooltip("켜두면 트윈이 끝날 때까지 다음 스텝으로 넘어가지 않는다. 꺼두면 즉시 넘어가고 " +
            "트윈은 뒤에서 계속 돈다(배경 연출).")]
        public bool waitForComplete = true;

        // Punch/Shake/Jump는 DOTween이 내부적으로 자기 ease를 쓰는 트윈이라, 겉에서 ease를
        // 덮어쓰면 흔들림/점프 모양이 망가진다 — 그런 액션은 이 값을 false로 덮어쓴다.
        protected virtual bool UsesEase => true;

        public override UniTask ExecuteAsync(TriggerContext context, CancellationToken cancellationToken)
        {
            var resolved = target != null ? target : context.Transform;
            if (resolved == null)
            {
                return UniTask.CompletedTask;
            }

            var tween = CreateTween(resolved);
            if (tween == null)
            {
                return UniTask.CompletedTask;
            }

            if (UsesEase)
            {
                tween.SetEase(ease);
            }
            tween.SetDelay(delay).SetLoops(loops, loopType);

            if (!waitForComplete || loops < 0)
            {
                return UniTask.CompletedTask;
            }

            return tween.AwaitKill(cancellationToken);
        }

        // null을 돌려주면 "할 일이 없다"로 보고 즉시 완료 처리한다.
        protected abstract Tween CreateTween(Transform target);
    }
}
