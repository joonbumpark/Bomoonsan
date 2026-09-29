using System.Collections.Generic;
using UnityEngine;

namespace Mountains
{
    // NPC를 스폰하는 액션들의 공통 기반. 스폰한 인스턴스를 기억해둬서, 나중에 다른
    // 트리거의 DespawnNpcAction이 "이 액션이 만든 NPC"를 정확히 지목할 수 있게 한다.
    // 이름이나 태그로 찾는 방식은 같은 CharacterData로 스폰된 다른 NPC까지 휩쓸어가고
    // 문자열 오타가 조용히 넘어가지만, 액션 컴포넌트를 직접 드래그하는 방식은 둘 다 없다.
    public abstract class LegacySpawnNpcActionBase : LegacyTriggerAction
    {
        [Header("소멸 연출 오버라이드")]
        [Tooltip("켜두면 이 액션이 스폰한 NPC의 소멸 연출을 아래 값으로 덮어쓴다 — 트리거마다 " +
            "다른 연출을 줄 때 쓴다. 꺼두면 NPC 프리팹에 붙은 NpcDespawnEffect를 그대로 " +
            "따르고, 그것마저 없으면 연출 없이 즉시 파괴된다.")]
        public bool overrideDespawnEffect;
        public NpcDespawnEffectSettings despawnEffect = new NpcDespawnEffectSettings();

        // 파괴된 인스턴스는 Unity 쪽에서 null이 되므로, 조회/추가 시점에 정리해준다
        // (NpcPathWalker.destroyOnArrival처럼 NPC가 스스로 사라지는 경우가 있다).
        readonly List<GameObject> _spawned = new List<GameObject>();

        public IReadOnlyList<GameObject> SpawnedNpcs
        {
            get
            {
                Prune();
                return _spawned;
            }
        }

        protected void RegisterSpawned(GameObject npc)
        {
            if (npc == null)
            {
                return;
            }

            Prune();
            _spawned.Add(npc);

            // 오버라이드는 스폰 직후에 심어야 한다 — NPC가 스스로 사라지는 경로
            // (destroyOnArrival)는 트리거를 거치지 않으므로, 나중에 끼워 넣을 기회가 없다.
            if (overrideDespawnEffect)
            {
                NpcDespawnEffect.Apply(npc, despawnEffect);
            }
        }

        void Prune()
        {
            _spawned.RemoveAll(npc => npc == null);
        }
    }
}
