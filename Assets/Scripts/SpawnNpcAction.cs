using System;
using UnityEngine;

namespace Mountains
{
    // CharacterManager를 통해 NPC 하나를 스폰하는 액션. 스폰 자체는 순간적이라 즉시 완료
    // 처리한다. 스폰한 인스턴스는 기반 클래스가 기억해두므로, 나중에 DespawnNpcAction으로
    // 이 액션을 지목해서 그대로 파괴하거나 비활성화할 수 있다.
    public class SpawnNpcAction : SpawnNpcActionBase
    {
        public CharacterData characterData;
        [Tooltip("비워두면 이 액션이 붙어있는 오브젝트의 위치에 스폰한다.")]
        public Transform spawnPoint;

        public override void Execute(Action onComplete)
        {
            if (characterData == null)
            {
                Debug.LogWarning("[SpawnNpcAction] characterData가 비어 있어 NPC를 스폰하지 않습니다.");
                onComplete?.Invoke();
                return;
            }

            Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
            var rotatation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
            RegisterSpawned(CharacterManager.Instance.CreateNpc(characterData, position, rotatation));
            onComplete?.Invoke();
        }
    }
}
