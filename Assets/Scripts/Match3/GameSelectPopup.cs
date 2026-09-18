using System;
using TMPro;
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
    /// 닉네임 입력창(nicknameInput)도 여기서 들고 있다 - 값 확정/저장/랜덤 생성은
    /// AppFlowManager가 게임 선택 순간(GameSelected 처리 시점)에 담당하고, 이 컴포넌트는
    /// 텍스트를 읽고 쓰는 창구 역할만 한다.
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
        [SerializeField] private TMP_InputField nicknameInput;

        public event Action<GameKind> GameSelected;

        public string NicknameInputText => nicknameInput != null ? nicknameInput.text : string.Empty;

        public void SetNicknameInputText(string text)
        {
            if (nicknameInput != null)
                nicknameInput.text = text ?? string.Empty;
        }

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
