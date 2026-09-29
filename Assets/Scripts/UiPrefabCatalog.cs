using UnityEngine;

namespace Mountains
{
    // 팝업/토스트가 쓸 프리팹을 가리키는 카탈로그. Resources 폴더에 이 에셋 하나만 두고,
    // 프리팹 자체는 Assets/Prefabs/UI처럼 원하는 곳에 두면 된다.
    //
    // Resources를 경유하는 이유: MessagePopup.Show()처럼 정적 메서드로 어디서든 부르는
    // 구조라 인스펙터로 참조를 물려줄 대상이 없다. 씬에 매니저를 배치해 참조를 걸게 하면
    // "씬마다 세팅해야 하는" 부담이 생기므로, 프로젝트에 하나 있으면 자동으로 쓰이게 한다.
    [CreateAssetMenu(menuName = "Mountains/UI Prefab Catalog")]
    public class UiPrefabCatalog : ScriptableObject
    {
        // Resources.Load에 넘기는 경로(확장자 없음). 이 이름으로 Resources 폴더 아래에 두면 된다.
        public const string ResourcePath = "UiPrefabCatalog";

        [Tooltip("비워두면 MessagePopupBuilder가 코드로 기본 모양을 만든다.")]
        public MessagePopupUI messagePopupPrefab;
        [Tooltip("비워두면 ToastUI가 코드로 기본 모양을 만든다.")]
        public ToastUI toastPrefab;

        static UiPrefabCatalog _cached;
        static bool _lookedUp;

        // 없으면 null을 돌려준다(프리팹을 안 만든 상태가 정상이라 경고하지 않는다).
        // 에셋이 없을 때 매번 Resources를 뒤지지 않도록 결과를 기억해둔다.
        public static UiPrefabCatalog Load()
        {
            if (_lookedUp && _cached != null)
            {
                return _cached;
            }

            _cached = Resources.Load<UiPrefabCatalog>(ResourcePath);
            _lookedUp = true;
            return _cached;
        }

        // 도메인 리로드를 꺼둔 프로젝트라 static 값이 Play 세션 사이에 남는다 — 에디터에서
        // 카탈로그를 새로 만들거나 프리팹을 바꿔도 다음 Play에 반영되도록 초기화한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            _cached = null;
            _lookedUp = false;
        }
    }
}
