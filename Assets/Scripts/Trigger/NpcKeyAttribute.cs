using UnityEngine;

namespace Mountains
{
    // string 필드에 붙이면 NpcKeyStore에 등록된 키 중에서 고르는 드롭다운이 된다
    // (Unity의 Tag 필드와 같은 사용감). 직접 타이핑할 일이 없어 오타가 나지 않는다.
    public class NpcKeyAttribute : PropertyAttribute
    {
    }
}
