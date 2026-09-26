using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mountains
{
    // "스폰된 NPC를 대상으로 삼는" 액션들(제거/트윈/FX)의 공통 기반. 대상 지정 방식과
    // 해석을 여기 모아두면 새 연출 액션을 추가할 때 인스펙터 구성이 저절로 같아진다.
    //
    // 예전에는 스폰 액션 컴포넌트를 직접 드래그했지만, 액션이 [SerializeReference]가 되면서
    // 다른 컴포넌트의 액션을 참조할 수 없게 됐다 — 대신 NpcKeyStore에 등록된 키로 지목한다.
    [Serializable]
    public abstract class NpcTargetingAction : TriggerAction
    {
        [Tooltip("이 키로 스폰된 NPC 전부가 대상이다.")]
        [NpcKey] public string[] spawnKeys = new string[0];

        [Tooltip("스폰과 무관하게, 씬에 미리 배치해둔 오브젝트도 추가로 지정할 수 있다.")]
        public GameObject[] extraTargets = new GameObject[0];

        protected readonly List<GameObject> _targets = new List<GameObject>();

        protected void ResolveTargets()
        {
            _targets.Clear();

            if (spawnKeys != null)
            {
                foreach (var key in spawnKeys)
                {
                    NpcSpawnRegistry.Collect(key, _targets);
                }
            }

            if (extraTargets == null)
            {
                return;
            }

            foreach (var target in extraTargets)
            {
                if (target != null)
                {
                    _targets.Add(target);
                }
            }
        }
    }
}
