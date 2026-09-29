using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mountains
{
    // NPC 하나를 스폰하는 액션. 스폰 자체는 순간적이라 즉시 완료된다.
    [Serializable]
    public class SpawnNpcAction : NpcSpawnActionBase
    {
        public override UniTask ExecuteAsync(TriggerContext context, CancellationToken cancellationToken)
        {
            if (WarnIfNoCharacter(characterData, nameof(SpawnNpcAction)))
            {
                return UniTask.CompletedTask;
            }

            var npc = CharacterManager.Instance.CreateNpc(characterData, ResolveSpawnPosition(context));
            Configure(npc);

            return UniTask.CompletedTask;
        }
    }
}
