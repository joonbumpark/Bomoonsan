using System;
using DG.Tweening;
using UnityEngine;

namespace Mountains
{
    // 무작위 방향으로 떨리는 연출(DOShake*). 피격, 폭발, 지진처럼 방향이 정해지지 않은
    // 흔들림에 쓴다.
    [Serializable]
    public class TweenShakeAction : TweenActionBase
    {
        [Header("흔들기")]
        public TweenChannel channel = TweenChannel.Position;
        [Tooltip("축별 흔들림 세기. Rotation이면 각도.")]
        public Vector3 strength = new Vector3(0.3f, 0.3f, 0.3f);
        [Tooltip("초당 흔들리는 횟수.")]
        public int vibrato = 10;
        [Tooltip("0이면 한 축으로만, 90이면 사방으로 흔들린다.")]
        [Range(0f, 180f)] public float randomness = 90f;
        [Tooltip("켜두면 흔들림이 점점 약해지며 끝난다.")]
        public bool fadeOut = true;

        // Shake도 DOTween이 흔들림 곡선을 직접 만드는 트윈이라 ease를 덮어쓰면 깨진다.
        protected override bool UsesEase => false;

        protected override Tween CreateTween(Transform target)
        {
            switch (channel)
            {
                case TweenChannel.Scale:
                    return target.DOShakeScale(duration, strength, vibrato, randomness, fadeOut);
                case TweenChannel.Rotation:
                    return target.DOShakeRotation(duration, strength, vibrato, randomness, fadeOut);
                default:
                    return target.DOShakePosition(duration, strength, vibrato, randomness, false, fadeOut);
            }
        }
    }
}
