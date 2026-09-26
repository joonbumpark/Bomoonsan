using System;
using UnityEngine;

namespace Mountains
{
    // NPC를 스폰하는 액션들의 공통 부분. 스폰 방식(그냥 세워두기 / 경로 걷기)만 다르고
    // 그 앞뒤는 똑같다 — 어떤 캐릭터를, 어디에, 어떤 키로 등록하고, 이동 성능과 소멸 연출을
    // 어떻게 덮어쓸지.
    //
    // 복제해두면 한쪽에만 등록이나 오버라이드를 빠뜨리기 쉽다. 실제로 이 절차는 순서도
    // 중요해서(CharacterData 적용 뒤에 오버라이드) 한 곳에 묶는다.
    [Serializable]
    public abstract class NpcSpawnActionBase : TriggerAction
    {
        public CharacterData characterData;
        [Tooltip("비워두면 트리거 위치에 스폰한다.")]
        [GizmoTarget("스폰")] public Transform spawnPoint;
        [Tooltip("나중에 이 NPC들을 지목할 키. 제거/연출 액션이 같은 키를 고른다.")]
        [NpcKey] public string spawnKey;

        [Header("이동 성능 오버라이드")]
        [Tooltip("0이 아닌 항목만 CharacterData의 값을 덮어쓴다 — 이 장면에서만 느리게/빠르게 " +
            "걷게 하고 싶을 때 쓴다.")]
        public NavMeshAgentSettings agentOverride = new NavMeshAgentSettings();

        [Header("소멸 연출 오버라이드")]
        [Tooltip("켜두면 이 액션이 스폰한 NPC의 소멸 연출을 아래 값으로 덮어쓴다. 꺼두면 NPC " +
            "프리팹의 NpcDespawnEffect를 따르고, 그것도 없으면 연출 없이 즉시 파괴된다.")]
        public bool overrideDespawnEffect;
        public NpcDespawnEffectSettings despawnEffect = new NpcDespawnEffectSettings();

        protected Vector3 ResolveSpawnPosition(TriggerContext context)
        {
            return spawnPoint != null ? spawnPoint.position : context.Transform.position;
        }

        // 스폰된 직후에 해야 하는 공통 뒤처리. CharacterData의 이동 성능은 CharacterManager가
        // 이미 적용한 뒤이므로, 여기서 덮어쓴 항목만 최종값이 된다.
        protected void Configure(GameObject npc)
        {
            if (npc == null)
            {
                return;
            }

            agentOverride.Apply(npc);
            NpcSpawnRegistry.Register(spawnKey, npc);

            if (overrideDespawnEffect)
            {
                NpcDespawnEffect.Apply(npc, despawnEffect);
            }
        }

        protected static bool WarnIfNoCharacter(CharacterData data, string actionName)
        {
            if (data != null)
            {
                return false;
            }

            Debug.LogWarning($"[{actionName}] characterData가 비어 있어 NPC를 스폰하지 않습니다.");
            return true;
        }
    }
}
