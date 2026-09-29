using UnityEngine;

namespace Mountains
{
    // [SerializeReference] 필드에 함께 붙이면 인스펙터에 "어떤 파생 타입을 넣을지" 고르는
    // 드롭다운이 생긴다.
    //
    // 필요한 이유: Unity 기본 인스펙터는 [SerializeReference] 항목에 타입 선택기를 주지
    // 않는다. 배열에 +를 눌러 칸을 늘려도 값이 null인 채로 남고, 무엇을 넣을지 지정할
    // 방법이 아예 없다(오브젝트 참조와 달리 드래그해 넣을 대상도 없다).
    public class SubclassSelectorAttribute : PropertyAttribute
    {
    }
}
