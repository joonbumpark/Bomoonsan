using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// 16조각(4x4) 직소 퍼즐. 미리 준비된 그림 2장(캐릭터 사진)을 번갈아 16조각으로 잘라
    /// 뒤섞어 놓고, 두 조각을 순서대로 탭하면 서로 자리를 바꾼다. 원래 그림대로 맞추면
    /// 점수를 얻고(적게 움직일수록 보너스가 큼) 곧바로 다음 그림으로 넘어간다 - 제한시간
    /// 동안 몇 판이나 맞추는지가 점수가 된다.
    ///
    /// 그림 2장은 고정이라(시드로 뒤섞는 순서만 무작위) 라운드를 시작하면 항상 첫 번째
    /// 그림부터 번갈아 나온다 - 대전에서도 같은 시드면 두 플레이어가 같은 순서로 같은
    /// 그림/뒤섞임을 받는다.
    /// </summary>
    public class JigsawGameManager : MonoBehaviour, IRoundGame
    {
        [Header("판 설정")]
        public int gridSize = 4;
        public float cellSize = 180f;
        public float cellSpacing = 12f;

        [Header("라운드 설정")]
        public float roundDurationSeconds = 90f;

        [Header("연출")]
        public float solvedCelebrationSeconds = 0.9f;

        private static readonly Color NormalTint = Color.white;
        private static readonly Color SelectedTint = new Color(1f, 0.85f, 0.35f);

        public event Action<int> RoundEnded;
        public int CurrentScore => score;

        private const float TopBarHeight = 220f;
        private const float PreviewBarHeight = 200f;
        private const float HintBarHeight = 110f;

        [Header("씬 UI (Bomoonsan > Build Game HUDs In Scene 로 생성)")]
        [SerializeField] private GameObject canvasRoot;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private Image previewImage;

        private RectTransform boardRoot;
        private Image[] tileImages;

        // 번갈아 쓰는 두 사진. Resources.Load는 처음 쓸 때만 하고, 자른 조각 스프라이트도
        // 이미지당 한 번만 만들어 재사용한다(고정된 사진이라 매 판 다시 자를 필요가 없다).
        private const int ImageCount = 2;
        private static readonly string[] ImageResourceNames = { "Sprites/Jigsaw/character_1", "Sprites/Jigsaw/character_2" };
        private readonly Sprite[] fullSpriteCache = new Sprite[ImageCount];
        private readonly Sprite[][] pieceSpriteCache = new Sprite[ImageCount][];
        private int puzzleIndex = -1;

        private int pieceCount;
        private int[] displayedPieceId; // slotIndex -> 원래 조각 id (0..pieceCount-1). 전부 identity면 완성.
        private Sprite[] pieceSprites;

        private int selectedSlot = -1;
        private int moves;
        private int score;
        private bool roundActive;
        private bool inputLocked;
        private float timeRemaining;
        private int lastDisplayedSeconds;
        private System.Random rng;

        private void Awake()
        {
            pieceCount = gridSize * gridSize;

            if (!ValidateSceneRefs())
                return;

            // BuildBoardRoot의 화면 맞춤 계산(Canvas.ForceUpdateCanvases)이 실제 캔버스
            // 크기를 읽어야 하는데, 씬에 미리 만들어둔 캔버스는 꺼진 채로 저장돼 있어서
            // 꺼진 상태로는 크기가 0으로 잡혀 보드가 지나치게 작아진다 - 계산하는 동안만
            // 잠깐 켜둔다.
            canvasRoot.SetActive(true);
            BuildBoardRoot(canvasRoot.transform);
            SetVisible(false);
        }

        /// <summary>
        /// 인스펙터에서 연결이 빠진 씬 UI 필드가 있으면 NRE 대신 어떤 필드가 비었는지
        /// 한 번에 알려주고 멈춘다 - Bomoonsan > Build Game HUDs In Scene을 다시
        /// 돌리거나 수동으로 연결하면 된다.
        /// </summary>
        private bool ValidateSceneRefs()
        {
            var missing = new List<string>();
            void Check(UnityEngine.Object obj, string fieldName)
            {
                if (obj == null)
                    missing.Add(fieldName);
            }

            Check(canvasRoot, nameof(canvasRoot));
            Check(scoreText, nameof(scoreText));
            Check(timerText, nameof(timerText));
            Check(hintText, nameof(hintText));
            Check(previewImage, nameof(previewImage));

            if (missing.Count == 0)
                return true;

            Debug.LogError(
                $"[JigsawGameManager] 씬 UI 참조가 비어 있음: {string.Join(", ", missing)}\n" +
                "Bomoonsan > Build Game HUDs In Scene 메뉴로 다시 생성하거나 인스펙터에서 직접 연결할 것.",
                this);
            return false;
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
            puzzleIndex = -1; // 라운드 시작마다 항상 첫 번째 그림부터 번갈아 나오게 한다.

            GenerateNewPuzzle();
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
                StopAllCoroutines();
                UpdateHud();
                RoundEnded?.Invoke(score);
                return;
            }

            UpdateHud();
        }

        // ----------------------------------------------------------------
        // 퍼즐 생성
        // ----------------------------------------------------------------

        private void GenerateNewPuzzle()
        {
            puzzleIndex = (puzzleIndex + 1) % ImageCount;
            EnsurePieceSpritesLoaded(puzzleIndex);

            pieceSprites = pieceSpriteCache[puzzleIndex];
            previewImage.sprite = fullSpriteCache[puzzleIndex];

            displayedPieceId = new int[pieceCount];
            for (int i = 0; i < pieceCount; i++)
                displayedPieceId[i] = i;
            ShuffleUntilScrambled(displayedPieceId, rng);

            selectedSlot = -1;
            moves = 0;
            inputLocked = false;

            RedrawAllTiles();
        }

        private static void ShuffleUntilScrambled(int[] array, System.Random rng)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }

            bool solved = true;
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] != i)
                {
                    solved = false;
                    break;
                }
            }
            if (solved)
                (array[0], array[1]) = (array[1], array[0]);
        }

        /// <summary>
        /// 이미지 인덱스별로 최초 1회만 원본 사진을 불러와 16조각으로 잘라 캐싱해둔다.
        /// 고정된 사진 2장을 번갈아 쓰는 것뿐이라 매 판 다시 자를 필요가 없다.
        /// </summary>
        private void EnsurePieceSpritesLoaded(int index)
        {
            if (pieceSpriteCache[index] != null)
                return;

            var texture = Resources.Load<Texture2D>(ImageResourceNames[index]);
            fullSpriteCache[index] = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));

            int cellPixelsX = texture.width / gridSize;
            int cellPixelsY = texture.height / gridSize;
            var sprites = new Sprite[pieceCount];
            for (int row = 0; row < gridSize; row++)
            {
                for (int col = 0; col < gridSize; col++)
                {
                    var rect = new Rect(col * cellPixelsX, texture.height - (row + 1) * cellPixelsY, cellPixelsX, cellPixelsY);
                    sprites[row * gridSize + col] = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f));
                }
            }
            pieceSpriteCache[index] = sprites;
        }

        // ----------------------------------------------------------------
        // 조각 탭 / 자리 바꾸기
        // ----------------------------------------------------------------

        private void OnTileTapped(int slotIndex)
        {
            if (!roundActive || inputLocked)
                return;

            if (selectedSlot == -1)
            {
                selectedSlot = slotIndex;
                RedrawTile(slotIndex);
                return;
            }

            if (selectedSlot == slotIndex)
            {
                selectedSlot = -1;
                RedrawTile(slotIndex);
                return;
            }

            (displayedPieceId[selectedSlot], displayedPieceId[slotIndex]) = (displayedPieceId[slotIndex], displayedPieceId[selectedSlot]);
            moves++;
            int previousSelected = selectedSlot;
            selectedSlot = -1;
            RedrawTile(previousSelected);
            RedrawTile(slotIndex);
            UpdateHud();

            if (IsSolved())
                StartCoroutine(SolvedThenNextPuzzle());
        }

        private bool IsSolved()
        {
            for (int i = 0; i < pieceCount; i++)
            {
                if (displayedPieceId[i] != i)
                    return false;
            }
            return true;
        }

        private IEnumerator SolvedThenNextPuzzle()
        {
            inputLocked = true;
            int bonus = Mathf.Max(100, 500 - moves * 15);
            score += bonus;
            hintText.text = $"완성! +{bonus}점";
            UpdateHud();

            yield return new WaitForSeconds(solvedCelebrationSeconds);

            if (roundActive)
                GenerateNewPuzzle();
        }

        // ----------------------------------------------------------------
        // 보드 루트 생성 (실제 게임판 - 그리드 크기가 인스펙터 설정에 따라 달라져서
        // 씬에 미리 박아둘 수 없다. 캔버스/점수바/완성본 미리보기 바/안내문구 같은
        // 나머지 UI는 전부 Bomoonsan > Build Game HUDs In Scene으로 씬에 미리
        // 만들어둔다.)
        // ----------------------------------------------------------------

        private void BuildBoardRoot(Transform parent)
        {
            boardRoot = UIFactory.CreateRect("BoardRoot", parent);
            boardRoot.anchorMin = boardRoot.anchorMax = new Vector2(0.5f, 0.5f);
            boardRoot.pivot = new Vector2(0.5f, 0.5f);

            float boardW = gridSize * cellSize + (gridSize - 1) * cellSpacing;
            float boardH = boardW;
            boardRoot.sizeDelta = new Vector2(boardW, boardH);
            boardRoot.anchoredPosition = new Vector2(0, (HintBarHeight - TopBarHeight - PreviewBarHeight) / 2f);

            var boardBg = UIFactory.CreateImage("BoardBackground", boardRoot, new Color(0, 0, 0, 0.8f));
            UIFactory.StretchFull(boardBg.rectTransform);

            Canvas.ForceUpdateCanvases();
            var canvasRect = (RectTransform)parent;
            float margin = 40f;
            float safeWidth = canvasRect.rect.width - margin * 2f;
            float safeHeight = canvasRect.rect.height - TopBarHeight - PreviewBarHeight - HintBarHeight - margin;
            float scale = Mathf.Min(1f, safeWidth / boardW, safeHeight / boardH);
            boardRoot.localScale = Vector3.one * scale;

            tileImages = new Image[pieceCount];
            for (int row = 0; row < gridSize; row++)
            {
                for (int col = 0; col < gridSize; col++)
                {
                    int slotIndex = row * gridSize + col;

                    var go = new GameObject($"Tile_{row}_{col}", typeof(RectTransform));
                    go.transform.SetParent(boardRoot, false);

                    var rt = (RectTransform)go.transform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(cellSize, cellSize);
                    rt.anchoredPosition = CellToLocalPos(row, col, boardW, boardH);

                    var img = go.AddComponent<Image>();
                    img.color = NormalTint;
                    img.preserveAspect = false;

                    var button = go.AddComponent<Button>();
                    button.transition = Selectable.Transition.None;
                    int capturedSlot = slotIndex;
                    button.onClick.AddListener(() => OnTileTapped(capturedSlot));

                    tileImages[slotIndex] = img;
                }
            }
        }

        private Vector2 CellToLocalPos(int row, int col, float boardW, float boardH)
        {
            float x = -boardW / 2f + cellSize / 2f + col * (cellSize + cellSpacing);
            float y = boardH / 2f - cellSize / 2f - row * (cellSize + cellSpacing);
            return new Vector2(x, y);
        }

        // ----------------------------------------------------------------
        // 렌더링/HUD
        // ----------------------------------------------------------------

        private void RedrawAllTiles()
        {
            for (int i = 0; i < pieceCount; i++)
                RedrawTile(i);
        }

        private void RedrawTile(int slotIndex)
        {
            var img = tileImages[slotIndex];
            img.sprite = pieceSprites[displayedPieceId[slotIndex]];
            img.color = slotIndex == selectedSlot ? SelectedTint : NormalTint;
        }

        private void UpdateHud()
        {
            scoreText.text = $"점수: {score}";
            if (roundActive && !inputLocked)
                hintText.text = $"조각을 두 번 탭해 자리를 바꾸세요 (이동 {moves}회)";

            int secondsLeft = Mathf.CeilToInt(timeRemaining);
            if (secondsLeft == lastDisplayedSeconds)
                return;

            lastDisplayedSeconds = secondsLeft;
            timerText.text = $"남은 시간: {secondsLeft / 60}:{secondsLeft % 60:00}";
        }
    }
}
