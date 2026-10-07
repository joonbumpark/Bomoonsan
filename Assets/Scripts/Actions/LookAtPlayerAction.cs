using System;
using DG.Tweening;
using UnityEngine;

namespace Mountains
{
    // 대상(비워두면 트리거 자신)이 플레이어 쪽으로 돌아보게 한다. TweenTransformAction의
    // LookAt 모션과 같은 트윈이지만, destination에 플레이어를 미리 걸어둘 수 없어서
    // (CharacterManager가 런타임에 만들기 때문 — PlayerLocator 주석 참고) 실행 시점마다
    // PlayerLocator로 새로 찾는다.
    [Serializable]
    public class LookAtPlayerAction : TweenActionBase
    {
        [Tooltip("비워두면 Player 태그로 찾는다. 특정 상황에서만 다른 대상을 보게 하고 " +
            "싶을 때만 지정한다.")]
        [GizmoTarget("플레이어(수동 지정)")] public Transform player;

        [Tooltip("Y로 두면 고개를 젖히지 않고 수평으로만 돌아본다.")]
        public AxisConstraint lookAtAxis = AxisConstraint.Y;

        protected override Tween CreateTween(Transform target)
        {
            var resolvedPlayer = PlayerLocator.Resolve(player);
            if (resolvedPlayer == null)
            {
                Debug.LogWarning("[LookAtPlayerAction] 플레이어를 찾지 못해 건너뜁니다.");
                return null;
            }

            return target.DOLookAt(resolvedPlayer.position, duration, lookAtAxis);
        }
    }
}
