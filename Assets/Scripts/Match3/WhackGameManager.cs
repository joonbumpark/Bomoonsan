using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// 복주머니 잡기(두더지잡기 스타일). 격자의 빈 구멍 중 무작위로 하나가 잠깐
    /// 복주머니로 반짝이면, 사라지기 전에 탭해서 점수를 얻는다. 놓쳐도 감점은 없다.
    ///
    /// 시드로 "언제 어디서 몇 개가 뜨는지"(스폰 스케줄)를 결정하고, 그 스케줄은
    /// 플레이어의 탭 여부와 무관하게 그대로 진행된다 - 그래서 대전에서 양쪽이 완전히
    /// 동일한 스폰 스케줄을 받아, 순수하게 반응 속도/정확도로만 점수를 겨루게 된다.
    /// </summary>
    public class WhackGameManager : MonoBehaviour, IRoundGame
    {
        [Header("판 설정")]
        [Min(2)] public int gridSize = 4;
        public float cellSize = 160f;
        public float cellSpacing = 16f;

        [Header("라운드 설정")]
        public float roundDurationSeconds = 45f;

        [Header("난이도 (시간이 지날수록 이 값들 사이로 서서히 어려워진다)")]
        public float initialSpawnInterval = 0.9f;
        public float minSpawnInterval = 0.35f;
        public float initialShowDuration = 1.1f;
        public float minShowDuration = 0.5f;
        public float difficultyRampSeconds = 30f;

        private static readonly Color[] MolePalette =
        {
            new Color(0.91f, 0.30f, 0.24f), // 빨강
            new Color(0.95f, 0.61f, 0.07f), // 주황
            new Color(0.95f, 0.87f, 0.20f), // 노랑
            new Color(0.30f, 0.69f, 0.31f), // 초록
            new Color(0.20f, 0.60f, 0.86f), // 파랑
        };
        private static readonly Color HoleColor = new Color(1f, 1f, 1f, 0.18f);

        public event Action<int> RoundEnded;
        public int CurrentScore => score;

        private const float TopBarHeight = 220f;
        private const float HintBarHeight = 100f;

        private GameObject canvasRoot;
        private RectTransform boardRoot;
        private Image[,] holes;
        private bool[,] active;
        private Text scoreText;
        private Text timerText;

        private int score;
        private bool roundActive;
        private float timeRemaining;
        private float elapsed;
        private int lastDisplayedSeconds;
        private System.Random rng;

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
            elapsed = 0f;
            timeRemaining = roundDurationSeconds;
            lastDisplayedSeconds = -1;
            rng = new System.Random(seed ?? Environment.TickCount);

            active = new bool[gridSize, gridSize];
            foreach (var hole in holes)
                SetHoleVisual(hole, false, Color.white);

            UpdateHud();
            SetVisible(true);
            StartCoroutine(SpawnLoop());
        }

        private void Update()
        {
            if (!roundActive)
                return;

            elapsed += Time.deltaTime;
            timeRemaining -= Time.deltaTime;
            if (timeRemaining <= 0f)
            {
                timeRemaining = 0f;
                roundActive = false;
                StopAllCoroutines();
                UpdateHud();
                RoundEnded?.Invoke(score);
                return;
            }

            UpdateHud();
        }

        // ----------------------------------------------------------------
        // 스폰 로직
        // ----------------------------------------------------------------

        private IEnumerator SpawnLoop()
        {
            while (roundActive)
            {
                float t = Mathf.Clamp01(elapsed / difficultyRampSeconds);
                float interval = Mathf.Lerp(initialSpawnInterval, minSpawnInterval, t);
                float showDuration = Mathf.Lerp(initialShowDuration, minShowDuration, t);

                yield return new WaitForSeconds(interval);
                if (!roundActive)
                    yield break;

                var cell = PickEmptyHole();
                if (cell.HasValue)
                    StartCoroutine(MoleRoutine(cell.Value.x, cell.Value.y, showDuration));
            }
        }

        private Vector2Int? PickEmptyHole()
        {
            var candidates = new List<Vector2Int>();
            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    if (!active[x, y])
                        candidates.Add(new Vector2Int(x, y));
                }
            }

            if (candidates.Count == 0)
                return null;
            return candidates[rng.Next(candidates.Count)];
        }

        private IEnumerator MoleRoutine(int x, int y, float duration)
        {
            active[x, y] = true;
            var color = MolePalette[rng.Next(MolePalette.Length)];
            SetHoleVisual(holes[x, y], true, color);

            float t = 0f;
            while (t < duration && active[x, y])
            {
                t += Time.deltaTime;
                yield return null;
            }

            if (active[x, y])
            {
                active[x, y] = false;
                SetHoleVisual(holes[x, y], false, Color.white);
            }
        }

        private void OnHoleTapped(int x, int y)
        {
            if (!roundActive || !active[x, y])
                return;

            active[x, y] = false;
            SetHoleVisual(holes[x, y], false, Color.white);
            score++;
            UpdateHud();
        }

        private void SetHoleVisual(Image hole, bool isActive, Color moleColor)
        {
            hole.color = isActive ? moleColor : HoleColor;
        }

        // ----------------------------------------------------------------
        // UI 생성
        // ----------------------------------------------------------------

        private void BuildUI()
        {
            canvasRoot = UIFactory.CreateGameCanvasRoot(transform, "WhackCanvas");
            UIFactory.CreateGameTopBar(canvasRoot.transform, TopBarHeight, out scoreText, out timerText);
            BuildBoardRoot(canvasRoot.transform);
            UIFactory.CreateGameHint(canvasRoot.transform, HintBarHeight, "반짝이는 복주머니를 재빨리 탭하세요!");
        }

        private void BuildBoardRoot(Transform parent)
        {
            boardRoot = UIFactory.CreateRect("BoardRoot", parent);
            boardRoot.anchorMin = boardRoot.anchorMax = new Vector2(0.5f, 0.5f);
            boardRoot.pivot = new Vector2(0.5f, 0.5f);

            float boardW = gridSize * cellSize + (gridSize - 1) * cellSpacing;
            float boardH = boardW;
            boardRoot.sizeDelta = new Vector2(boardW, boardH);
            boardRoot.anchoredPosition = new Vector2(0, (HintBarHeight - TopBarHeight) / 2f);

            var boardBg = UIFactory.CreateImage("BoardBackground", boardRoot, new Color(0, 0, 0, 0.25f));
            UIFactory.StretchFull(boardBg.rectTransform);

            // Match3GameManager와 같은 방식: 화면 비율이 기준 해상도와 많이 다르면
            // 판 전체를 남는 공간에 맞춰 축소한다.
            Canvas.ForceUpdateCanvases();
            var canvasRect = (RectTransform)parent;
            float margin = 40f;
            float safeWidth = canvasRect.rect.width - margin * 2f;
            float safeHeight = canvasRect.rect.height - TopBarHeight - HintBarHeight - margin;
            float scale = Mathf.Min(1f, safeWidth / boardW, safeHeight / boardH);
            boardRoot.localScale = Vector3.one * scale;

            holes = new Image[gridSize, gridSize];
            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    var go = new GameObject($"Hole_{x}_{y}", typeof(RectTransform));
                    go.transform.SetParent(boardRoot, false);

                    var rt = (RectTransform)go.transform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(cellSize, cellSize);
                    rt.anchoredPosition = CellToLocalPos(x, y, boardW, boardH);

                    var img = go.AddComponent<Image>();
                    img.sprite = TileArt.Base;
                    img.color = HoleColor;
                    img.preserveAspect = true;

                    var button = go.AddComponent<Button>();
                    button.transition = Selectable.Transition.None; // 색은 직접 관리하므로 기본 트윈은 끈다.
                    int cx = x, cy = y;
                    button.onClick.AddListener(() => OnHoleTapped(cx, cy));

                    holes[x, y] = img;
                }
            }
        }

        private Vector2 CellToLocalPos(int col, int row, float boardW, float boardH)
        {
            float x = -boardW / 2f + cellSize / 2f + col * (cellSize + cellSpacing);
            float y = -boardH / 2f + cellSize / 2f + row * (cellSize + cellSpacing);
            return new Vector2(x, y);
        }

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
