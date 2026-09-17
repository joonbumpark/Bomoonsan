using System;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// 게임을 고른 뒤 싱글/대전 중 뭘로 할지 고르는 팝업. 실제 배치/아트는 프리팹에서
    /// 직접 만들고, 인스펙터에서 두 버튼만 연결해두면 이 컴포넌트가 클릭을 받아
    /// SinglePlayClicked/VersusPlayClicked 이벤트로 알려준다 - AppFlowManager 같은
    /// 상위 코드가 이 이벤트를 구독해서 실제 라운드 시작/매칭 대기를 처리한다.
    /// </summary>
    public class PlayModePopup : MonoBehaviour
    {
        [SerializeField] private Button singleButton;
        [SerializeField] private Button versusButton;

        public event Action SinglePlayClicked;
        public event Action VersusPlayClicked;

        private void Awake()
        {
            if (singleButton != null)
                singleButton.onClick.AddListener(() => SinglePlayClicked?.Invoke());

            if (versusButton != null)
                versusButton.onClick.AddListener(() => VersusPlayClicked?.Invoke());
        }

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
    }
}
