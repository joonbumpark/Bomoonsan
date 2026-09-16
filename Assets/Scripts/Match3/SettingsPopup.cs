using System;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// 인게임 중 설정 버튼을 누르면 여는 일시정지 팝업. 실제 배치/아트는 프리팹에서
    /// 직접 만들고, 인스펙터에서 닫기/다시하기/메인메뉴 버튼만 연결해두면 된다.
    ///
    /// Open()되어 있는 동안 Time.timeScale을 0으로 만들어 게임을 멈춘다 - 어떤
    /// IRoundGame이든(Match3/Whack/Simon/Tetris/Jigsaw 전부 Time.deltaTime 기반이라)
    /// 따로 코드를 손보지 않아도 그대로 멈춘다. uGUI 입력은 timeScale의 영향을 받지
    /// 않으므로 이 팝업 자체의 버튼은 게임이 멈춰있어도 정상적으로 눌린다.
    ///
    /// "다시하기"/"메인메뉴로 나가기"의 실제 처리(라운드 재시작, 화면 전환)는 이
    /// 컴포넌트가 아니라 AppFlowManager 같은 상위 코드가 RestartRequested/
    /// ExitToMenuRequested를 구독해서 한다 - 두 경우 모두 이벤트를 쏘기 전에 먼저
    /// 스스로 Close()해서 timeScale을 원래대로 되돌려 놓는다.
    /// </summary>
    public class SettingsPopup : MonoBehaviour
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button exitToMenuButton;

        public event Action RestartRequested;
        public event Action ExitToMenuRequested;

        private float previousTimeScale = 1f;

        private void Awake()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            if (restartButton != null)
                restartButton.onClick.AddListener(() =>
                {
                    Close();
                    RestartRequested?.Invoke();
                });

            if (exitToMenuButton != null)
                exitToMenuButton.onClick.AddListener(() =>
                {
                    Close();
                    ExitToMenuRequested?.Invoke();
                });
        }

        /// <summary>인게임 설정 버튼이 호출한다 - 팝업을 띄우고 게임을 멈춘다.</summary>
        public void Open()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            gameObject.SetActive(true);
        }

        /// <summary>팝업을 닫고 게임을 멈추기 전 속도로 되돌린다.</summary>
        public void Close()
        {
            gameObject.SetActive(false);
            Time.timeScale = previousTimeScale;
        }
    }
}
