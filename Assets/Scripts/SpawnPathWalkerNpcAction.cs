using System;
using UnityEngine;

namespace Mountains
{
    // CharacterManager.CreatePathWalkerNpc를 통해, 플레이어가 가까이 오면 지정한
    // pathPoints를 따라 걷는 NPC를 스폰하는 액션. 스폰 자체는 순간적이라 즉시 완료 처리한다.
    public class SpawnPathWalkerNpcAction : SpawnNpcActionBase
    {
        public CharacterData characterData;
        [Tooltip("비워두면 이 액션이 붙어있는 오브젝트의 위치에 스폰한다.")]
        public Transform spawnPoint;
        [Tooltip("스폰된 NPC가 순서대로 따라갈 지점들.")]
        public Transform[] pathPoints;
        [Tooltip("마지막 지점에 도착하면 처음으로 돌아가 반복할지. 꺼두면 마지막 지점에서 멈춘다.")]
        public bool loop = true;
        [Tooltip("loop가 꺼져 있을 때, 마지막 지점에 도착하면 이 NPC를 파괴할지.")]
        public bool destroyOnArrival = false;

        public override void Execute(Action onComplete)
        {
            if (characterData == null)
            {
                Debug.LogWarning("[SpawnPathWalkerNpcAction] characterData가 비어 있어 NPC를 스폰하지 않습니다.");
                onComplete?.Invoke();
                return;
            }

            Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
            var npcGo = CharacterManager.Instance.CreatePathWalkerNpc(characterData, position, pathPoints);

            RegisterSpawned(npcGo);

            var walker = npcGo != null ? npcGo.GetComponent<NpcPathWalker>() : null;
            if (walker != null)
            {
                walker.loop = loop;
                walker.destroyOnArrival = destroyOnArrival;
            }

            onComplete?.Invoke();
        }
    }
}
