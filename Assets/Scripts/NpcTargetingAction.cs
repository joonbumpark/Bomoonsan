using System.Collections.Generic;
using UnityEngine;

namespace Mountains
{
    // "스폰 액션이 만든 NPC"를 대상으로 삼는 액션들(제거/트윈/FX)의 공통 기반. 대상 지정
    // 방식과 해석을 여기 모아두면, 새 연출 액션을 추가할 때 인스펙터 구성이 저절로 같아진다.
    //
    // 대상을 이름이나 태그가 아니라 스폰 액션 컴포넌트로 지목하는 이유: 이름으로 찾으면 같은
    // CharacterData로 스폰된 다른 NPC까지 휩쓸어가고 문자열 오타가 조용히 넘어가지만,
    // 컴포넌트를 직접 드래그하면 둘 다 없고 다른 트리거의 액션도 그대로 가리킬 수 있다.
    public abstract class NpcTargetingAction : TriggerAction
    {
        [Tooltip("여기 지정한 스폰 액션들이 만든 NPC 전부가 대상이다.")]
        public SpawnNpcActionBase[] spawnActions = new SpawnNpcActionBase[0];

        [Tooltip("스폰 액션과 무관하게, 씬에 미리 배치해둔 오브젝트도 추가로 지정할 수 있다.")]
        public GameObject[] extraTargets = new GameObject[0];

        protected readonly List<GameObject> _targets = new List<GameObject>();

        protected void ResolveTargets()
        {
            _targets.Clear();

            if (spawnActions != null)
            {
                foreach (var action in spawnActions)
                {
                    if (action == null)
                    {
                        continue;
                    }
                    _targets.AddRange(action.SpawnedNpcs);
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
