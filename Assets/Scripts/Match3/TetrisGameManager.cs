using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// 일반 테트리스. 10x20 판에 7종류 블록이 무작위로 떨어지고, 화면 아래 버튼(좌/우/회전/
    /// 소프트드롭/하드드롭)으로 조작한다. 가로 한 줄을 꽉 채우면 그 줄이 사라지고 점수를 얻으며,
    /// 줄이 쌓여 새 블록이 나올 자리가 없으면(게임오버) 그 시점 점수로 라운드가 끝난다 - 그렇지
    /// 않으면 다른 게임들처럼 제한시간이 다 됐을 때 끝난다.
    ///
    /// 회전은 벽차기(wall kick) 없이 "그 자리에서 회전 가능한지"만 검사하는 단순한 방식이다.
    /// </summary>
    public class TetrisGameManager : MonoBehaviour, IRoundGame
    {
        [Header("판 설정")]
        public int columns = 10;
        public int rows = 20;
        public float cellSize = 56f;
        public float cellSpacing = 2f;

        [Header("라운드 설정")]
        public float roundDurationSeconds = 90f;

        [Header("난이도")]
        public float initialGravitySeconds = 0.8f;
        public float minGravitySeconds = 0.12f;
        public float gravityStepPerLevel = 0.06f;
        public int linesPerLevel = 10;

        [Header("버튼 길게 누르기")]
        public float repeatFirstDelay = 0.25f;
        public float repeatInterval = 0.08f;

        // 회전 상태 계산은 "NxN 박스 안에서 (x,y) -> (N-1-y, x)" 90도 시계방향 회전
        // 공식을 반복 적용해서 구한다 - 조각마다 4가지 회전을 따로 표로 만들 필요가 없다.
        private static readonly Vector2Int[][] PieceCellsBase =
        {
            new[] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(3, 1) }, // I
            new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) }, // O
            new[] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(1, 0) }, // T
            new[] { new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) }, // S
            new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(2, 1) }, // Z
            new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1) }, // J
            new[] { new Vector2Int(2, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1) }, // L
        };
        private static readonly int[] PieceBoxSize = { 4, 2, 3, 3, 3, 3, 3 };

        // PieceCellsBase와 같은 순서(I,O,T,S,Z,J,L). 실제 파일은
        // Assets/Resources/Sprites/Tetris/block_<이름>.png.
        private static readonly string[] PieceArtNames = { "i", "o", "t", "s", "z", "j", "l" };
        private static readonly Sprite[] pieceSpriteCache = new Sprite[PieceArtNames.Length];

        private static Sprite PieceSprite(int type)
        {
            if (pieceSpriteCache[type] == null)
                pieceSpriteCache[type] = Resources.Load<Sprite>("Sprites/Tetris/block_" + PieceArtNames[type]);
            return pieceSpriteCache[type];
        }

        private static readonly Color EmptyCellColor = new Color(1f, 1f, 1f, 0.06f);
        private static readonly int[] LineClearScoreTable = { 0, 100, 300, 500, 800 };

        public event Action<int> RoundEnded;
        public int CurrentScore => score;

        private const float TopBarHeight = 220f;
        private const float NextPanelHeight = 170f;
        private const float ControlBarHeight = 220f;

        private GameObject canvasRoot;
        private RectTransform boardRoot;
        private Image[,] cellImages;
        private Image[,] nextPreviewCells;
        private Text scoreText;
        private Text timerText;

        private int[,] board; // 0 = 빈칸, 1..7 = 잠긴 블록의 (pieceType+1)
        private int score;
        private bool roundActive;
        private float timeRemaining;
        private int lastDisplayedSeconds;
        private System.Random rng;

        private int currentType;
        private int currentRotation;
        private int pivotCol;
        private int pivotRow;
        private int nextType;

        private int totalLinesCleared;
        private int level;
        private float gravityInterval;
        private float gravityTimer;

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
        }

        public void SetVisible(bool visible) => canvasRoot.SetActive(visible);

        public void BeginRound(int? seed = null)
        {
            StopAllCoroutines();

            score = 0;
            roundActive = true;
            timeRemaining = roundDurationSeconds;
            lastDisplayedSeconds = -1;
            rng = new System.Random(seed ?? Environment.TickCount);

            board = new int[rows, columns];
            totalLinesCleared = 0;
            level = 0;
            gravityInterval = initialGravitySeconds;
            gravityTimer = 0f;

            nextType = rng.Next(PieceCellsBase.Length);
            SpawnPiece();

            UpdateHud();
            RedrawBoard();
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
                EndRound();
                return;
            }

            gravityTimer += Time.deltaTime;
            if (gravityTimer >= gravityInterval)
            {
                gravityTimer = 0f;
                if (!TryMove(0, 1))
                    LockPiece();
            }

            UpdateHud();
        }

        private void EndRound()
        {
            roundActive = false;
            StopAllCoroutines();
            UpdateHud();
            RoundEnded?.Invoke(score);
        }

        // ----------------------------------------------------------------
        // 조각 생성/이동/회전
        // ----------------------------------------------------------------

        private void SpawnPiece()
        {
            currentType = nextType;
            nextType = rng.Next(PieceCellsBase.Length);
            currentRotation = 0;

            int boxSize = PieceBoxSize[currentType];
            int minY = int.MaxValue;
            foreach (var c in PieceCellsBase[currentType])
                minY = Mathf.Min(minY, c.y);

            pivotCol = (columns - boxSize) / 2;
            pivotRow = -minY;

            RedrawNextPreview();

            if (!CanPlace(currentType, currentRotation, pivotCol, pivotRow))
            {
                // 새 조각이 나올 자리가 없다 - 게임 오버.
                EndRound();
            }
        }

        private static Vector2Int RotateCell(Vector2Int c, int boxSize) => new Vector2Int(boxSize - 1 - c.y, c.x);

        private Vector2Int[] GetCells(int type, int rotation)
        {
            int boxSize = PieceBoxSize[type];
            var cells = new Vector2Int[4];
            var baseCells = PieceCellsBase[type];
            for (int i = 0; i < 4; i++)
                cells[i] = baseCells[i];

            int steps = ((rotation % 4) + 4) % 4;
            for (int s = 0; s < steps; s++)
            {
                for (int i = 0; i < 4; i++)
                    cells[i] = RotateCell(cells[i], boxSize);
            }
            return cells;
        }

        private bool CanPlace(int type, int rotation, int atPivotCol, int atPivotRow)
        {
            foreach (var cell in GetCells(type, rotation))
            {
                int br = atPivotRow + cell.y;
                int bc = atPivotCol + cell.x;
                if (bc < 0 || bc >= columns || br < 0 || br >= rows)
                    return false;
                if (board[br, bc] != 0)
                    return false;
            }
            return true;
        }

        private bool TryMove(int dCol, int dRow)
        {
            if (!roundActive)
                return false;

            int newCol = pivotCol + dCol;
            int newRow = pivotRow + dRow;
            if (!CanPlace(currentType, currentRotation, newCol, newRow))
                return false;

            pivotCol = newCol;
            pivotRow = newRow;
            RedrawBoard();
            return true;
        }

        private void TryRotate()
        {
            if (!roundActive)
                return;

            int newRotation = currentRotation + 1;
            if (!CanPlace(currentType, newRotation, pivotCol, pivotRow))
                return;

            currentRotation = newRotation;
            RedrawBoard();
        }

        private void SoftDrop()
        {
            if (!roundActive)
                return;

            if (TryMove(0, 1))
            {
                score++;
                gravityTimer = 0f;
                UpdateHud();
            }
            else
            {
                LockPiece();
            }
        }

        private void HardDrop()
        {
            if (!roundActive)
                return;

            int cellsDropped = 0;
            while (CanPlace(currentType, currentRotation, pivotCol, pivotRow + 1))
            {
                pivotRow++;
                cellsDropped++;
            }
            score += cellsDropped * 2;
            gravityTimer = 0f;
            LockPiece();
        }

        private void LockPiece()
        {
            if (!roundActive)
                return;

            foreach (var cell in GetCells(currentType, currentRotation))
            {
                int br = pivotRow + cell.y;
                int bc = pivotCol + cell.x;
                if (br >= 0 && br < rows && bc >= 0 && bc < columns)
                    board[br, bc] = currentType + 1;
            }

            ClearCompletedLines();
            if (roundActive)
                SpawnPiece();

            UpdateHud();
            RedrawBoard();
        }

        private void ClearCompletedLines()
        {
            var fullRows = new List<int>();
            for (int r = 0; r < rows; r++)
            {
                bool full = true;
                for (int c = 0; c < columns; c++)
                {
                    if (board[r, c] == 0)
                    {
                        full = false;
                        break;
                    }
                }
                if (full)
                    fullRows.Add(r);
            }

            if (fullRows.Count == 0)
                return;

            foreach (int r in fullRows)
            {
                for (int rr = r; rr > 0; rr--)
                {
                    for (int c = 0; c < columns; c++)
                        board[rr, c] = board[rr - 1, c];
                }
                for (int c = 0; c < columns; c++)
                    board[0, c] = 0;
            }

            int cleared = Mathf.Min(fullRows.Count, LineClearScoreTable.Length - 1);
            score += LineClearScoreTable[cleared] * (level + 1);

            totalLinesCleared += fullRows.Count;
            level = totalLinesCleared / linesPerLevel;
            gravityInterval = Mathf.Max(minGravitySeconds, initialGravitySeconds - level * gravityStepPerLevel);
        }

        // ----------------------------------------------------------------
        // UI 생성
        // ----------------------------------------------------------------

        private void BuildUI()
        {
            canvasRoot = UIFactory.CreateGameCanvasRoot(transform, "TetrisCanvas");
            UIFactory.CreateGameTopBar(canvasRoot.transform, TopBarHeight, out scoreText, out timerText);
            BuildNextPreview(canvasRoot.transform);
            BuildBoardRoot(canvasRoot.transform);
            BuildControlBar(canvasRoot.transform);
        }

        private void BuildNextPreview(Transform parent)
        {
            var bar = UIFactory.CreateRect("NextBar", parent);
            bar.anchorMin = new Vector2(0, 1);
            bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(0.5f, 1);
            bar.sizeDelta = new Vector2(0, NextPanelHeight);
            bar.anchoredPosition = new Vector2(0, -TopBarHeight);

            var label = UIFactory.CreateText("NextLabel", bar, "다음 블록", 44, TextAnchor.MiddleLeft);
            var labelRt = label.rectTransform;
            labelRt.anchorMin = new Vector2(0, 0);
            labelRt.anchorMax = new Vector2(0.5f, 1);
            labelRt.offsetMin = new Vector2(40, 0);
            labelRt.offsetMax = Vector2.zero;

            var previewRoot = UIFactory.CreateRect("NextPreview", bar);
            previewRoot.anchorMin = previewRoot.anchorMax = new Vector2(0.75f, 0.5f);
            previewRoot.pivot = new Vector2(0.5f, 0.5f);
            float miniCell = 32f;
            previewRoot.sizeDelta = new Vector2(miniCell * 4, miniCell * 4);

            nextPreviewCells = new Image[4, 4];
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    var img = UIFactory.CreateImage($"Mini_{x}_{y}", previewRoot, EmptyCellColor);
                    var rt = img.rectTransform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(miniCell - 2, miniCell - 2);
                    rt.anchoredPosition = new Vector2(
                        -miniCell * 4 / 2f + miniCell / 2f + x * miniCell,
                        miniCell * 4 / 2f - miniCell / 2f - y * miniCell);
                    nextPreviewCells[y, x] = img;
                }
            }
        }

        private void BuildBoardRoot(Transform parent)
        {
            boardRoot = UIFactory.CreateRect("BoardRoot", parent);
            boardRoot.anchorMin = boardRoot.anchorMax = new Vector2(0.5f, 0.5f);
            boardRoot.pivot = new Vector2(0.5f, 0.5f);

            float boardW = columns * cellSize + (columns - 1) * cellSpacing;
            float boardH = rows * cellSize + (rows - 1) * cellSpacing;
            boardRoot.sizeDelta = new Vector2(boardW, boardH);
            boardRoot.anchoredPosition = new Vector2(0, (ControlBarHeight - TopBarHeight - NextPanelHeight) / 2f);

            var boardBg = UIFactory.CreateImage("BoardBackground", boardRoot, new Color(0, 0, 0, 0.25f));
            UIFactory.StretchFull(boardBg.rectTransform);

            Canvas.ForceUpdateCanvases();
            var canvasRect = (RectTransform)parent;
            float margin = 40f;
            float safeWidth = canvasRect.rect.width - margin * 2f;
            float safeHeight = canvasRect.rect.height - TopBarHeight - NextPanelHeight - ControlBarHeight - margin;
            float scale = Mathf.Min(1f, safeWidth / boardW, safeHeight / boardH);
            boardRoot.localScale = Vector3.one * scale;

            cellImages = new Image[rows, columns];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    var img = UIFactory.CreateImage($"Cell_{r}_{c}", boardRoot, EmptyCellColor);
                    var rt = img.rectTransform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(cellSize, cellSize);
                    rt.anchoredPosition = CellToLocalPos(r, c, boardW, boardH);
                    cellImages[r, c] = img;
                }
            }
        }

        private Vector2 CellToLocalPos(int row, int col, float boardW, float boardH)
        {
            float x = -boardW / 2f + cellSize / 2f + col * (cellSize + cellSpacing);
            float y = boardH / 2f - cellSize / 2f - row * (cellSize + cellSpacing);
            return new Vector2(x, y);
        }

        private void BuildControlBar(Transform parent)
        {
            var bar = UIFactory.CreateRect("ControlBar", parent);
            bar.anchorMin = new Vector2(0, 0);
            bar.anchorMax = new Vector2(1, 0);
            bar.pivot = new Vector2(0.5f, 0);
            bar.sizeDelta = new Vector2(0, ControlBarHeight);
            bar.anchoredPosition = Vector2.zero;

            float btnW = 170f;
            float btnH = 150f;
            float spacing = 20f;
            string[] labels = { "◀", "▶", "회전", "▼", "드롭" };
            float totalW = labels.Length * btnW + (labels.Length - 1) * spacing;

            for (int i = 0; i < labels.Length; i++)
            {
                float x = -totalW / 2f + btnW / 2f + i * (btnW + spacing);
                var button = UIFactory.CreateButton($"CtrlBtn_{i}", bar, labels[i], new Vector2(x, 0), new Vector2(btnW, btnH));

                switch (i)
                {
                    case 0:
                        AddRepeatingTrigger(button.gameObject, () => TryMove(-1, 0));
                        break;
                    case 1:
                        AddRepeatingTrigger(button.gameObject, () => TryMove(1, 0));
                        break;
                    case 2:
                        button.onClick.AddListener(TryRotate);
                        break;
                    case 3:
                        AddRepeatingTrigger(button.gameObject, SoftDrop);
                        break;
                    case 4:
                        button.onClick.AddListener(HardDrop);
                        break;
                }
            }
        }

        /// <summary>버튼을 누르고 있는 동안 action을 반복 호출한다 (좌/우 이동, 소프트드롭용).</summary>
        private void AddRepeatingTrigger(GameObject buttonGo, Action action)
        {
            var trigger = buttonGo.AddComponent<EventTrigger>();
            Coroutine repeatCoroutine = null;

            void StartRepeat()
            {
                action();
                repeatCoroutine = StartCoroutine(RepeatAction(action));
            }

            void StopRepeat()
            {
                if (repeatCoroutine != null)
                {
                    StopCoroutine(repeatCoroutine);
                    repeatCoroutine = null;
                }
            }

            AddTriggerEntry(trigger, EventTriggerType.PointerDown, _ => StartRepeat());
            AddTriggerEntry(trigger, EventTriggerType.PointerUp, _ => StopRepeat());
            AddTriggerEntry(trigger, EventTriggerType.PointerExit, _ => StopRepeat());
        }

        private static void AddTriggerEntry(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
        }

        private IEnumerator RepeatAction(Action action)
        {
            yield return new WaitForSeconds(repeatFirstDelay);
            while (true)
            {
                action();
                yield return new WaitForSeconds(repeatInterval);
            }
        }

        // ----------------------------------------------------------------
        // 렌더링/HUD
        // ----------------------------------------------------------------

        private void RedrawBoard()
        {
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    int val = board[r, c];
                    var img = cellImages[r, c];
                    img.sprite = val == 0 ? null : PieceSprite(val - 1);
                    img.color = val == 0 ? EmptyCellColor : Color.white;
                }
            }

            if (!roundActive)
                return;

            var sprite = PieceSprite(currentType);
            foreach (var cell in GetCells(currentType, currentRotation))
            {
                int br = pivotRow + cell.y;
                int bc = pivotCol + cell.x;
                if (br >= 0 && br < rows && bc >= 0 && bc < columns)
                {
                    cellImages[br, bc].sprite = sprite;
                    cellImages[br, bc].color = Color.white;
                }
            }
        }

        private void RedrawNextPreview()
        {
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    nextPreviewCells[y, x].sprite = null;
                    nextPreviewCells[y, x].color = EmptyCellColor;
                }
            }

            var sprite = PieceSprite(nextType);
            foreach (var cell in PieceCellsBase[nextType])
            {
                nextPreviewCells[cell.y, cell.x].sprite = sprite;
                nextPreviewCells[cell.y, cell.x].color = Color.white;
            }
        }

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
