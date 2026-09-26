using System;
using UnityEngine;

namespace Mountains
{
    // [보존용] 예전 방식(자식 GameObject + MonoBehaviour)으로 만든 액션의 기반 클래스.
    //
    // 새 작업에는 쓰지 않는다 — 새 액션은 Assets/Scripts/Actions의 TriggerAction(순수
    // 클래스, [SerializeReference])을 쓴다. 이 폴더를 남겨두는 이유는 아직 마이그레이션하지
    // 않은 씬이 있을 수 있기 때문이다. 옛 컴포넌트가 살아 있어야 그 안의 값을 읽어
    // 새 형식으로 옮길 수 있다(Mountains > TriggerAction 마이그레이션).
    //
    // 삭제해도 되는 시점: 모든 씬에서 마이그레이션을 돌리고, EventTrigger의 actions(레거시)
    // 목록이 전부 비었을 때. 그 전에 지우면 남은 씬의 설정값이 복구 불가능하게 사라진다.
    //
    // EventTrigger 안에서 실행되는 액션 하나의 기반 클래스. 액션마다 별도 컴포넌트로
    // 구현하고, EventTrigger의 자식 오브젝트에 붙여서 구성한다 — 유니티 기본 인스펙터
    // (GetComponentsInChildren)만으로 새 액션 타입을 자유롭게 추가/재배열할 수 있어
    // 커스텀 직렬화나 드롭다운 에디터가 필요 없다.
    public abstract class LegacyTriggerAction : MonoBehaviour
    {
        // 액션을 시작하고, 끝나면(즉시든 나중이든) onComplete를 정확히 한 번 호출해야 한다.
        // EventTrigger가 같은 스텝의 다른 액션들과 함께 이 콜백을 세서 다음 스텝으로 넘어갈
        // 시점을 판단하므로, onComplete를 못 부르면 시퀀스가 거기서 영원히 멈춘다.
        public abstract void Execute(Action onComplete);
    }
}
