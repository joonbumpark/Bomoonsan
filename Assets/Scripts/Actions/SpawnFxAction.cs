using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mountains
{
    // 지정한 위치에 FX 프리팹을 하나씩 터뜨리는 액션. 등장이면 아직 NPC가 없을 수 있으니
    // at에 지점을 직접 물리고, 퇴장이면 spawnKeys로 사라질 NPC들을 지목하면 각자 위치에 나온다.
    [Serializable]
    public class SpawnFxAction : NpcTargetingAction
    {
        public GameObject fxPrefab;

        [Header("위치")]
        [Tooltip("지정하면 여기 한 곳에만 생성한다. 비워두면 대상들의 위치에 각각 생성하고, " +
            "그것마저 없으면 트리거 위치에 생성한다.")]
        [GizmoTarget("FX")] public Transform at;
        public Vector3 offset;

        [Header("수명")]
        [Tooltip("이 시간이 지나면 생성된 FX를 파괴한다. 0 이하면 스스로 정리하는 FX로 보고 두지 않는다.")]
        public float lifetime = 3f;
        [Tooltip("켜두면 lifetime이 끝날 때까지 다음 스텝으로 넘어가지 않는다.")]
        public bool waitForLifetime = false;

        public override UniTask ExecuteAsync(TriggerContext context, CancellationToken cancellationToken)
        {
            if (fxPrefab == null)
            {
                Debug.LogWarning("[SpawnFxAction] fxPrefab이 비어 있어 FX를 생성하지 않습니다.");
                return UniTask.CompletedTask;
            }

            if (at != null)
            {
                FxUtility.Spawn(fxPrefab, at.position + offset, lifetime);
            }
            else
            {
                ResolveTargets();
                if (_targets.Count == 0)
                {
                    FxUtility.Spawn(fxPrefab, context.Transform.position + offset, lifetime);
                }
                else
                {
                    foreach (var target in _targets)
                    {
                        FxUtility.Spawn(fxPrefab, target.transform.position + offset, lifetime);
                    }
                }
            }

            if (!waitForLifetime || lifetime <= 0f)
            {
                return UniTask.CompletedTask;
            }

            return UniTask.Delay(TimeSpan.FromSeconds(lifetime), cancellationToken: cancellationToken);
        }
    }
}
