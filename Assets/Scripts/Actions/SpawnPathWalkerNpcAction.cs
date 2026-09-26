using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mountains
{
    // 경로를 따라 걷는 NPC를 스폰하는 액션. 기본값은 플레이어가 가까이 와야 출발한다 —
    // 플레이어와 무관하게 걷게 하려면 SpawnWalkingNpcAction을 쓰거나 waitForPlayer를 끈다.
    [Serializable]
    public class SpawnPathWalkerNpcAction : NpcSpawnActionBase
    {
        [Header("경로")]
        [Tooltip("스폰된 NPC가 순서대로 따라갈 지점들.")]
        [GizmoTarget("경로")] public Transform[] pathPoints;
        [Tooltip("켜두면 플레이어가 가까이 와야 출발하고 멀어지면 멈춘다. 끄면 플레이어와 " +
            "무관하게 스폰 즉시 걷기 시작한다(순찰/배경 NPC).")]
        public bool waitForPlayer = true;
        [Tooltip("서 있는 동안 플레이어 쪽을 바라볼지. waitForPlayer가 켜져 있을 때만 의미가 있다.")]
        public bool lookAtPlayerWhenIdle = true;
        [Tooltip("경로를 몇 바퀴 돌지. -1이면 무한 반복, 1이면 한 바퀴만(0도 한 바퀴).")]
        [Min(-1)] public int loopCount = -1;
        [Tooltip("경로를 다 돈 뒤 이 NPC를 파괴할지. loopCount가 -1(무한)이면 쓰이지 않는다.")]
        public bool destroyOnArrival = false;

        public override UniTask ExecuteAsync(TriggerContext context, CancellationToken cancellationToken)
        {
            if (WarnIfNoCharacter(characterData, GetType().Name))
            {
                return UniTask.CompletedTask;
            }

            var npc = CharacterManager.Instance.CreatePathWalkerNpc(characterData,
                ResolveSpawnPosition(context), pathPoints);
            Configure(npc);

            var walker = npc != null ? npc.GetComponent<NpcPathWalker>() : null;
            if (walker != null)
            {
                walker.waitForPlayer = waitForPlayer;
                walker.lookAtPlayerWhenIdle = lookAtPlayerWhenIdle;
                walker.loopCount = loopCount;
                walker.destroyOnArrival = destroyOnArrival;
            }

            return UniTask.CompletedTask;
        }
    }
}
