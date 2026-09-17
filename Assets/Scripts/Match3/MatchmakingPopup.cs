using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// 대전 매칭 대기 팝업. 지금 AppFlowManager가 코드로 직접 만들어 쓰는 매칭 팝업과
    /// 구성이 같다 - 상태 문구 하나 + 취소 버튼 하나. 실제 배치/아트는 프리팹에서
    /// 만들고, 인스펙터에서 이 둘만 연결해두면 된다.
    ///
    /// 상태는 세 가지뿐이다: 상대를 찾는 중(취소 가능), 매칭된 후 상대 결과를
    /// 기다리는 중(취소 불가 - 이미 매칭됐으므로), 연결 오류(취소로 메뉴 복귀 가능).
    /// 실제로 대기열에서 나가고 메뉴로 돌아가는 처리는 이 컴포넌트가 아니라
    /// AppFlowManager 같은 상위 코드가 CancelClicked를 구독해서 한다.
    /// </summary>
    public class MatchmakingPopup : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button cancelButton;

        public event Action CancelClicked;

        private void Awake()
        {
            if (cancelButton != null)
                cancelButton.onClick.AddListener(() => CancelClicked?.Invoke());
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>대기열에 들어가 상대를 찾는 중 - 취소로 대기열을 나갈 수 있다.</summary>
        public void ShowSearching()
        {
            gameObject.SetActive(true);
            statusText.text = "상대를 찾는 중...";
            cancelButton.gameObject.SetActive(true);
        }

        /// <summary>매칭은 됐고 양쪽 점수 제출을 기다리는 중 - 이미 매칭됐으니 취소는 못 한다.</summary>
        public void ShowWaitingForResult()
        {
            gameObject.SetActive(true);
            statusText.text = "결과를 기다리는 중...";
            cancelButton.gameObject.SetActive(false);
        }

        /// <summary>연결 오류 - 취소를 눌러 메뉴로 돌아가는 것만 가능하다.</summary>
        public void ShowError(string message)
        {
            gameObject.SetActive(true);
            statusText.text = $"연결 오류: {message}\n(취소를 눌러 메뉴로 돌아가세요)";
            cancelButton.gameObject.SetActive(true);
        }
    }
}
