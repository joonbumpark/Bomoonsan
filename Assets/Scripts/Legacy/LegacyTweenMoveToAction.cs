using System;
using DG.Tweening;
using UnityEngine;

namespace Mountains
{
    // DOTween으로 target을 destination 위치까지 부드럽게 이동시키는 액션. 이동이 끝날
    // 때까지(Tween 완료 콜백) onComplete를 미룬다 — 문/발판 같은 연출을 시퀀스에 끼워 넣을 때 쓴다.
    // 새로 붙이지 못하게 Add Component 메뉴에서 감춘다 — 이미 붙어 있는
    // 컴포넌트는 그대로 동작하므로 마이그레이션에는 영향이 없다.
    [AddComponentMenu("")]
    public class LegacyTweenMoveToAction : LegacyTriggerAction
    {
        [Tooltip("비워두면 이 액션이 붙어있는 오브젝트를 직접 움직인다.")]
        public Transform target;
        [Tooltip("이동할 목표 위치.")]
        public Transform destination;
        public float duration = 1f;
        public Ease ease = Ease.OutQuad;

        public override void Execute(Action onComplete)
        {
            var t = target != null ? target : transform;
            if (destination == null)
            {
                Debug.LogWarning("[LegacyTweenMoveToAction] destination이 없어 이동을 건너뜁니다.");
                onComplete?.Invoke();
                return;
            }

            t.DOMove(destination.position, duration)
                .SetEase(ease)
                .OnComplete(() => onComplete?.Invoke());
        }
    }
}
