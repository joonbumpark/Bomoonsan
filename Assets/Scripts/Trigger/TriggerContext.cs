using UnityEngine;

namespace Mountains
{
    // 액션이 MonoBehaviour가 아니게 되면서 잃어버린 것들(자기 Transform, 코루틴을 돌릴
    // 주체, 로그에 쓸 이름)을 대신 건네주는 묶음. 액션을 실행하는 쪽(EventTrigger)이 만든다.
    public class TriggerContext
    {
        // 액션을 들고 있는 컴포넌트. 코루틴이 꼭 필요한 경우나 오브젝트 수명을 따라가야 할 때 쓴다.
        public MonoBehaviour Owner { get; }

        // 예전에 액션이 자기 transform을 기본값으로 쓰던 자리를 대신한다
        // (예: 스폰 위치를 비워두면 트리거 위치에 스폰).
        public Transform Transform => Owner != null ? Owner.transform : null;

        // 경고 메시지에 "어느 트리거에서 났는지" 남기기 위한 이름.
        public string Name => Owner != null ? Owner.name : "(destroyed)";

        public TriggerContext(MonoBehaviour owner)
        {
            Owner = owner;
        }
    }
}
