using System;
using UnityEngine;

namespace Mountains
{
    // NPC를 없애는 유일한 진입점. 트리거로 치우든(DespawnNpcAction) NPC가 스스로
    // 사라지든(NpcPathWalker.destroyOnArrival) 여기를 지나가게 해서, 소멸 연출이
    // 경로에 따라 나왔다 안 나왔다 하는 일이 없게 한다.
    //
    // OnDestroy()에서 연출을 띄우는 방법도 있지만, 그쪽은 씬 언로드나 플레이 종료 때도
    // 불려서 "파괴 중에 새 오브젝트를 만들 수 없다"는 에러를 뱉는다 — 명시적으로 이
    // 함수를 거치는 방식을 택했다.
    public static class NpcDespawner
    {
        public static void Despawn(GameObject npc, Action onDestroyed = null)
        {
            if (npc == null)
            {
                onDestroyed?.Invoke();
                return;
            }

            var effect = npc.GetComponent<NpcDespawnEffect>();
            if (effect == null)
            {
                UnityEngine.Object.Destroy(npc);
                onDestroyed?.Invoke();
                return;
            }

            if (effect.IsDespawning)
            {
                // 이미 진행 중인 연출이 끝나면서 파괴한다 — 여기서 또 기다릴 수는 없으니
                // 호출한 쪽에는 바로 끝났다고 알린다(시퀀스가 멈추지 않게).
                onDestroyed?.Invoke();
                return;
            }
            effect.MarkDespawning();

            var settings = effect.settings;
            FxUtility.Spawn(settings.fxPrefab, npc.transform.position + settings.fxOffset,
                settings.fxLifetime);

            NpcTweenUtility.Play(npc, false, settings.tweenDuration, settings.ease, settings.sinkDistance,
                settings.tweenScale, true, () =>
                {
                    if (npc != null)
                    {
                        UnityEngine.Object.Destroy(npc);
                    }
                    onDestroyed?.Invoke();
                });
        }
    }
}
