using System;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// 게임 종류별 버튼이 있는 게임 선택 팝업. 실제 배치/아트는 프리팹에서 직접 만들고,
    /// 인스펙터에서 각 버튼을 GameKind와 연결해두기만 하면 이 컴포넌트가 클릭을 받아
    /// GameSelected 이벤트로 알려준다 - AppFlowManager 같은 상위 코드가 이 이벤트를
    /// 구독해서 실제 화면 전환/라운드 시작을 처리한다.
    ///
    /// 이 팝업이 앱의 첫 화면(메인)이라 뒤로 갈 곳이 없다 - 닫기 버튼은 없다.
    /// </summary>
    public class GameSelectPopup : MonoBehaviour
    {
        [Serializable]
        public struct GameButtonEntry
        {
            public GameKind kind;
            public Button button;
        }

        [SerializeField] private GameButtonEntry[] gameButtons;

        public event Action<GameKind> GameSelected;

        private void Awake()
        {
            foreach (var entry in gameButtons)
            {
                if (entry.button == null)
                    continue;

                var kind = entry.kind;
                entry.button.onClick.AddListener(() => GameSelected?.Invoke(kind));
            }
        }

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
    }
}
