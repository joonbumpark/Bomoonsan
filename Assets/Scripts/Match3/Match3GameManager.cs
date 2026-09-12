using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// 3매치 퍼즐의 전체 흐름을 담당한다: UI 생성, 입력 처리, 스왑/매치/낙하/리필 애니메이션,
    /// 점수와 이동 횟수 관리, 승리/패배 처리.
    ///
    /// 씬에 아무것도 준비되어 있지 않아도 동작한다 — 게임을 Play 하면 AutoBootstrap이
    /// 자동으로 이 컴포넌트를 가진 오브젝트를 만들어주기 때문에, 별도의 씬 세팅 없이
    /// 바로 Play 버튼만 누르면 퍼즐이 생성된다.
    /// </summary>
    public class Match3GameManager : MonoBehaviour
    {
        [Header("보드 설정")]
        [Min(4)] public int width = 8;
        [Min(4)] public int height = 8;
        [Range(3, 6)] public int tileTypeCount = 5;
        public float cellSize = 110f;
        public float cellSpacing = 8f;

        [Header("게임 규칙")]
        [Tooltip("0이면 이동 횟수 제한이 없다.")]
        public int moveLimit = 20;
        public int targetScore = 1000;

        [Header("입력")]
        [Tooltip("드래그가 이 거리(스크린 픽셀)를 넘으면 스와이프로 인식해 스왑을 시도한다.")]
        public float swipeThreshold = 60f;

        [Header("애니메이션 속도")]
        public float swapDuration = 0.15f;
        public float fallDuration = 0.25f;
        public float clearDuration = 0.15f;

        private static readonly Color[] Palette =
        {
            new Color(0.91f, 0.30f, 0.24f), // 빨강
            new Color(0.95f, 0.61f, 0.07f), // 주황
            new Color(0.95f, 0.87f, 0.20f), // 노랑
            new Color(0.30f, 0.69f, 0.31f), // 초록
            new Color(0.20f, 0.60f, 0.86f), // 파랑
            new Color(0.61f, 0.35f, 0.71f), // 보라
        };

        private Match3Board board;
        private TileView[,] views;
        private RectTransform boardRoot;

        private Text scoreText;
        private Text movesText;
        private GameObject endPanel;
        private Text endTitleText;
        private Text endScoreText;

        private int score;
        private int movesLeft;
        private bool inputLocked;
        private bool gameOver;

        // 이 씬에서 Play를 눌렀을 때만 퍼즐이 자동으로 생성된다.
        // 다른 씬(예: SampleScene)에서 Play를 눌러도 아무 일도 일어나지 않는다.
        private const string GameplaySceneName = "InGameScene";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (SceneManager.GetActiveScene().name != GameplaySceneName)
                return;

            if (Object.FindFirstObjectByType<Match3GameManager>() != null)
                return;

            var go = new GameObject("Match3GameManager");
            go.AddComponent<Match3GameManager>();
        }

        private void Awake()
        {
            EnsureEventSystem();
            BuildUI();
            StartNewGame();
        }

        private void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            // 이 프로젝트는 새 Input System만 사용하도록 설정되어 있으므로
            // 레거시 StandaloneInputModule 대신 InputSystemUIInputModule을 사용한다.
            var uiModule = eventSystemGo.AddComponent<InputSystemUIInputModule>();
            uiModule.AssignDefaultActions();
        }

        private void StartNewGame()
        {
            score = 0;
            movesLeft = moveLimit;
            gameOver = false;
            inputLocked = false;

            int typeCount = Mathf.Clamp(tileTypeCount, 3, Palette.Length);
            board = new Match3Board(width, height, typeCount);

            BuildBoardViews();
            UpdateHud();
            endPanel.SetActive(false);
        }

        // ----------------------------------------------------------------
        // UI 생성
        // ----------------------------------------------------------------

        private void BuildUI()
        {
            var canvasGo = new GameObject("Match3Canvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            var background = CreateImage("Background", canvasGo.transform, new Color(0.10f, 0.11f, 0.15f));
            StretchFull(background.rectTransform);

            BuildTopBar(canvasGo.transform);
            BuildBoardRoot(canvasGo.transform);
            BuildHint(canvasGo.transform);
            BuildEndPanel(canvasGo.transform);
        }

        private void BuildTopBar(Transform parent)
        {
            var topBar = CreateRect("TopBar", parent);
            topBar.anchorMin = new Vector2(0, 1);
            topBar.anchorMax = new Vector2(1, 1);
            topBar.pivot = new Vector2(0.5f, 1);
            topBar.sizeDelta = new Vector2(0, 220);
            topBar.anchoredPosition = Vector2.zero;

            scoreText = CreateText("ScoreText", topBar, "점수: 0", 56, TextAnchor.MiddleLeft);
            var scoreRt = scoreText.rectTransform;
            scoreRt.anchorMin = new Vector2(0, 0);
            scoreRt.anchorMax = new Vector2(0.5f, 1);
            scoreRt.offsetMin = new Vector2(40, 0);
            scoreRt.offsetMax = Vector2.zero;

            movesText = CreateText("MovesText", topBar, "이동 횟수: 20", 56, TextAnchor.MiddleRight);
            var movesRt = movesText.rectTransform;
            movesRt.anchorMin = new Vector2(0.5f, 0);
            movesRt.anchorMax = new Vector2(1, 1);
            movesRt.offsetMin = Vector2.zero;
            movesRt.offsetMax = new Vector2(-40, 0);
        }

        private void BuildBoardRoot(Transform parent)
        {
            boardRoot = CreateRect("BoardRoot", parent);
            boardRoot.anchorMin = boardRoot.anchorMax = new Vector2(0.5f, 0.5f);
            boardRoot.pivot = new Vector2(0.5f, 0.5f);

            float boardW = width * cellSize + (width - 1) * cellSpacing;
            float boardH = height * cellSize + (height - 1) * cellSpacing;
            boardRoot.sizeDelta = new Vector2(boardW, boardH);
            boardRoot.anchoredPosition = new Vector2(0, -60);

            var boardBg = CreateImage("BoardBackground", boardRoot, new Color(0, 0, 0, 0.25f));
            StretchFull(boardBg.rectTransform);
        }

        private void BuildHint(Transform parent)
        {
            var hint = CreateText("HintText", parent, "타일을 드래그해서 인접한 타일과 교환하세요", 40, TextAnchor.MiddleCenter);
            hint.color = new Color(1f, 1f, 1f, 0.6f);
            var rt = hint.rectTransform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(0, 100);
            rt.anchoredPosition = new Vector2(0, 40);
        }

        private void BuildEndPanel(Transform parent)
        {
            endPanel = new GameObject("EndPanel", typeof(RectTransform));
            var rt = (RectTransform)endPanel.transform;
            rt.SetParent(parent, false);
            StretchFull(rt);

            var bg = endPanel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.75f);

            endTitleText = CreateText("EndTitle", rt, "게임 종료", 96, TextAnchor.MiddleCenter);
            var titleRt = endTitleText.rectTransform;
            titleRt.anchorMin = new Vector2(0.1f, 0.56f);
            titleRt.anchorMax = new Vector2(0.9f, 0.72f);
            titleRt.offsetMin = titleRt.offsetMax = Vector2.zero;

            endScoreText = CreateText("EndScore", rt, "최종 점수: 0", 60, TextAnchor.MiddleCenter);
            var scoreRt = endScoreText.rectTransform;
            scoreRt.anchorMin = new Vector2(0.1f, 0.44f);
            scoreRt.anchorMax = new Vector2(0.9f, 0.56f);
            scoreRt.offsetMin = scoreRt.offsetMax = Vector2.zero;

            var restartButton = CreateButton("RestartButton", rt, "다시 시작", new Vector2(0.5f, 0.32f));
            restartButton.onClick.AddListener(StartNewGame);

            endPanel.SetActive(false);
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var rt = CreateRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Text CreateText(string name, Transform parent, string content, int fontSize, TextAnchor anchor)
        {
            var rt = CreateRect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, Vector2 anchorCenter)
        {
            var rt = CreateRect(name, parent);
            rt.anchorMin = rt.anchorMax = anchorCenter;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(420, 140);

            var image = rt.gameObject.AddComponent<Image>();
            image.color = new Color(0.20f, 0.60f, 0.86f);

            var button = rt.gameObject.AddComponent<Button>();

            var labelText = CreateText(name + "_Label", rt, label, 52, TextAnchor.MiddleCenter);
            StretchFull(labelText.rectTransform);

            return button;
        }

        // ----------------------------------------------------------------
        // 보드 뷰 생성/배치
        // ----------------------------------------------------------------

        private void BuildBoardViews()
        {
            if (views != null)
            {
                foreach (var view in views)
                {
                    if (view != null)
                        Destroy(view.gameObject);
                }
            }

            views = new TileView[width, height];
            for (int col = 0; col < width; col++)
            {
                for (int row = 0; row < height; row++)
                {
                    views[col, row] = CreateTileView(col, row, board.GetType(col, row));
                }
            }
        }

        private TileView CreateTileView(int col, int row, int type)
        {
            var go = new GameObject($"Tile_{col}_{row}", typeof(RectTransform));
            go.transform.SetParent(boardRoot, false);

            go.AddComponent<Image>();
            var view = go.AddComponent<TileView>();

            var rt = (RectTransform)go.transform;
            // CellToLocalPos가 보드 중심을 (0,0)으로 계산하므로, anchor도 보드루트 중심에 맞춘다.
            // (좌하단(0,0)으로 두면 그만큼 좌표가 추가로 밀려서 보드의 우상단 부분만 화면에 보이게 된다.)
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(cellSize, cellSize);
            rt.anchoredPosition = CellToLocalPos(col, row);

            view.Init(this, col, row, type, Palette[type]);
            return view;
        }

        private Vector2 CellToLocalPos(int col, int row)
        {
            float boardW = width * cellSize + (width - 1) * cellSpacing;
            float boardH = height * cellSize + (height - 1) * cellSpacing;
            float x = -boardW / 2f + cellSize / 2f + col * (cellSize + cellSpacing);
            float y = -boardH / 2f + cellSize / 2f + row * (cellSize + cellSpacing);
            return new Vector2(x, y);
        }

        // ----------------------------------------------------------------
        // 입력 처리
        // ----------------------------------------------------------------

        /// <summary>타일을 direction 방향으로 드래그했을 때 TileView가 호출한다.</summary>
        public void RequestSwap(TileView tile, Vector2Int direction)
        {
            if (inputLocked || gameOver)
                return;

            int targetCol = tile.Col + direction.x;
            int targetRow = tile.Row + direction.y;
            if (targetCol < 0 || targetCol >= width || targetRow < 0 || targetRow >= height)
                return;

            var targetTile = views[targetCol, targetRow];
            if (targetTile == null)
                return;

            tile.SetSelected(false);
            StartCoroutine(TrySwap(tile, targetTile));
        }

        // ----------------------------------------------------------------
        // 스왑 / 매치 / 낙하 / 리필
        // ----------------------------------------------------------------

        private IEnumerator TrySwap(TileView tileA, TileView tileB)
        {
            inputLocked = true;

            var a = new Vector2Int(tileA.Col, tileA.Row);
            var b = new Vector2Int(tileB.Col, tileB.Row);

            yield return StartCoroutine(AnimateSwapVisual(tileA, tileB));
            board.Swap(a, b);
            SwapViews(tileA, tileB);

            if (board.FindMatches().Count == 0)
            {
                // 매치가 만들어지지 않으면 원래대로 되돌린다.
                yield return StartCoroutine(AnimateSwapVisual(tileA, tileB));
                board.Swap(a, b);
                SwapViews(tileA, tileB);
                inputLocked = false;
                yield break;
            }

            if (moveLimit > 0)
                movesLeft--;

            yield return StartCoroutine(ResolveMatches());

            if (!gameOver && !board.HasAnyValidMove())
            {
                board.Shuffle();
                RefreshAllViews();
            }

            UpdateHud();
            CheckEndConditions();
            inputLocked = false;
        }

        private void SwapViews(TileView a, TileView b)
        {
            int aCol = a.Col, aRow = a.Row;
            int bCol = b.Col, bRow = b.Row;

            views[aCol, aRow] = b;
            views[bCol, bRow] = a;

            a.SetPosition(bCol, bRow);
            b.SetPosition(aCol, aRow);
        }

        private IEnumerator AnimateSwapVisual(TileView a, TileView b)
        {
            Vector2 aStart = a.RectTransform.anchoredPosition;
            Vector2 bStart = b.RectTransform.anchoredPosition;

            float t = 0f;
            while (t < swapDuration)
            {
                t += Time.deltaTime;
                float e = EaseOutQuad(Mathf.Clamp01(t / swapDuration));
                a.RectTransform.anchoredPosition = Vector2.Lerp(aStart, bStart, e);
                b.RectTransform.anchoredPosition = Vector2.Lerp(bStart, aStart, e);
                yield return null;
            }

            a.RectTransform.anchoredPosition = bStart;
            b.RectTransform.anchoredPosition = aStart;
        }

        private IEnumerator ResolveMatches()
        {
            int chain = 0;

            while (true)
            {
                var matches = board.FindMatches();
                if (matches.Count == 0)
                    break;

                chain++;

                yield return StartCoroutine(AnimateClear(matches));

                board.Clear(matches);
                foreach (var pos in matches)
                {
                    var view = views[pos.x, pos.y];
                    if (view != null)
                    {
                        Destroy(view.gameObject);
                        views[pos.x, pos.y] = null;
                    }
                }

                score += matches.Count * 10 * chain;

                var moves = board.CollapseColumns();
                ApplyCollapseToViews(moves);
                yield return StartCoroutine(AnimateFall(moves.Select(m => views[m.to.x, m.to.y])));

                var spawns = board.RefillEmpties();
                SpawnNewTiles(spawns);
                yield return StartCoroutine(AnimateFall(spawns.Select(s => views[s.pos.x, s.pos.y])));

                UpdateHud();
                yield return new WaitForSeconds(0.05f);
            }
        }

        private IEnumerator AnimateClear(IEnumerable<Vector2Int> cells)
        {
            var tiles = cells.Select(p => views[p.x, p.y]).Where(v => v != null).ToList();
            if (tiles.Count == 0)
                yield break;

            float t = 0f;
            while (t < clearDuration)
            {
                t += Time.deltaTime;
                float scale = Mathf.Lerp(1f, 0f, t / clearDuration);
                foreach (var tile in tiles)
                    tile.RectTransform.localScale = Vector3.one * scale;
                yield return null;
            }
        }

        private void ApplyCollapseToViews(List<(Vector2Int from, Vector2Int to)> moves)
        {
            foreach (var (from, to) in moves)
            {
                var view = views[from.x, from.y];
                views[from.x, from.y] = null;
                views[to.x, to.y] = view;
                if (view != null)
                    view.SetPosition(to.x, to.y);
            }
        }

        private void SpawnNewTiles(List<(Vector2Int pos, int type)> spawns)
        {
            foreach (var (pos, type) in spawns)
            {
                var view = CreateTileView(pos.x, pos.y, type);
                // 보드 위쪽에서 떨어져 내려오는 것처럼 보이도록 시작 위치를 살짝 위로 잡는다.
                view.RectTransform.anchoredPosition = CellToLocalPos(pos.x, pos.y + 3);
                views[pos.x, pos.y] = view;
            }
        }

        private IEnumerator AnimateFall(IEnumerable<TileView> tiles)
        {
            var list = tiles.Where(v => v != null).ToList();
            if (list.Count == 0)
                yield break;

            var starts = list.Select(v => v.RectTransform.anchoredPosition).ToList();
            var targets = list.Select(v => CellToLocalPos(v.Col, v.Row)).ToList();

            float t = 0f;
            while (t < fallDuration)
            {
                t += Time.deltaTime;
                float e = EaseOutQuad(Mathf.Clamp01(t / fallDuration));
                for (int i = 0; i < list.Count; i++)
                    list[i].RectTransform.anchoredPosition = Vector2.Lerp(starts[i], targets[i], e);
                yield return null;
            }

            for (int i = 0; i < list.Count; i++)
                list[i].RectTransform.anchoredPosition = targets[i];
        }

        private void RefreshAllViews()
        {
            for (int col = 0; col < width; col++)
            {
                for (int row = 0; row < height; row++)
                {
                    var view = views[col, row];
                    if (view == null)
                        continue;
                    int type = board.GetType(col, row);
                    view.SetType(type, Palette[type]);
                }
            }
        }

        private static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);

        // ----------------------------------------------------------------
        // HUD / 게임 종료
        // ----------------------------------------------------------------

        private void UpdateHud()
        {
            scoreText.text = $"점수: {score}";
            movesText.text = moveLimit > 0 ? $"이동 횟수: {Mathf.Max(0, movesLeft)}" : "이동 횟수: ∞";
        }

        private void CheckEndConditions()
        {
            if (gameOver)
                return;

            if (score >= targetScore)
                EndGame(true);
            else if (moveLimit > 0 && movesLeft <= 0)
                EndGame(false);
        }

        private void EndGame(bool win)
        {
            gameOver = true;
            endTitleText.text = win ? "승리했습니다!" : "게임 종료";
            endScoreText.text = $"최종 점수: {score}";
            endPanel.SetActive(true);
        }
    }
}
