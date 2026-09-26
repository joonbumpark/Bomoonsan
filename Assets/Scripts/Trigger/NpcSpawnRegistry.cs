using System.Collections.Generic;
using UnityEngine;

namespace Mountains
{
    // 키별로 "그 키로 스폰된 NPC들"을 기억한다. 스폰 액션이 등록하고, 제거/연출 액션이
    // 같은 키로 찾아간다. 키 목록은 NpcKeyStore가 들고 있고(Tag Manager와 같은 역할),
    // 여기 저장되는 값은 그 문자열이다.
    //
    // 예전에 SpawnNpcActionBase가 자기 안에 리스트를 들고 있던 역할을 밖으로 뺀 것이다 —
    // 액션 인스턴스를 직접 참조할 수 없게 되면서 보관 장소도 공용으로 옮겼다.
    public static class NpcSpawnRegistry
    {
        static readonly Dictionary<string, List<GameObject>> _spawned =
            new Dictionary<string, List<GameObject>>();

        public static void Register(string key, GameObject npc)
        {
            if (string.IsNullOrEmpty(key) || npc == null)
            {
                return;
            }

            if (!_spawned.TryGetValue(key, out var list))
            {
                list = new List<GameObject>();
                _spawned[key] = list;
            }

            Prune(list);
            list.Add(npc);
        }

        // 찾은 결과를 호출한 쪽 리스트에 덧붙인다(매번 새 리스트를 만들지 않게).
        public static void Collect(string key, List<GameObject> results)
        {
            if (string.IsNullOrEmpty(key) || results == null || !_spawned.TryGetValue(key, out var list))
            {
                return;
            }

            Prune(list);
            results.AddRange(list);
        }

        // 파괴된 인스턴스는 Unity 쪽에서 null이 되므로 볼 때마다 정리한다
        // (NpcPathWalker.destroyOnArrival처럼 NPC가 스스로 사라지는 경우가 있다).
        static void Prune(List<GameObject> list)
        {
            list.RemoveAll(npc => npc == null);
        }

        // 도메인 리로드를 꺼둔 프로젝트라 static 값이 Play 세션 사이에 남는다 — 지난 Play에서
        // 등록된(이미 파괴된) NPC가 남아 있지 않게 초기화한다. 씬을 다시 로드할 때도
        // 필요하지만 그쪽은 SceneFlow가 따로 챙긴다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            _spawned.Clear();
        }
    }
}
