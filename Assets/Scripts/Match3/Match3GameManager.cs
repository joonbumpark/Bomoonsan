using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// 실제 퍼즐 한 판(보드+HUD+입력)만 담당한다. 메뉴/매칭/결과 화면 같은 전체 앱 흐름은
    /// AppFlowManager가 맡고, 이 클래스는 AppFlowManager가 BeginRound()를 호출해줘야 시작한다.
    ///
    /// 라운드는 정해진 시간(roundDurationSeconds) 동안 진행되고, 시간이 다 되면
    /// RoundEnded 이벤트로 최종 점수를 알린다. 싱글/대전 모두 같은 규칙(시간제)을 쓰며,
    /// 대전에서는 BeginRound(seed)에 서버가 내려준 시드를 넣어 양쪽이 같은 보드로 시작한다.
    /// </summary>
    public class Match3GameManager : MonoBehaviour, IRoundGame
    {
        [Header("보드 설정")]
        [Min(4)] public int width = 8;
        [Min(4)] public int height = 8;
        [Range(3, 6)] public int tileTypeCount = 5;
        public float cellSize = 110f;
        public float cellSpacing = 8f;

        [Header("라운드 설정")]
        public float roundDurationSeconds = 90f;

        [Header("입력")]
        [Tooltip("드래그가 이 거리(스크린 픽셀)를 넘으면 스와이프로 인식해 스왑을 시도한다.")]
        public float swipeThreshold = 60f;

        [Header("애니메이션 속도")]
        public float swapDuration = 0.15f;
        public float fallDuration = 0.25f;
        public float clearDuration = 0.15f;

        /// <summary>라운드가 시간 종료로 끝났을 때 최종 점수와 함께 호출된다.</summary>
        public event Action<int> RoundEnded;

        public int CurrentScore => score;

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
        private GameObject canvasRoot;

        private Text scoreText;
        private Text timerText;

        private int score;
        private bool inputLocked;
        private bool roundActive;
        private float timeRemaining;
        private int lastDisplayedSeconds;

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
        }

        /// <summary>게임 화면을 보이거나 숨긴다. AppFlowManager가 메뉴/결과 화면과 전환할 때 쓴다.</summary>
        public void SetVisible(bool visible)
        {
            canvasRoot.SetActive(visible);
        }

        /// <summary>
        /// 새 라운드를 시작한다. seed를 지정하면(대전 모드) 그 시드로 보드를 생성해
        /// 양쪽 플레이어가 같은 배치로 시작하고, null이면(싱글 모드) 매번 랜덤하게 생성한다.
        /// </summary>
        public void BeginRound(int? seed = null)
        {
            StopAllCoroutines();

            score = 0;
            inputLocked = false;
            roundActive = true;
            timeRemaining = roundDurationSeconds;
            lastDisplayedSeconds = -1;

            int typeCount = Mathf.Clamp(tileTypeCount, 3, Palette.Length);
            board = new Match3Board(width, height, typeCount, seed);

            BuildBoardViews();
            UpdateHud();
            SetVisible(true);
        }

        private void Update()
        {
            if (!roundActive)
                return;

            timeRemaining -= Time.deltaTime;
            if (timeRemaining <= 0f)
            {
                timeRemaining = 0f;
                roundActive = false;
                inputLocked = true;
                StopAllCoroutines(); // 진행 중이던 스왑/연쇄 애니메이션을 즉시 멈춰서 시간 종료 이후 점수가 더 안 오르게 한다.
                UpdateHud();
                RoundEnded?.Invoke(score);
                return;
            }

            UpdateHud();
        }

        // ----------------------------------------------------------------
        // UI 생성
        // ----------------------------------------------------------------

        private void BuildUI()
        {
            canvasRoot = UIFactory.CreateGameCanvasRoot(transform, "Match3Canvas");
            UIFactory.CreateGameTopBar(canvasRoot.transform, TopBarHeight, out scoreText, out timerText);
            BuildBoardRoot(canvasRoot.transform);
            UIFactory.CreateGameHint(canvasRoot.transform, HintBarHeight, "드래그로 교환, 아이템 블록은 탭하거나 옮기면 발동!");
        }

        // 상단 바/하단 안내문구가 차지하는 고정 높이. 보드 크기를 화면에 맞출 때도 사용한다.
        private const float TopBarHeight = 220f;
        private const float HintBarHeight = 100f;

        private void BuildBoardRoot(Transform parent)
        {
            boardRoot = UIFactory.CreateRect("BoardRoot", parent);
            boardRoot.anchorMin = boardRoot.anchorMax = new Vector2(0.5f, 0.5f);
            boardRoot.pivot = new Vector2(0.5f, 0.5f);

            float boardW = width * cellSize + (width - 1) * cellSpacing;
            float boardH = height * cellSize + (height - 1) * cellSpacing;
            boardRoot.sizeDelta = new Vector2(boardW, boardH);
            // 상단 바와 하단 안내문구 사이 정중앙에 오도록 고정 오프셋을 준다.
            boardRoot.anchoredPosition = new Vector2(0, (HintBarHeight - TopBarHeight) / 2f);

            var boardBg = UIFactory.CreateImage("BoardBackground", boardRoot, new Color(0, 0, 0, 0.25f));
            UIFactory.StretchFull(boardBg.rectTransform);

            // 세로로 좁거나(가로로 긴 창 등) 화면 비율이 기준 해상도와 많이 다르면
            // 보드가 상단 바/안내문구와 겹칠 수 있으므로, 남는 공간에 맞춰 통째로 축소한다.
            Canvas.ForceUpdateCanvases();
            var canvasRect = (RectTransform)parent;
            float margin = 40f;
            float safeWidth = canvasRect.rect.width - margin * 2f;
            float safeHeight = canvasRect.rect.height - TopBarHeight - HintBarHeight - margin;
            float scale = Mathf.Min(1f, safeWidth / boardW, safeHeight / boardH);
            boardRoot.localScale = Vector3.one * scale;
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
            if (inputLocked || !roundActive)
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

        /// <summary>아이템 블록을 드래그가 아닌 짧은 탭으로 클릭했을 때 TileView가 호출한다.</summary>
        public void RequestActivateItem(TileView tile)
        {
            if (inputLocked || !roundActive || tile.Item == ItemType.None)
                return;

            StartCoroutine(ActivateItemRoutine(tile));
        }

        // ----------------------------------------------------------------
        // 스왑 / 매치 / 낙하 / 리필
        // ----------------------------------------------------------------

        private IEnumerator TrySwap(TileView tileA, TileView tileB)
        {
            inputLocked = true;

            var a = new Vector2Int(tileA.Col, tileA.Row);
            var b = new Vector2Int(tileB.Col, tileB.Row);
            bool involvesItem = tileA.Item != ItemType.None || tileB.Item != ItemType.None;

            yield return StartCoroutine(AnimateSwapVisual(tileA, tileB));
            board.Swap(a, b);
            SwapViews(tileA, tileB);

            bool matched = board.FindMatches().Count > 0;

            if (!involvesItem && !matched)
            {
                // 아이템도 아니고 매치도 만들어지지 않으면 원래대로 되돌린다.
                yield return StartCoroutine(AnimateSwapVisual(tileA, tileB));
                board.Swap(a, b);
                SwapViews(tileA, tileB);
                inputLocked = false;
                yield break;
            }

            if (involvesItem)
            {
                // 아이템 블록은 매치 성립 여부와 상관없이, 옮겨지면(스왑되면) 바로 발동한다.
                var cellsToClear = new HashSet<Vector2Int>();
                if (tileA.Item != ItemType.None)
                    cellsToClear.UnionWith(board.ActivateItem(new Vector2Int(tileA.Col, tileA.Row)));
                if (tileB.Item != ItemType.None)
                    cellsToClear.UnionWith(board.ActivateItem(new Vector2Int(tileB.Col, tileB.Row)));

                yield return StartCoroutine(RunCascade(cellsToClear, chain: 1));
            }
            else
            {
                yield return StartCoroutine(ResolveMatches());
            }

            if (roundActive && !board.HasAnyValidMove())
            {
                board.Shuffle();
                RefreshAllViews();
            }

            UpdateHud();
            inputLocked = false;
        }

        private IEnumerator ActivateItemRoutine(TileView tile)
        {
            inputLocked = true;

            var cellsToClear = board.ActivateItem(new Vector2Int(tile.Col, tile.Row));
            yield return StartCoroutine(RunCascade(cellsToClear, chain: 1));

            if (roundActive && !board.HasAnyValidMove())
            {
                board.Shuffle();
                RefreshAllViews();
            }

            UpdateHud();
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

        /// <summary>스왑으로 만들어진 첫 매치를 모양별로 처리한다(아이템 생성 포함한 뒤 연쇄 진행).</summary>
        private IEnumerator ResolveMatches()
        {
            var groups = board.FindMatchGroups();
            var cellsToClear = ApplyMatchGroupsAndGetClearSet(groups);
            yield return StartCoroutine(RunCascade(cellsToClear, chain: 1));
        }

        /// <summary>
        /// 주어진 칸들을 지우고 중력/리필을 적용한 뒤, 그 결과로 새로 생기는 매치가 있으면
        /// 아이템 생성까지 포함해 더 이상 지울 칸이 없을 때까지 반복한다.
        /// </summary>
        private IEnumerator RunCascade(HashSet<Vector2Int> cellsToClear, int chain)
        {
            while (cellsToClear.Count > 0)
            {
                yield return StartCoroutine(AnimateClear(cellsToClear));

                board.Clear(cellsToClear);
                foreach (var pos in cellsToClear)
                {
                    var view = views[pos.x, pos.y];
                    if (view != null)
                    {
                        Destroy(view.gameObject);
                        views[pos.x, pos.y] = null;
                    }
                }

                score += cellsToClear.Count * 10 * chain;

                var moves = board.CollapseColumns();
                ApplyCollapseToViews(moves);
                yield return StartCoroutine(AnimateFall(moves.Select(m => views[m.to.x, m.to.y])));

                var spawns = board.RefillEmpties();
                SpawnNewTiles(spawns);
                yield return StartCoroutine(AnimateFall(spawns.Select(s => views[s.pos.x, s.pos.y])));

                UpdateHud();
                yield return new WaitForSeconds(0.05f);

                chain++;
                var groups = board.FindMatchGroups();
                cellsToClear = ApplyMatchGroupsAndGetClearSet(groups);
            }
        }

        /// <summary>
        /// 매치 그룹들을 순회하며, 아이템으로 바뀌어야 할 칸은 지우지 않고 아이템을 붙여 살려두고
        /// 나머지 칸들만 모아서 실제로 지울 집합으로 반환한다.
        /// </summary>
        private HashSet<Vector2Int> ApplyMatchGroupsAndGetClearSet(List<MatchGroup> groups)
        {
            var cellsToClear = new HashSet<Vector2Int>();

            foreach (var group in groups)
            {
                foreach (var cell in group.Cells)
                {
                    if (group.SpawnItem != ItemType.None && cell == group.SpawnCell)
                    {
                        board.SetItem(cell.x, cell.y, group.SpawnItem);
                        var view = views[cell.x, cell.y];
                        if (view != null)
                            view.SetItem(group.SpawnItem);
                        continue; // 이 칸은 지우지 않고 아이템 블록으로 남긴다.
                    }

                    cellsToClear.Add(cell);
                }
            }

            return cellsToClear;
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
                    view.SetItem(board.GetItem(col, row));
                }
            }
        }

        private static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);

        // ----------------------------------------------------------------
        // HUD
        // ----------------------------------------------------------------

        private void UpdateHud()
        {
            scoreText.text = $"점수: {score}";

            int secondsLeft = Mathf.CeilToInt(timeRemaining);
            if (secondsLeft == lastDisplayedSeconds)
                return;

            lastDisplayedSeconds = secondsLeft;
            timerText.text = $"남은 시간: {secondsLeft / 60}:{secondsLeft % 60:00}";
        }
    }
}
