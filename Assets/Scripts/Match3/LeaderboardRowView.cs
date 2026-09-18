using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// ResultPopup의 리더보드 스크롤뷰 안에서 한 줄(등수/닉네임/점수)을 담당하는 행.
    /// 프리팹으로 만들어서 ResultPopup.leaderboardRowPrefab에 연결해두면, 결과 화면이 뜰
    /// 때마다 ResultPopup이 목록 개수만큼 찍어내고 Bind로 값을 채운다.
    /// </summary>
    public class LeaderboardRowView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI rankText;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private Image background;
        [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.06f);
        [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.3f, 0.35f);

        /// <summary>rank는 1부터 시작한다. isMe가 true면(지금 플레이한 사람의 기록이면)
        /// 배경을 강조색으로 바꾼다.</summary>
        public void Bind(int rank, string name, int score, bool isMe)
        {
            if (rankText != null) rankText.text = rank.ToString();
            if (nameText != null) nameText.text = name;
            if (scoreText != null) scoreText.text = score.ToString();
            if (background != null) background.color = isMe ? highlightColor : normalColor;
        }
    }
}
