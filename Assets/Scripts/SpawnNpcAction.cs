using System;
using UnityEngine;

namespace Mountains
{
    // CharacterManager를 통해 NPC 하나를 스폰하는 액션. 스폰 자체는 순간적이라 즉시 완료
    // 처리한다.
    public class SpawnNpcAction : TriggerAction
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
            CharacterManager.Instance.CreateNpc(characterData, position);
            onComplete?.Invoke();
        }
    }
}
