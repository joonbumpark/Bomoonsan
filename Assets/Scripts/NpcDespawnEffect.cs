using UnityEngine;

namespace Mountains
{
    // NPC 자신이 들고 다니는 소멸 연출. NPC 프리팹(CharacterManager.npcPrefab)에 붙여두면
    // 어떤 경로로 사라지든 같은 연출이 나온다 — 트리거로 치울 때(DespawnNpcAction)뿐 아니라,
    // NpcPathWalker.destroyOnArrival처럼 NPC가 스스로 사라지는 경로에도 적용된다. 그쪽은
    // 트리거를 거치지 않아 액션을 끼워 넣을 자리가 아예 없기 때문에, 설정을 NPC 쪽에 두는
    // 방식이 필요하다.
    //
    // 붙이지 않으면 예전처럼 즉시 파괴된다. 트리거마다 다른 연출을 주고 싶으면 스폰 액션의
    // overrideDespawnEffect를 켜면 되고(Apply가 이 컴포넌트를 스폰된 NPC에 심는다),
    // 대화 연출처럼 완전히 자유로운 구성이 필요하면 TweenNpcAction + SpawnFxAction을
    // 스텝으로 조합하면 된다.
    public class NpcDespawnEffect : MonoBehaviour
    {
        public NpcDespawnEffectSettings settings = new NpcDespawnEffectSettings();

        // 소멸 연출이 도는 동안 같은 NPC에 또 Despawn이 들어오면 연출이 처음부터 다시
        // 시작되고 파괴도 그만큼 밀린다 — 한 번 시작하면 잠근다.
        public bool IsDespawning { get; private set; }

        public void MarkDespawning()
        {
            IsDespawning = true;
        }

        // 런타임에 스폰된 NPC에 연출을 심거나 덮어쓴다. 프리팹에 이미 붙어 있으면 그 컴포넌트의
        // 값만 갈아끼워서, 같은 NPC에 컴포넌트가 두 개 생기지 않게 한다(Ensure 패턴).
        public static NpcDespawnEffect Apply(GameObject npc, NpcDespawnEffectSettings source)
        {
            if (npc == null || source == null)
            {
                return null;
            }

            var effect = npc.GetComponent<NpcDespawnEffect>();
            if (effect == null)
            {
                effect = npc.AddComponent<NpcDespawnEffect>();
            }

            effect.settings = source.Clone();
            return effect;
        }
    }
}
