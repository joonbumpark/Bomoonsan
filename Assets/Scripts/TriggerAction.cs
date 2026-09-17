using System;
using UnityEngine;

namespace Mountains
{
    // EventTrigger 안에서 실행되는 액션 하나의 기반 클래스. 액션마다 별도 컴포넌트로
    // 구현하고, EventTrigger의 자식 오브젝트에 붙여서 구성한다 — 유니티 기본 인스펙터
    // (GetComponentsInChildren)만으로 새 액션 타입을 자유롭게 추가/재배열할 수 있어
    // 커스텀 직렬화나 드롭다운 에디터가 필요 없다.
    public abstract class TriggerAction : MonoBehaviour
    {
        // 액션을 시작하고, 끝나면(즉시든 나중이든) onComplete를 정확히 한 번 호출해야 한다.
        // EventTrigger가 같은 스텝의 다른 액션들과 함께 이 콜백을 세서 다음 스텝으로 넘어갈
        // 시점을 판단하므로, onComplete를 못 부르면 시퀀스가 거기서 영원히 멈춘다.
        public abstract void Execute(Action onComplete);
    }
}
