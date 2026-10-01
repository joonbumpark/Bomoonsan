using Mountains;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// 게임 선택 화면(로비)의 옵션 버튼을 누르면 여는 팝업. 실제 배치/아트는 프리팹에서
    /// 직접 만들고, 인스펙터에서 닫기/처음으로 돌아가기 버튼만 연결해두면 된다.
    /// 처음으로 돌아가기(다시하기)는 타이틀 씬으로 이동한다.
    ///
    /// 인게임 SettingsPopup과 달리 게임이 돌고 있지 않을 때 뜨는 팝업이라 timeScale은
    /// 건드리지 않는다. 여닫기는 GameSelectPopup이 옵션 버튼 클릭 시 Open()으로 한다.
    /// </summary>
    public class LobbyOptionPopup : MonoBehaviour
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private Button returnToStartButton;
        [SerializeField] private string titleSceneName = "TitleScene";

        private void Awake()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            if (returnToStartButton != null)
                returnToStartButton.onClick.AddListener(OnReturnToStartClicked);
        }

        public void Open() => gameObject.SetActive(true);
        public void Close() => gameObject.SetActive(false);

        private void OnReturnToStartClicked()
        {
            SceneFlow.Load(titleSceneName);
        }
    }
}
