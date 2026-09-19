using UnityEngine;

namespace Mountains
{
    // FX 프리팹을 한 번 터뜨리고 수명이 다하면 정리하는 공통 코드. 트리거 액션
    // (SpawnFxAction)과 NPC 자신의 소멸 연출(NpcDespawner) 양쪽에서 쓴다.
    public static class FxUtility
    {
        // FX를 대상의 자식으로 붙이지 않는 것이 핵심이다 — 자식이면 NPC가 파괴되는 순간
        // 같이 사라져서 정작 보여주려던 소멸 연출이 한 프레임도 안 보인다.
        public static GameObject Spawn(GameObject prefab, Vector3 position, float lifetime)
        {
            // 프리팹이 자기 회전을 이미 맞춰뒀으면 그대로 쓴다.
            return prefab != null
                ? Spawn(prefab, position, prefab.transform.rotation, lifetime)
                : null;
        }

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation,
            float lifetime)
        {
            if (prefab == null)
            {
                return null;
            }

            var fx = Object.Instantiate(prefab, position, rotation);
            if (lifetime > 0f)
            {
                Object.Destroy(fx, lifetime);
            }
            return fx;
        }
    }
}
