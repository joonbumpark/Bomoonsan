using UnityEngine;

namespace Mountains
{
    // 액션이 자기만의 기즈모를 그리고 싶을 때 구현한다. [GizmoTarget]은 "참조하는 오브젝트
    // 위치에 이름표"만 찍어주는데, 반경이나 범위처럼 값 자체를 봐야 조준할 수 있는 액션이
    // 있다 — 그런 것들을 위한 확장점이다.
    //
    // EventTrigger의 OnDrawGizmos가 호출하므로 Gizmos/Handles를 그대로 쓸 수 있다.
    public interface ITriggerActionGizmo
    {
        // origin: 이 액션을 들고 있는 트리거의 Transform.
        void DrawGizmos(Transform origin);
    }
}
