using System;
using UnityEngine;
using UnityEngine.AI;

namespace Mountains
{
    // SpawnNpcAction/SpawnPathWalkerNpcAction이 만든 NPC를 다시 치우는 액션. 대상 액션을
    // 인스펙터에서 드래그해서 지정하므로, 같은 트리거 안이든 완전히 다른 트리거든 상관없이
    // "저 액션이 스폰한 것들"을 그대로 가리킬 수 있다.
    //
    // 파괴(Destroy)와 비활성화(Deactivate)를 나눠둔 이유: 다시 등장시킬 NPC라면 파괴해버리면
    // 복구할 방법이 없고(스폰 액션을 또 실행하면 다른 인스턴스가 된다), 반대로 두 번 다시
    // 쓸 일이 없는 NPC를 꺼두기만 하면 씬에 계속 쌓인다.
    public class DespawnNpcAction : NpcTargetingAction
    {
        public enum Mode
        {
            Destroy,
            Deactivate,
            Activate
        }

        [Tooltip("Destroy=완전히 제거, Deactivate=꺼두기(나중에 Activate로 되살릴 수 있음), " +
            "Activate=Deactivate로 꺼둔 NPC를 다시 켜기.")]
        public Mode mode = Mode.Destroy;

        [Tooltip("NPC에 NpcDespawnEffect가 붙어 있으면 소멸 연출이 끝나고 실제로 파괴될 " +
            "때까지 다음 스텝으로 넘어가지 않는다. Destroy 모드에서만 의미가 있다.")]
        public bool waitForDespawnEffect = true;

        public override void Execute(Action onComplete)
        {
            ResolveTargets();
            if (_targets.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            if (mode == Mode.Destroy)
            {
                DestroyTargets(onComplete);
                return;
            }

            foreach (var target in _targets)
            {
                SetActive(target, mode == Mode.Activate);
            }
            onComplete?.Invoke();
        }

        // 파괴는 NpcDespawner를 거친다 — NPC 프리팹에 붙은 소멸 연출(NpcDespawnEffect)이
        // 트리거로 치울 때든 NPC가 스스로 사라질 때든 똑같이 나오게 하기 위해서다.
        void DestroyTargets(Action onComplete)
        {
            if (!waitForDespawnEffect)
            {
                foreach (var target in _targets)
                {
                    NpcDespawner.Despawn(target);
                }
                onComplete?.Invoke();
                return;
            }

            var counter = new CompletionCounter(_targets.Count, onComplete);
            foreach (var target in _targets)
            {
                NpcDespawner.Despawn(target, counter.Signal);
            }
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

        // NavMeshAgent는 컴포넌트가 꺼졌다 켜질 때 자기 위치 근처의 NavMesh에 다시
        // 올라타려 한다 — 꺼져 있는 동안 지형이 재생성되는 등으로 NavMesh에서 벗어났으면
        // 에이전트가 붙지 못한 채 남아 이동 명령이 전부 무시된다. CharacterManager가
        // 스폰할 때 하는 것과 같은 방식으로 가장 가까운 NavMesh 지점에 스냅해준다.
        static void RestoreNavMeshAgent(GameObject npc)
        {
            var agent = npc.GetComponent<NavMeshAgent>();
            if (agent == null || agent.isOnNavMesh)
            {
                return;
            }

            if (!agent.Warp(npc.transform.position))
            {
                Debug.LogWarning($"[DespawnNpcAction] {npc.name}: NavMesh 위로 되돌리지 못했습니다. " +
                    "NavMesh를 다시 구워야 이동할 수 있습니다.");
            }
        }
    }
}
