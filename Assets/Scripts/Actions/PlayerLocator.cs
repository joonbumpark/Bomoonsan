using UnityEngine;

namespace Mountains
{
    // "지정한 Transform이 있으면 그것, 없으면 Player 태그로 찾기"를 여러 액션이 똑같이
    // 반복하고 있었다. 플레이어는 CharacterManager가 런타임에 만들어서 인스펙터로 미리
    // 걸어둘 수 없기 때문에 생기는 패턴이라, 한 곳에 모아둔다.
    public static class PlayerLocator
    {
        public static Transform Resolve(Transform explicitPlayer)
        {
            if (explicitPlayer != null)
            {
                return explicitPlayer;
            }

            var found = GameObject.FindGameObjectWithTag("Player");
            return found != null ? found.transform : null;
        }
    }
}
