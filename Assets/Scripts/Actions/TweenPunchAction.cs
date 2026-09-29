using System;
using DG.Tweening;
using UnityEngine;

namespace Mountains
{
    // 한 방향으로 튕겼다 제자리로 돌아오는 연출(DOPunch*). 방향이 정해진 한 방에 쓴다 —
    // 무작위 흔들림은 TweenShakeAction 쪽이다.
    [Serializable]
    public class TweenPunchAction : TweenActionBase
    {
        [Header("펀치")]
        public TweenChannel channel = TweenChannel.Scale;
        [Tooltip("튕겨나갈 방향과 세기. Scale이면 각 축의 크기 변화량, Rotation이면 각도.")]
        public Vector3 punch = new Vector3(0f, 0.2f, 0f);
        [Tooltip("제자리로 돌아오는 동안 몇 번 진동할지.")]
        public int vibrato = 10;
        [Tooltip("클수록 목표를 지나쳐 반대쪽으로 넘어갔다 돌아온다.")]
        [Range(0f, 1f)] public float elasticity = 1f;

        // Punch는 DOTween이 진동 곡선을 직접 만드는 트윈이라 겉에서 ease를 덮어쓰면 깨진다.
        protected override bool UsesEase => false;

        protected override Tween CreateTween(Transform target)
        {
            switch (channel)
            {
                case TweenChannel.Position:
                    return target.DOPunchPosition(punch, duration, vibrato, elasticity);
                case TweenChannel.Rotation:
                    return target.DOPunchRotation(punch, duration, vibrato, elasticity);
                default:
                    return target.DOPunchScale(punch, duration, vibrato, elasticity);
            }
        }
    }
}
