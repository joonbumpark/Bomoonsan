using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// 라운드/매치가 끝나면 뜨는 결과 팝업. 실제 배치/아트는 프리팹에서 직접 만들고,
    /// 인스펙터에서 필드들을 연결해두면 된다 - 싱글/대전, 어떤 게임이든 이 팝업 하나를
    /// 공용으로 쓴다(AppFlowManager 참고).
    ///
    /// - 점수 텍스트: 숫자만 표시한다 (SetScore).
    /// - 승패 이미지: 대전 매치에서만 뜬다 - 결과가 "win"/"lose"일 때만 각각 켜고, 싱글
    ///   플레이(result == null)거나 "draw"면 둘 다 끈다 (SetMatchOutcome).
    /// - 리더보드: 스크롤뷰 content 밑에 행 프리팹을 개수만큼 찍어서 채운다
    ///   (SetLeaderboard). 지금 플레이한 사람의 행은 LeaderboardRowView가 강조 표시한다.
    /// - 나가기: 게임 선택 화면으로 나간다 (ExitClicked).
    /// - 다시하기: 방금 그 게임을 같은 모드(싱글/대전)로 다시 진행한다 (RetryClicked).
    /// </summary>
    public class ResultPopup : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private Button exitButton;
        [SerializeField] private Button retryButton;

        [Header("승패 이미지 (대전에서만 표시)")]
        [SerializeField] private GameObject winImage;
        [SerializeField] private GameObject loseImage;

        [Header("리더보드 스크롤뷰")]
        [SerializeField] private Transform leaderboardContent;
        [SerializeField] private LeaderboardRowView leaderboardRowPrefab;

        private readonly List<LeaderboardRowView> spawnedRows = new List<LeaderboardRowView>();

        public event Action ExitClicked;
        public event Action RetryClicked;

        private void Awake()
        {
            if (exitButton != null)
                exitButton.onClick.AddListener(() => ExitClicked?.Invoke());

            if (retryButton != null)
                retryButton.onClick.AddListener(() => RetryClicked?.Invoke());
        }

        /// <summary>팝업을 열고 점수를 채운다. 승패 이미지는 일단 끄고(SetMatchOutcome),
        /// 리더보드도 비워둔다 - 알게 되는 대로 각각 따로 호출할 것.</summary>
        public void Show(int score)
        {
            gameObject.SetActive(true);
            SetScore(score);
            SetMatchOutcome(null);
            ClearLeaderboard();
        }

        public void SetScore(int score)
        {
            if (scoreText != null)
                scoreText.text = score.ToString();
        }

        /// <summary>대전 결과("win"/"lose"/"draw")에 따라 승패 이미지를 켠다. null이면(싱글
        /// 플레이) 둘 다 끈다.</summary>
        public void SetMatchOutcome(string result)
        {
            if (winImage != null)
                winImage.SetActive(result == "win");
            if (loseImage != null)
                loseImage.SetActive(result == "lose");
        }

        /// <summary>리더보드 목록을 스크롤뷰에 채운다. entries는 이미 점수 내림차순으로
        /// 정렬되어 있다고 가정한다(서버/LocalLeaderboardStore 둘 다 그렇게 준다). myName과
        /// 이름이 같은 행은 강조 표시된다.</summary>
        public void SetLeaderboard(List<LeaderboardEntry> entries, string myName)
        {
            ClearLeaderboard();

            if (leaderboardContent == null || leaderboardRowPrefab == null || entries == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var row = Instantiate(leaderboardRowPrefab, leaderboardContent);
                row.Bind(i + 1, entry.name, entry.score, entry.name == myName);
                spawnedRows.Add(row);
            }
        }

        private void ClearLeaderboard()
        {
            foreach (var row in spawnedRows)
            {
                if (row != null)
                    Destroy(row.gameObject);
            }
            spawnedRows.Clear();
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
