using System;
using DG.Tweening;
using UnityEngine;

namespace Mountains
{
    // DOTween 트윈 하나를 EventTrigger 스텝으로 감싸는 액션들의 공통 기반. 대상/시간/ease/
    // 반복처럼 모든 트윈이 공유하는 옵션과, "트윈이 끝날 때까지 다음 스텝을 막을지"를
    // 여기서 한 번만 처리한다 — 액션마다 같은 코드를 두면 완료 통보를 빠뜨리기 쉽고,
    // 그러면 시퀀스가 그 자리에서 영원히 멈춘다.
    public abstract class LegacyTweenActionBase : LegacyTriggerAction
    {
        [Tooltip("비워두면 이 액션이 붙어있는 오브젝트를 대상으로 삼는다.")]
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
        [Tooltip("켜두면 트윈이 끝날 때까지 다음 스텝으로 넘어가지 않는다. 꺼두면 즉시 완료 " +
            "처리하고 트윈은 뒤에서 계속 돈다(배경 연출).")]
        public bool waitForComplete = true;

        // Punch/Shake/Jump는 DOTween이 내부적으로 자기 ease를 쓰는 트윈이라, 겉에서 ease를
        // 덮어쓰면 흔들림/점프 모양이 망가진다 — 그런 액션은 이 값을 false로 덮어쓴다.
        protected virtual bool UsesEase => true;

        protected Transform ResolveTarget()
        {
            return target != null ? target : transform;
        }

        public override void Execute(Action onComplete)
        {
            var tween = CreateTween(ResolveTarget());
            if (tween == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (UsesEase)
            {
                tween.SetEase(ease);
            }
            tween.SetDelay(delay).SetLoops(loops, loopType);

            if (!waitForComplete || loops < 0)
            {
                onComplete?.Invoke();
                return;
            }

            // OnComplete가 아니라 OnKill을 쓴다 — 연출 도중 대상이 파괴되면 트윈은 완료가
            // 아니라 Kill로 끝나고, 그때 OnComplete만 걸어뒀으면 콜백이 영영 안 온다.
            bool finished = false;
            tween.OnKill(() =>
            {
                if (finished)
                {
                    return;
                }
                finished = true;
                onComplete?.Invoke();
            });
        }

        // null을 돌려주면 "할 일이 없다"로 보고 즉시 완료 처리한다(필요한 참조가 비어 있는 경우).
        protected abstract Tween CreateTween(Transform target);
    }
}
