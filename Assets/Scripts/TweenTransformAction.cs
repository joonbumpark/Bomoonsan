using DG.Tweening;
using UnityEngine;

namespace Mountains
{
    // Transform의 위치/회전/스케일을 트윈하는 범용 액션. 넷 모두 Vector3 하나를 목표로 삼는
    // 같은 모양의 트윈이라, 종류만 골라 쓰는 편이 액션을 네 개로 쪼개는 것보다 다루기 쉽다.
    //
    // 기존 TweenMoveToAction은 이미 씬에 배치돼 동작 중이라 그대로 남겨둔다 — 새로 만드는
    // 연출만 이쪽을 쓰면 된다.
    public class TweenTransformAction : TweenActionBase
    {
        public enum Motion
        {
            Move,
            LocalMove,
            Rotate,
            LocalRotate,
            Scale,
            LookAt
        }

        [Header("대상 동작")]
        public Motion motion = Motion.Move;

        [Tooltip("지정하면 이 오브젝트의 현재 값을 목표로 삼는다(Move=위치, Rotate=회전, " +
            "Scale=스케일, LookAt=바라볼 지점). 비워두면 아래 endValue를 쓴다.")]
        public Transform destination;
        [Tooltip("destination이 비어 있을 때 쓰는 목표 값.")]
        public Vector3 endValue;

        [Header("옵션")]
        [Tooltip("켜두면 목표 값을 현재 값에 더한다(상대 이동/회전). LookAt에는 적용되지 않는다.")]
        public bool relative;
        [Tooltip("Rotate/LocalRotate 전용. FastBeyond360을 쓰면 360도를 넘는 회전도 그대로 돈다.")]
        public RotateMode rotateMode = RotateMode.Fast;
        [Tooltip("LookAt 전용. Y로 두면 고개를 젖히지 않고 수평으로만 돌아본다.")]
        public AxisConstraint lookAtAxis = AxisConstraint.Y;

        protected override Tween CreateTween(Transform target)
        {
            Vector3 value = ResolveEndValue(target);

            // LookAt은 "목표 값으로 보간"이 아니라 "그 지점을 바라보게 회전"이라 relative가
            // 의미가 없다 — 나머지 넷만 SetRelative를 적용한다.
            if (motion == Motion.LookAt)
            {
                return target.DOLookAt(value, duration, lookAtAxis);
            }

            Tween tween;
            switch (motion)
            {
                case Motion.LocalMove:
                    tween = target.DOLocalMove(value, duration);
                    break;
                case Motion.Rotate:
                    tween = target.DORotate(value, duration, rotateMode);
                    break;
                case Motion.LocalRotate:
                    tween = target.DOLocalRotate(value, duration, rotateMode);
                    break;
                case Motion.Scale:
                    tween = target.DOScale(value, duration);
                    break;
                default:
                    tween = target.DOMove(value, duration);
                    break;
            }

            return relative ? tween.SetRelative() : tween;
        }

        // destination이 있으면 동작 종류에 맞는 값을 꺼내 쓴다 — LocalMove에 월드 좌표를
        // 그대로 넣으면 부모가 움직인 만큼 엉뚱한 곳으로 가므로 부모 기준으로 변환해준다.
        Vector3 ResolveEndValue(Transform target)
        {
            if (destination == null)
            {
                return endValue;
            }

            switch (motion)
            {
                case Motion.LocalMove:
                    return target.parent != null
                        ? target.parent.InverseTransformPoint(destination.position)
                        : destination.position;
                case Motion.Rotate:
                    return destination.eulerAngles;
                case Motion.LocalRotate:
                    return destination.localEulerAngles;
                case Motion.Scale:
                    return destination.localScale;
                default:
                    return destination.position;
            }
        }
    }
}
