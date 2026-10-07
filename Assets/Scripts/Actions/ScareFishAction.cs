using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Mountains.Env;
using UnityEngine;

namespace Mountains
{
    // 지정한 자리에 잠깐 "놀람 지점"을 만들어 물고기를 흩어지게 한다. 돌을 던졌다,
    // 물에 발을 담갔다, 낚싯줄이 떨어졌다 같은 순간 반응용이다.
    //
    // 포식자 오브젝트를 만들어 두고 따라다니게 하는 대신 지점과 수명만 남기는 이유:
    // 연출이 끝나면 저절로 사라져 뒷정리가 필요 없고, 물고기 무리가 여럿이어도 모두
    // 같은 지점에 반응한다(FishScare가 전역 목록이라 무리를 지목할 필요가 없다).
    [Serializable]
    public class ScareFishAction : TriggerAction, ITriggerActionGizmo
    {
        [Tooltip("놀라게 할 자리. 비워두면 트리거 위치를 쓴다.")]
        [GizmoTarget("물고기 놀람")] public Transform at;
        public Vector3 offset;

        [Tooltip("이 반경 안의 물고기가 반응한다. 가장자리로 갈수록 약해진다.")]
        [Min(0.1f)] public float radius = 8f;
        [Tooltip("밀어내는 세기. 무리 가중치(보통 1~4)와 같은 단위다.")]
        [Min(0f)] public float strength = 3f;
        [Tooltip("몇 초 동안 유지할지. 지나면 저절로 사라진다.")]
        [Min(0.01f)] public float duration = 2f;

        [Tooltip("켜두면 유지 시간이 끝날 때까지 다음 스텝으로 넘어가지 않는다. 꺼두면 " +
            "물고기가 흩어지는 동안 다음 연출이 함께 진행된다.")]
        public bool waitForDuration;

        // 판정이 수평 거리만 보므로(높이는 무시) 반경도 수평 원으로 그린다 — 구로 그리면
        // Y를 맞춰야 하는 것처럼 보인다.
        public void DrawGizmos(Transform origin)
        {
#if UNITY_EDITOR
            Vector3 center = ResolvePosition(origin);

            UnityEditor.Handles.color = new Color(1f, 0.4f, 0.3f, 0.9f);
            UnityEditor.Handles.DrawWireDisc(center, Vector3.up, radius);
            UnityEditor.Handles.color = new Color(1f, 0.4f, 0.3f, 0.12f);
            UnityEditor.Handles.DrawSolidDisc(center, Vector3.up, radius);
            UnityEditor.Handles.Label(center + Vector3.up * 0.3f, $"놀람 r={radius:0.#} / {duration:0.#}s");
#endif
        }

        Vector3 ResolvePosition(Transform origin)
        {
            return (at != null ? at.position : origin != null ? origin.position : Vector3.zero) + offset;
        }

        public override UniTask ExecuteAsync(TriggerContext context, CancellationToken cancellationToken)
        {
            Vector3 position = ResolvePosition(context.Transform);
            FishScare.Add(position, radius, strength, duration);

            if (!waitForDuration)
            {
                return UniTask.CompletedTask;
            }

            return UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: cancellationToken);
        }
    }
}
