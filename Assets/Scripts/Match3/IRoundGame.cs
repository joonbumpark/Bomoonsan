using System;

namespace Match3
{
    /// <summary>
    /// 하나의 "제한시간 라운드 + 점수" 게임이 지켜야 하는 공통 계약. AppFlowManager가
    /// 어떤 게임이 선택됐든 똑같은 방식(BeginRound/RoundEnded/CurrentScore/SetVisible)으로
    /// 다루기 위한 인터페이스다. Match3GameManager/WhackGameManager/SimonGameManager가 구현한다.
    /// </summary>
    public interface IRoundGame
    {
        /// <summary>라운드가 시간 종료로 끝났을 때 최종 점수와 함께 호출된다.</summary>
        event Action<int> RoundEnded;

        int CurrentScore { get; }

        /// <summary>
        /// 새 라운드를 시작한다. seed를 지정하면(대전 모드) 그 시드로 콘텐츠를 생성해
        /// 양쪽 플레이어가 같은 조건으로 시작하고, null이면(싱글 모드) 매번 랜덤하게 생성한다.
        /// </summary>
        void BeginRound(int? seed = null);

        /// <summary>게임 화면을 보이거나 숨긴다. AppFlowManager가 메뉴/결과 화면과 전환할 때 쓴다.</summary>
        void SetVisible(bool visible);
    }

    /// <summary>선택 가능한 게임 종류. 서버 큐/리더보드도 이 이름으로 구분한다.</summary>
    public enum GameKind
    {
        Match3,
        Whack,
        Simon,
        Tetris,
        Jigsaw,
    }

    public static class GameKindExtensions
    {
        /// <summary>서버 프로토콜(join_queue/get_leaderboard)에 실어 보내는 문자열 id.</summary>
        public static string ToServerId(this GameKind kind) => kind switch
        {
            GameKind.Match3 => "match3",
            GameKind.Whack => "whack",
            GameKind.Simon => "simon",
            GameKind.Tetris => "tetris",
            GameKind.Jigsaw => "jigsaw",
            _ => "match3",
        };

        public static string DisplayName(this GameKind kind) => kind switch
        {
            GameKind.Match3 => "3매치 퍼즐",
            GameKind.Whack => "복주머니 잡기",
            GameKind.Simon => "순서 기억하기",
            GameKind.Tetris => "테트리스",
            GameKind.Jigsaw => "직소 퍼즐",
            _ => kind.ToString(),
        };

        /// <summary>매치3만 대전(매칭/서버 리더보드)을 지원한다 - 나머지는 싱글 전용이라
        /// AppFlowManager가 싱글/대전 선택 팝업을 안 띄우고, 점수도 서버 대신 기기에만
        /// 남긴다 (LocalLeaderboardStore).</summary>
        public static bool SupportsVersusMode(this GameKind kind) => kind == GameKind.Match3;
    }
}
