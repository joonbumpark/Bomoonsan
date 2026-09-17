using System;
using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    /// <summary>
    /// 앱 전체 흐름을 관리한다: 게임 선택(3매치/테트리스/직소 퍼즐, 앱의 첫 화면) -> (매치3만)
    /// 싱글/대전 선택(PlayModePopup) -> (대전이면) 매칭 대기 -> 실제 플레이(IRoundGame) ->
    /// 결과 화면(ResultPopup) -> 나가기(게임 선택으로) 또는 다시하기(같은 모드로 재도전).
    ///
    /// 매치3만 대전을 지원한다(GameKindExtensions.SupportsVersusMode) - 나머지 게임은
    /// 게임을 고르자마자 싱글로 바로 시작하고, 싱글/대전 선택 팝업 자체를 안 띄운다.
    ///
    /// 리더보드도 게임에 따라 저장 위치가 다르다: 매치3는 싱글/대전 어느 쪽이든 라운드가
    /// 끝나면 서버에 자동 기록되고(같은 닉네임이면 더 높은 점수만 남음), 나머지 게임은
    /// 대전이 없어서 서버 대신 이 기기에만 기록한다(LocalLeaderboardStore, 같은 규칙).
    /// 결과 화면의 등수 표시(ResultPopup.SetRank)도 매치3는 서버 응답(OnLeaderboardRank)을
    /// 기다렸다가 채우고, 나머지는 기록 직후 로컬에서 바로 계산해서 채운다.
    ///
    /// 닉네임은 입력 UI 없이 최초 1회 자동 생성해서 PlayerPrefs에 저장해두고 계속 재사용한다
    /// (ResolveNickname).
    ///
    /// 아래 씬 UI 필드는 전부 인스펙터에서 미리 연결돼 있어야 한다 - Canvas/패널을
    /// 코드로 매번 새로 만드는 대신 Bomoonsan > Build App Shell In Scene 메뉴
    /// (Assets/Editor/AppShellSceneBuilder.cs)로 InGameScene에 미리 배치해두고,
    /// 이 컴포넌트는 그 오브젝트들을 참조만 한다. 위치/아트를 손보고 싶으면 씬에서
    /// 직접 만지면 되고, 코드를 다시 실행할 필요가 없다.
    /// </summary>
    public class AppFlowManager : MonoBehaviour
    {
        [Header("대전 서버")]
        public string serverUrl = "wss://bomoonsan-match3.fly.dev";

        private const string NicknamePrefKey = "match3_nickname";

        [Header("씬 UI - 공통")]
        [SerializeField] private GameObject appCanvasRoot;
        [SerializeField] private GameSelectPopup gameSelectPopup;
        [SerializeField] private PlayModePopup playModePopup;
        [SerializeField] private MatchmakingPopup matchmakingPopup;
        [SerializeField] private ResultPopup resultPopup;

        private Dictionary<GameKind, IRoundGame> games;
        private GameKind selectedGame = GameKind.Match3;
        private IRoundGame CurrentGame => games[selectedGame];

        private NetworkClient network;

        private bool isVersusMatch;
        private string currentMatchId;
        private string currentOpponentName;
        private Action pendingOnOpenAction;

        private void Awake()
        {
            if (!ValidateSceneRefs())
                return;

            games = new Dictionary<GameKind, IRoundGame>
            {
                { GameKind.Match3, CreateGame<Match3GameManager>("Match3GameManager") },
                { GameKind.Whack, CreateGame<WhackGameManager>("WhackGameManager") },
                { GameKind.Simon, CreateGame<SimonGameManager>("SimonGameManager") },
                { GameKind.Tetris, CreateGame<TetrisGameManager>("TetrisGameManager") },
                { GameKind.Jigsaw, CreateGame<JigsawGameManager>("JigsawGameManager") },
            };
            foreach (var game in games.Values)
                game.RoundEnded += HandleRoundEnded;

            var networkGo = new GameObject("NetworkClient");
            networkGo.transform.SetParent(transform, false);
            network = networkGo.AddComponent<NetworkClient>();
            network.serverUrl = serverUrl;
            network.OnOpen += HandleNetworkOpen;
            network.OnQueued += HandleQueued;
            network.OnMatched += HandleMatched;
            network.OnMatchResult += HandleMatchResult;
            network.OnLeaderboardRank += HandleLeaderboardRank;
            network.OnError += HandleNetworkError;

            WireButtons();
            ShowGameSelect();
        }

        /// <summary>
        /// 씬에 미리 배치된(인스펙터에서 값 조정용) 게임 매니저가 있으면 그걸 쓰고,
        /// 없으면 새로 만든다. Match3GameManager뿐 아니라 Whack/Simon도 같은 방식이다.
        /// </summary>
        private T CreateGame<T>(string name) where T : Component, IRoundGame
        {
            var existing = UnityEngine.Object.FindFirstObjectByType<T>();
            if (existing != null)
                return existing;

            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        /// <summary>
        /// 인스펙터에서 연결이 빠진 씬 UI 필드가 있으면 NRE 대신 어떤 필드가
        /// 비었는지 한 번에 알려주고 멈춘다 - Bomoonsan > Build App Shell In Scene을
        /// 다시 돌리거나 수동으로 연결하면 된다.
        /// </summary>
        private bool ValidateSceneRefs()
        {
            var missing = new List<string>();
            void Check(UnityEngine.Object obj, string fieldName)
            {
                if (obj == null)
                    missing.Add(fieldName);
            }

            Check(appCanvasRoot, nameof(appCanvasRoot));
            Check(gameSelectPopup, nameof(gameSelectPopup));
            Check(playModePopup, nameof(playModePopup));
            Check(matchmakingPopup, nameof(matchmakingPopup));
            Check(resultPopup, nameof(resultPopup));

            if (missing.Count == 0)
                return true;

            Debug.LogError(
                $"[AppFlowManager] 씬 UI 참조가 비어 있음: {string.Join(", ", missing)}\n" +
                "Bomoonsan > Build App Shell In Scene 메뉴로 다시 생성하거나 인스펙터에서 직접 연결할 것.",
                this);
            return false;
        }

        private void WireButtons()
        {
            gameSelectPopup.GameSelected += OnGameSelected;

            playModePopup.SinglePlayClicked += OnSinglePlayClicked;
            playModePopup.VersusPlayClicked += OnVersusPlayClicked;

            matchmakingPopup.CancelClicked += OnCancelMatchmakingClicked;

            resultPopup.ExitClicked += ShowGameSelect;
            resultPopup.RetryClicked += OnRetryClicked;
        }

        // ----------------------------------------------------------------
        // 화면 전환
        // ----------------------------------------------------------------

        private void SetActivePanel(GameObject panel)
        {
            gameSelectPopup.gameObject.SetActive(panel == gameSelectPopup.gameObject);
            playModePopup.gameObject.SetActive(panel == playModePopup.gameObject);
            matchmakingPopup.gameObject.SetActive(panel == matchmakingPopup.gameObject);
            resultPopup.gameObject.SetActive(panel == resultPopup.gameObject);
            appCanvasRoot.SetActive(panel != null);

            // 패널이 하나도 안 떠 있을 때(panel == null)만 실제 게임 화면이고,
            // 그중에서도 지금 선택된 게임만 보이게 한다.
            foreach (var kv in games)
                kv.Value.SetVisible(panel == null && kv.Key == selectedGame);
        }

        private void ShowPlayModePopup()
        {
            if (network.IsConnected)
                network.LeaveQueue();

            isVersusMatch = false;
            SetActivePanel(playModePopup.gameObject);
            playModePopup.Show();
        }

        private void ShowMatchmaking()
        {
            SetActivePanel(matchmakingPopup.gameObject);
            matchmakingPopup.ShowSearching();
        }

        private void ShowWaitingForResult()
        {
            SetActivePanel(matchmakingPopup.gameObject);
            matchmakingPopup.ShowWaitingForResult();
        }

        private void ShowGame()
        {
            SetActivePanel(null);
        }

        private void ShowGameSelect()
        {
            SetActivePanel(gameSelectPopup.gameObject);
            gameSelectPopup.Show();
        }

        private void ShowResult(int score)
        {
            SetActivePanel(resultPopup.gameObject);
            resultPopup.Show(score);
        }

        // ----------------------------------------------------------------
        // 버튼 핸들러
        // ----------------------------------------------------------------

        /// <summary>게임 선택 화면(앱 첫 화면)에서 게임을 고르면, 대전을 지원하는 게임(매치3)만
        /// 싱글/대전 선택 팝업으로 넘어가고, 나머지는 고르자마자 바로 싱글로 시작한다.</summary>
        private void OnGameSelected(GameKind kind)
        {
            selectedGame = kind;

            if (kind.SupportsVersusMode())
                ShowPlayModePopup();
            else
                OnSinglePlayClicked();
        }

        private void OnSinglePlayClicked()
        {
            isVersusMatch = false;
            ShowGame();
            CurrentGame.BeginRound();
        }

        private void OnVersusPlayClicked()
        {
            isVersusMatch = true;
            string name = ResolveNickname();
            ShowMatchmaking();
            EnsureConnectedThen(() => network.JoinQueue(name, selectedGame.ToServerId()));
        }

        /// <summary>결과 화면의 "다시하기" - 방금 그 게임을 같은 모드(싱글/대전)로 다시 한다.</summary>
        private void OnRetryClicked()
        {
            if (isVersusMatch)
                OnVersusPlayClicked();
            else
                OnSinglePlayClicked();
        }

        private void OnCancelMatchmakingClicked()
        {
            network.LeaveQueue();
            ShowPlayModePopup();
        }

        private void EnsureConnectedThen(Action action)
        {
            if (network.IsConnected)
            {
                action();
                return;
            }

            pendingOnOpenAction = action;
            network.Connect();
        }

        // ----------------------------------------------------------------
        // 네트워크 콜백
        // ----------------------------------------------------------------

        private void HandleNetworkOpen()
        {
            var action = pendingOnOpenAction;
            pendingOnOpenAction = null;
            action?.Invoke();
        }

        private void HandleQueued()
        {
            // 서버가 대기열 참가를 확인해준 것뿐이라 특별히 할 일은 없다.
        }

        private void HandleMatched(string matchId, int seed, string opponentName)
        {
            currentMatchId = matchId;
            currentOpponentName = opponentName;
            ShowGame();
            CurrentGame.BeginRound(seed);
        }

        private void HandleRoundEnded(int finalScore)
        {
            if (isVersusMatch)
            {
                ShowWaitingForResult();
                network.SubmitScore(currentMatchId, finalScore);
            }
            else
            {
                ShowResult(finalScore);

                string name = ResolveNickname();
                if (selectedGame.SupportsVersusMode())
                {
                    // 대전과 동일한 서버 리더보드에, 매치 없이 바로 기록한다 (같은
                    // 닉네임이면 서버가 더 높은 점수만 남긴다). 등수는 서버가
                    // leaderboard_rank로 알려주면 HandleLeaderboardRank에서 채운다.
                    EnsureConnectedThen(() => network.SubmitSoloScore(name, selectedGame.ToServerId(), finalScore));
                }
                else
                {
                    // 대전이 없는 게임은 서버 리더보드가 없다 - 기기에만 기록하고,
                    // 등수도 서버 응답 없이 바로 계산해서 채운다.
                    LocalLeaderboardStore.RecordScore(selectedGame, name, finalScore);
                    var (rank, total) = LocalLeaderboardStore.GetRank(selectedGame, name);
                    resultPopup.SetRank(rank, total);
                }
            }
        }

        private void HandleMatchResult(string result, int yourScore, int opponentScore)
        {
            // 승/패/무나 상대 점수는 지금 결과 팝업엔 별도 자리가 없다 - 일단 내 점수와
            // 등수만 다른 결과 화면과 동일하게 보여준다. 등수는 곧 이어서 서버가 보내는
            // leaderboard_rank로 채워진다.
            ShowResult(yourScore);
        }

        private void HandleLeaderboardRank(string game, int rank, int total)
        {
            // 게임을 바꾼 뒤에 뒤늦게 도착한 응답이면 무시한다.
            if (game != selectedGame.ToServerId())
                return;

            resultPopup.SetRank(rank, total);
        }

        private void HandleNetworkError(string message)
        {
            Debug.LogError($"[AppFlowManager] 네트워크 오류: {message}");
            if (matchmakingPopup.gameObject.activeSelf)
                matchmakingPopup.ShowError(message);
        }

        // ----------------------------------------------------------------
        // 닉네임
        // ----------------------------------------------------------------

        /// <summary>입력 UI 없이 최초 1회 자동으로 만들어 PlayerPrefs에 저장해두고 계속
        /// 재사용한다 (대전 상대에게 보이는 이름이자 리더보드에 올라가는 이름).</summary>
        private string ResolveNickname()
        {
            string saved = PlayerPrefs.GetString(NicknamePrefKey, string.Empty);
            if (!string.IsNullOrEmpty(saved))
                return saved;

            string name = "Player" + UnityEngine.Random.Range(1000, 9999);
            PlayerPrefs.SetString(NicknamePrefKey, name);
            return name;
        }
    }
}
