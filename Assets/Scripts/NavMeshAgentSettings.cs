using System;
using UnityEngine;
using UnityEngine.AI;

namespace Mountains
{
    // NavMeshAgent의 이동 성능 값 묶음. CharacterData(캐릭터의 기본 성질)와 스폰 액션
    // (이 장면에서만 다르게)에서 같은 모양으로 쓴다.
    //
    // 항목마다 "쓸지 말지" 체크박스를 두지 않고 0 이하를 "건드리지 않음"으로 쓰는 이유:
    // 세 값 모두 0이면 애초에 움직일 수 없는 값이라(속도 0, 가속 0) 의미 있는 설정과
    // 겹치지 않는다. 덕분에 인스펙터가 절반으로 줄고, 비워두면 프리팹 값이 그대로 남는다.
    [Serializable]
    public class NavMeshAgentSettings
    {
        [Tooltip("0이면 프리팹 값을 그대로 둔다.")]
        [Min(0f)] public float speed;
        [Tooltip("0이면 프리팹 값을 그대로 둔다. 회전 속도(도/초).")]
        [Min(0f)] public float angularSpeed;
        [Tooltip("0이면 프리팹 값을 그대로 둔다.")]
        [Min(0f)] public float acceleration;

        public bool HasAnyOverride => speed > 0f || angularSpeed > 0f || acceleration > 0f;

        public void Apply(NavMeshAgent agent)
        {
            if (agent == null)
            {
                return;
            }

            if (speed > 0f)
            {
                agent.speed = speed;
            }
            if (angularSpeed > 0f)
            {
                agent.angularSpeed = angularSpeed;
            }
            if (acceleration > 0f)
            {
                agent.acceleration = acceleration;
            }
        }

        public void Apply(GameObject character)
        {
            if (character == null || !HasAnyOverride)
            {
                return;
            }

            Apply(character.GetComponent<NavMeshAgent>());
        }
    }
}
