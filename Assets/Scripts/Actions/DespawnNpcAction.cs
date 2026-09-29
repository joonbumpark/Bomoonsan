using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

namespace Mountains
{
    // 스폰된 NPC를 다시 치우는 액션.
    //
    // 파괴와 비활성화를 나눠둔 이유: 다시 등장시킬 NPC를 파괴하면 복구할 방법이 없고
    // (스폰을 또 실행하면 다른 인스턴스가 된다), 두 번 다시 쓸 일 없는 NPC를 꺼두기만 하면
    // 씬에 계속 쌓인다.
    [Serializable]
    public class DespawnNpcAction : NpcTargetingAction
    {
        public enum Mode
        {
            Destroy,
            Deactivate,
            Activate
        }

        [Tooltip("Destroy=완전히 제거, Deactivate=꺼두기, Activate=꺼둔 NPC를 다시 켜기.")]
        public Mode mode = Mode.Destroy;

        [Tooltip("NPC에 NpcDespawnEffect가 붙어 있으면 소멸 연출이 끝나고 실제로 파괴될 " +
            "때까지 다음 스텝으로 넘어가지 않는다. Destroy 모드에서만 의미가 있다.")]
        public bool waitForDespawnEffect = true;

        public override UniTask ExecuteAsync(TriggerContext context, CancellationToken cancellationToken)
        {
            ResolveTargets();
            if (_targets.Count == 0)
            {
                return UniTask.CompletedTask;
            }

            if (mode != Mode.Destroy)
            {
                foreach (var target in _targets)
                {
                    SetActive(target, mode == Mode.Activate);
                }
                return UniTask.CompletedTask;
            }

            // 파괴는 NpcDespawner를 거친다 — NPC 프리팹에 붙은 소멸 연출이 트리거로 치울 때든
            // NPC가 스스로 사라질 때든 똑같이 나오게 하기 위해서다.
            if (!waitForDespawnEffect)
            {
                foreach (var target in _targets)
                {
                    NpcDespawner.Despawn(target);
                }
                return UniTask.CompletedTask;
            }

            var tasks = new UniTask[_targets.Count];
            for (int i = 0; i < _targets.Count; i++)
            {
                var target = _targets[i];
                tasks[i] = CallbackTask.Run(done => NpcDespawner.Despawn(target, done), cancellationToken);
            }

            return UniTask.WhenAll(tasks);
        }

        static void SetActive(GameObject npc, bool active)
        {
            if (npc == null)
            {
                return;
            }

            npc.SetActive(active);
            if (active)
            {
                RestoreNavMeshAgent(npc);
            }
        }

        // NavMeshAgent는 꺼졌다 켜질 때 자기 위치 근처의 NavMesh에 다시 올라타려 한다 —
        // 그 사이 지형이 바뀌어 벗어났으면 붙지 못한 채 남아 이동 명령이 전부 무시된다.
        static void RestoreNavMeshAgent(GameObject npc)
        {
            var agent = npc.GetComponent<NavMeshAgent>();
            if (agent == null || agent.isOnNavMesh)
            {
                return;
            }

            if (!agent.Warp(npc.transform.position))
            {
                Debug.LogWarning($"[DespawnNpcAction] {npc.name}: NavMesh 위로 되돌리지 못했습니다.");
            }
        }
    }
}
