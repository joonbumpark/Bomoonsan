using DG.Tweening;
using UnityEngine;

namespace Mountains
{
    // DOJump으로 대상을 포물선을 그리며 목표 지점까지 보내는 액션. 코인 연출(Reward)처럼
    // 던져서 떨어뜨리는 움직임에 쓴다.
    public class TweenJumpAction : TweenActionBase
    {
        [Header("점프")]
        [Tooltip("지정하면 이 오브젝트의 위치로 점프한다. 비워두면 아래 endValue를 쓴다.")]
        public Transform destination;
        public Vector3 endValue;
        [Tooltip("켜두면 목표 위치를 현재 위치 기준 상대 좌표로 본다.")]
        public bool relative;

        [Tooltip("포물선의 높이. 클수록 높이 뜬다.")]
        public float jumpPower = 3f;
        [Tooltip("몇 번 튕길지. 2 이상이면 목표 지점까지 가면서 여러 번 통통 튄다.")]
        [Min(1)] public int numJumps = 1;
        [Tooltip("켜두면 월드 좌표가 아니라 로컬 좌표로 점프한다(부모가 움직이는 오브젝트).")]
        public bool local;

        // DOJump은 수직/수평 움직임을 각자 다른 ease로 묶은 Sequence다 — 겉에서 ease를
        // 덮어쓰면 포물선 모양이 망가진다.
        protected override bool UsesEase => false;

        protected override Tween CreateTween(Transform target)
        {
            Vector3 value = destination != null ? destination.position : endValue;
            if (relative)
            {
                value += local ? target.localPosition : target.position;
            }

            return local
                ? target.DOLocalJump(value, jumpPower, numJumps, duration)
                : target.DOJump(value, jumpPower, numJumps, duration);
        }
    }
}
