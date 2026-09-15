using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// 앱 전체 흐름을 관리한다: 메뉴(싱글/대전/리더보드) -> (대전이면) 매칭 대기 ->
    /// 실제 플레이(Match3GameManager) -> 결과 화면 -> 다시 메뉴.
    ///
    /// 씬에 아무것도 없어도 Play를 누르면 자동으로 생성된다 (InGameScene 한정).
    /// </summary>
    public class AppFlowManager : MonoBehaviour
    {
        [Header("대전 서버")]
        public string serverUrl = "wss://bomoonsan-match3.fly.dev";

        private const string GameplaySceneName = "InGameScene";
        private const string NicknamePrefKey = "match3_nickname";

        // 팝업 카드(복주머니 프레임) 배경이 흰색이라, 그 위에 올라가는 글자는
        // 기본 흰 글씨(UIFactory.CreateText 기본값) 대신 어두운 색을 써야 잘 보인다.
        private static readonly Color PopupTextColor = new Color(0.24f, 0.16f, 0.08f);

        private Match3GameManager gameManager;
        private NetworkClient network;

        private GameObject appCanvasRoot;
        private GameObject menuPanel;
        private GameObject matchmakingPanel;
        private GameObject resultPanel;
        private GameObject leaderboardPanel;

        private InputField nicknameInputField;
        private Text matchmakingText;
        private Button cancelMatchmakingButton;
        private Text resultTitleText;
        private Text resultDetailText;
        private Text leaderboardListText;

        private bool isVersusMatch;
        private string currentMatchId;
        private string currentOpponentName;
        private Action pendingOnOpenAction;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (SceneManager.GetActiveScene().name != GameplaySceneName)
                return;

            if (UnityEngine.Object.FindFirstObjectByType<AppFlowManager>() != null)
                return;

            var go = new GameObject("AppFlowManager");
            go.AddComponent<AppFlowManager>();
        }

        private void Awake()
        {
            EnsureEventSystem();
            BuildUI();

            // 씬에 미리 배치된(인스펙터에서 값 조정용) Match3GameManager가 있으면 그걸 쓰고,
            // 없으면 새로 만든다.
            gameManager = UnityEngine.Object.FindFirstObjectByType<Match3GameManager>();
            if (gameManager == null)
            {
                var gameManagerGo = new GameObject("Match3GameManager");
                gameManagerGo.transform.SetParent(transform, false);
                gameManager = gameManagerGo.AddComponent<Match3GameManager>();
            }
            gameManager.RoundEnded += HandleRoundEnded;

            var networkGo = new GameObject("NetworkClient");
            networkGo.transform.SetParent(transform, false);
            network = networkGo.AddComponent<NetworkClient>();
            network.serverUrl = serverUrl;
            network.OnOpen += HandleNetworkOpen;
            network.OnQueued += HandleQueued;
            network.OnMatched += HandleMatched;
            network.OnMatchResult += HandleMatchResult;
            network.OnLeaderboard += HandleLeaderboard;
            network.OnError += HandleNetworkError;

            LoadNickname();
            ShowMenu();
        }

        private void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            // 이 프로젝트는 새 Input System만 사용하도록 설정되어 있으므로
            // 레거시 StandaloneInputModule 대신 InputSystemUIInputModule을 사용한다.
            var uiModule = eventSystemGo.AddComponent<InputSystemUIInputModule>();
            uiModule.AssignDefaultActions();
        }

        // ----------------------------------------------------------------
        // UI 생성
        // ----------------------------------------------------------------

        private void BuildUI()
        {
            var canvasGo = new GameObject("AppCanvas");
            canvasGo.transform.SetParent(transform, false);
            appCanvasRoot = canvasGo;

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            var background = UIFactory.CreateImage("Background", canvasGo.transform, new Color(0.10f, 0.11f, 0.15f));
            UIFactory.StretchFull(background.rectTransform);

            BuildMenuPanel(canvasGo.transform);
            BuildMatchmakingPanel(canvasGo.transform);
            BuildResultPanel(canvasGo.transform);
            BuildLeaderboardPanel(canvasGo.transform);
        }

        private static GameObject CreateFullscreenPanel(string name, Transform parent, Color bgColor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            UIFactory.StretchFull(rt);

            if (bgColor.a > 0f)
            {
                var image = go.AddComponent<Image>();
                image.color = bgColor;
            }

            return go;
        }

        // 아래 각 패널은 전부 부모 중심(0,0)을 기준으로 한 픽셀 오프셋으로 배치한다.
        // 0~1 비율 기준 배치는 화면 비율(가로로 넓은 창 등)에 따라 실제 캔버스 높이가
        // 크게 달라져서 요소끼리 겹칠 수 있었는데(보드에서 겪었던 것과 같은 문제),
        // 고정 픽셀 오프셋은 캔버스 크기와 무관하게 항상 같은 간격을 유지한다.

        private void BuildMenuPanel(Transform parent)
        {
            menuPanel = CreateFullscreenPanel("MenuPanel", parent, new Color(0, 0, 0, 0));

            UIFactory.CreateTextAt(
                "Title", menuPanel.transform, "보문산 3매치 퍼즐", 76, TextAnchor.MiddleCenter,
                new Vector2(0, 480), new Vector2(900, 150));

            nicknameInputField = UIFactory.CreateInputField(
                "NicknameInput", menuPanel.transform, "닉네임 (대전용)", new Vector2(0, 220), new Vector2(600, 110));

            var singleButton = UIFactory.CreateButton("SingleButton", menuPanel.transform, "싱글 플레이", new Vector2(0, 80), new Vector2(560, 120));
            singleButton.onClick.AddListener(OnSinglePlayClicked);

            var versusButton = UIFactory.CreateButton("VersusButton", menuPanel.transform, "대전 플레이", new Vector2(0, -60), new Vector2(560, 120));
            versusButton.onClick.AddListener(OnVersusPlayClicked);

            var leaderboardButton = UIFactory.CreateButton("LeaderboardButton", menuPanel.transform, "리더보드", new Vector2(0, -200), new Vector2(560, 120));
            leaderboardButton.onClick.AddListener(OnLeaderboardClicked);
        }

        private void BuildMatchmakingPanel(Transform parent)
        {
            matchmakingPanel = CreateFullscreenPanel("MatchmakingPanel", parent, new Color(0, 0, 0, 0.6f));
            UIFactory.CreatePopupCard("Card", matchmakingPanel.transform, new Vector2(800, 500));

            matchmakingText = UIFactory.CreateTextAt(
                "MatchmakingText", matchmakingPanel.transform, "상대를 찾는 중...", 60, TextAnchor.MiddleCenter,
                new Vector2(0, 60), new Vector2(900, 150));
            matchmakingText.color = PopupTextColor;

            cancelMatchmakingButton = UIFactory.CreateButton("CancelButton", matchmakingPanel.transform, "취소", new Vector2(0, -100), new Vector2(300, 120));
            cancelMatchmakingButton.onClick.AddListener(OnCancelMatchmakingClicked);

            matchmakingPanel.SetActive(false);
        }

        private void BuildResultPanel(Transform parent)
        {
            resultPanel = CreateFullscreenPanel("ResultPanel", parent, new Color(0, 0, 0, 0.75f));
            UIFactory.CreatePopupCard("Card", resultPanel.transform, new Vector2(900, 650));

            resultTitleText = UIFactory.CreateTextAt(
                "ResultTitle", resultPanel.transform, "게임 종료", 88, TextAnchor.MiddleCenter,
                new Vector2(0, 180), new Vector2(900, 150));
            resultTitleText.color = PopupTextColor;

            resultDetailText = UIFactory.CreateTextAt(
                "ResultDetail", resultPanel.transform, "점수: 0", 48, TextAnchor.MiddleCenter,
                new Vector2(0, 30), new Vector2(900, 100));
            resultDetailText.color = PopupTextColor;

            var menuButton = UIFactory.CreateButton("ResultMenuButton", resultPanel.transform, "메뉴로", new Vector2(0, -150), new Vector2(360, 130));
            menuButton.onClick.AddListener(ShowMenu);

            resultPanel.SetActive(false);
        }

        private void BuildLeaderboardPanel(Transform parent)
        {
            leaderboardPanel = CreateFullscreenPanel("LeaderboardPanel", parent, new Color(0, 0, 0, 0.85f));
            UIFactory.CreatePopupCard("Card", leaderboardPanel.transform, new Vector2(850, 950));

            var leaderboardTitle = UIFactory.CreateTextAt(
                "LeaderboardTitle", leaderboardPanel.transform, "리더보드", 76, TextAnchor.MiddleCenter,
                new Vector2(0, 300), new Vector2(700, 110));
            leaderboardTitle.color = PopupTextColor;

            leaderboardListText = UIFactory.CreateTextAt(
                "LeaderboardList", leaderboardPanel.transform, "불러오는 중...", 40, TextAnchor.UpperCenter,
                new Vector2(0, 10), new Vector2(700, 420));
            leaderboardListText.color = PopupTextColor;

            var closeButton = UIFactory.CreateButton("LeaderboardCloseButton", leaderboardPanel.transform, "닫기", new Vector2(0, -300), new Vector2(300, 120));
            closeButton.onClick.AddListener(ShowMenu);

            leaderboardPanel.SetActive(false);
        }

        // ----------------------------------------------------------------
        // 화면 전환
        // ----------------------------------------------------------------

        private void SetActivePanel(GameObject panel)
        {
            menuPanel.SetActive(panel == menuPanel);
            matchmakingPanel.SetActive(panel == matchmakingPanel);
            resultPanel.SetActive(panel == resultPanel);
            leaderboardPanel.SetActive(panel == leaderboardPanel);
            appCanvasRoot.SetActive(panel != null);
            gameManager.SetVisible(panel == null);
        }

        private void ShowMenu()
        {
            if (network.IsConnected)
                network.LeaveQueue();

            isVersusMatch = false;
            SetActivePanel(menuPanel);
        }

        private void ShowMatchmaking()
        {
            SetActivePanel(matchmakingPanel);
            matchmakingText.text = "상대를 찾는 중...";
            cancelMatchmakingButton.gameObject.SetActive(true);
        }

        private void ShowWaitingForResult()
        {
            SetActivePanel(matchmakingPanel);
            matchmakingText.text = "결과를 기다리는 중...";
            cancelMatchmakingButton.gameObject.SetActive(false);
        }

        private void ShowGame()
        {
            SetActivePanel(null);
        }

        private void ShowLeaderboardLoading()
        {
            SetActivePanel(leaderboardPanel);
            leaderboardListText.text = "불러오는 중...";
        }

        // ----------------------------------------------------------------
        // 버튼 핸들러
        // ----------------------------------------------------------------

        private void OnSinglePlayClicked()
        {
            isVersusMatch = false;
            ShowGame();
            gameManager.BeginRound();
        }

        private void OnVersusPlayClicked()
        {
            isVersusMatch = true;
            string name = ResolveNickname();
            ShowMatchmaking();
            EnsureConnectedThen(() => network.JoinQueue(name));
        }

        private void OnLeaderboardClicked()
        {
            ShowLeaderboardLoading();
            EnsureConnectedThen(() => network.RequestLeaderboard());
        }

        private void OnCancelMatchmakingClicked()
        {
            network.LeaveQueue();
            ShowMenu();
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
            gameManager.BeginRound(seed);
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
                SetActivePanel(resultPanel);
                resultTitleText.text = "게임 종료";
                resultDetailText.text = $"점수: {finalScore}";
            }
        }

        private void HandleMatchResult(string result, int yourScore, int opponentScore)
        {
            SetActivePanel(resultPanel);
            resultTitleText.text = result switch
            {
                "win" => "승리했습니다!",
                "lose" => "패배했습니다",
                _ => "무승부",
            };
            resultDetailText.text = $"내 점수: {yourScore}   상대({currentOpponentName}) 점수: {opponentScore}";
        }

        private void HandleLeaderboard(List<LeaderboardEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                leaderboardListText.text = "아직 기록이 없습니다.";
                return;
            }

            var sb = new StringBuilder();
            int rank = 1;
            foreach (var entry in entries.Take(10))
            {
                sb.AppendLine($"{rank}. {entry.name} - {entry.score}");
                rank++;
            }
            leaderboardListText.text = sb.ToString();
        }

        private void HandleNetworkError(string message)
        {
            Debug.LogError($"[AppFlowManager] 네트워크 오류: {message}");
            if (matchmakingPanel.activeSelf)
            {
                matchmakingText.text = $"연결 오류: {message}\n(취소를 눌러 메뉴로 돌아가세요)";
                cancelMatchmakingButton.gameObject.SetActive(true);
            }
        }

        // ----------------------------------------------------------------
        // 닉네임 저장
        // ----------------------------------------------------------------

        private void LoadNickname()
        {
            string saved = PlayerPrefs.GetString(NicknamePrefKey, string.Empty);
            if (!string.IsNullOrEmpty(saved))
                nicknameInputField.text = saved;
        }

        private string ResolveNickname()
        {
            string name = nicknameInputField.text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                name = "Player" + UnityEngine.Random.Range(1000, 9999);
                nicknameInputField.text = name;
            }

            PlayerPrefs.SetString(NicknamePrefKey, name);
            return name;
        }
    }
}
