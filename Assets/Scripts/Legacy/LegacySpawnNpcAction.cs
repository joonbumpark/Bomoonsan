using System;
using UnityEngine;

namespace Mountains
{
    // CharacterManager를 통해 NPC 하나를 스폰하는 액션. 스폰 자체는 순간적이라 즉시 완료
    // 처리한다. 스폰한 인스턴스는 기반 클래스가 기억해두므로, 나중에 DespawnNpcAction으로
    // 이 액션을 지목해서 그대로 파괴하거나 비활성화할 수 있다.
    // 새로 붙이지 못하게 Add Component 메뉴에서 감춘다 — 이미 붙어 있는
    // 컴포넌트는 그대로 동작하므로 마이그레이션에는 영향이 없다.
    [AddComponentMenu("")]
    public class LegacySpawnNpcAction : LegacySpawnNpcActionBase
    {
        public CharacterData characterData;
        [Tooltip("비워두면 이 액션이 붙어있는 오브젝트의 위치에 스폰한다.")]
        public Transform spawnPoint;

        public override void Execute(Action onComplete)
        {
            if (characterData == null)
            {
                Debug.LogWarning("[LegacySpawnNpcAction] characterData가 비어 있어 NPC를 스폰하지 않습니다.");
                onComplete?.Invoke();
                return;
            }

            Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
            RegisterSpawned(CharacterManager.Instance.CreateNpc(characterData, position));
            onComplete?.Invoke();
        }
    }
}
