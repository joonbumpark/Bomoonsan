using System.Collections.Generic;
using UnityEngine;

namespace Mountains
{
    // 스폰된 NPC를 지목할 때 쓰는 키 목록. Unity의 Tag Manager와 같은 역할이다 — 키를
    // 여기 한 곳에 모아두고, 액션에서는 드롭다운으로 골라 쓴다.
    //
    // 키마다 ScriptableObject를 하나씩 만드는 방식도 검토했지만, 스폰 지점이 늘어날수록
    // 에셋만 쌓인다. 씬 오브젝트를 키로 쓰는 방법은 에셋이 안 늘지만 프리팹 안의 트리거가
    // 씬 오브젝트를 참조할 수 없다(Unity 제약). 문자열은 그 둘을 다 피하고, 대신 오타
    // 위험이 생기는데 그건 드롭다운으로 막는다.
    //
    // 실제 참조는 문자열 값으로 저장된다 — 여기서 키 이름을 바꾸면 이미 지정해둔 곳은
    // 옛 이름을 그대로 들고 있게 되므로(Tag와 같은 성질), 드롭다운이 "목록에 없는 키"로
    // 표시해준다.
    [CreateAssetMenu(menuName = "Mountains/NPC Key Store")]
    public class NpcKeyStore : ScriptableObject
    {
        [Tooltip("스폰/제거 액션에서 고를 수 있는 키 목록.")]
        public List<string> keys = new List<string>();

        public bool Contains(string key)
        {
            return !string.IsNullOrEmpty(key) && keys.Contains(key);
        }

        // 이미 있으면 아무것도 하지 않는다. 추가됐으면 true.
        public bool Add(string key)
        {
            if (string.IsNullOrEmpty(key) || keys.Contains(key))
            {
                return false;
            }

            keys.Add(key);
            return true;
        }
    }
}
