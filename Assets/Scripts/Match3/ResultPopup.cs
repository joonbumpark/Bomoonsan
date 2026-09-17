using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// 라운드/매치가 끝나면 뜨는 결과 팝업. 실제 배치/아트는 프리팹에서 직접 만들고,
    /// 인스펙터에서 텍스트 2개와 버튼 2개만 연결해두면 된다 - 싱글/대전, 어떤 게임이든
    /// 이 팝업 하나를 공용으로 쓴다(AppFlowManager 참고).
    ///
    /// - 점수 텍스트: 숫자만 표시한다 (SetScore).
    /// - 등수 텍스트: 리더보드 안에서 "내 등수/전체 유저"를 표시한다 (SetRank). 매치3는
    ///   서버 응답을 기다려야 해서 일단 Show()에서 SetRankPending()으로 자리만
    ///   잡아두고, 서버가 응답하면 AppFlowManager가 SetRank를 호출해 채운다. 나머지
    ///   게임(로컬 리더보드)은 점수를 기록하자마자 바로 등수를 알 수 있어서 대기 없이
    ///   SetRank가 곧바로 불린다.
    /// - 나가기: 게임 선택 화면으로 나간다 (ExitClicked).
    /// - 다시하기: 방금 그 게임을 같은 모드(싱글/대전)로 다시 진행한다 (RetryClicked).
    /// </summary>
    public class ResultPopup : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI rankText;
        [SerializeField] private Button exitButton;
        [SerializeField] private Button retryButton;

        public event Action ExitClicked;
        public event Action RetryClicked;

        private void Awake()
        {
            if (exitButton != null)
                exitButton.onClick.AddListener(() => ExitClicked?.Invoke());

            if (retryButton != null)
                retryButton.onClick.AddListener(() => RetryClicked?.Invoke());
        }

        /// <summary>팝업을 열고 점수를 채운다. 등수는 아직 모르니 일단 대기 표시로 둔다 -
        /// 알게 되는 대로 SetRank를 따로 호출할 것.</summary>
        public void Show(int score)
        {
            gameObject.SetActive(true);
            SetScore(score);
            SetRankPending();
        }

        public void SetScore(int score)
        {
            if (scoreText != null)
                scoreText.text = score.ToString();
        }

        /// <summary>등수를 아직 몰라 서버 응답을 기다리는 동안 자리만 잡아둔다.</summary>
        public void SetRankPending()
        {
            if (rankText != null)
                rankText.text = "-/-";
        }

        /// <summary>rank/total 모두 1부터 시작하는 값이다 - "5/42"처럼 표시한다.</summary>
        public void SetRank(int rank, int total)
        {
            if (rankText != null)
                rankText.text = $"{rank}/{total}";
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
