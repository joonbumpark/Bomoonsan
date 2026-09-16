using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// 16조각(4x4) 직소 퍼즐. 매 라운드마다 씨드로 알록달록한 그림을 하나 만들어 16조각으로
    /// 잘라 뒤섞어 놓고, 두 조각을 순서대로 탭하면 서로 자리를 바꾼다. 원래 그림대로
    /// 맞추면 점수를 얻고(적게 움직일수록 보너스가 큼) 곧바로 새 그림으로 다시 시작한다 -
    /// 제한시간 동안 몇 판이나 맞추는지가 점수가 된다.
    ///
    /// 그림은 기존 타일 아트를 쓰지 않고 코드로 그때그때 생성한다(방사형 그라데이션 +
    /// 무작위 색 원 몇 개) - 같은 시드면 두 플레이어가 똑같은 그림/뒤섞임을 받는다.
    /// </summary>
    public class JigsawGameManager : MonoBehaviour, IRoundGame
    {
        [Header("판 설정")]
        public int gridSize = 4;
        public float cellSize = 180f;
        public float cellSpacing = 12f;
        public int textureSize = 480;

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

        private GameObject canvasRoot;
        private RectTransform boardRoot;
        private Image[] tileImages;
        private Image previewImage;
        private Text scoreText;
        private Text timerText;
        private Text hintText;

        private int pieceCount;
        private int[] displayedPieceId; // slotIndex -> 원래 조각 id (0..pieceCount-1). 전부 identity면 완성.
        private Sprite[] pieceSprites;
        private Sprite fullSprite;
        private Texture2D artworkTexture;

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

        private void OnDestroy()
        {
            DestroyArtwork();
        }

        // ----------------------------------------------------------------
        // 퍼즐 생성
        // ----------------------------------------------------------------

        private void GenerateNewPuzzle()
        {
            DestroyArtwork();

            artworkTexture = GenerateArtworkTexture(textureSize, rng);
            fullSprite = Sprite.Create(artworkTexture, new Rect(0, 0, textureSize, textureSize), new Vector2(0.5f, 0.5f));
            previewImage.sprite = fullSprite;

            int cellPixels = textureSize / gridSize;
            pieceSprites = new Sprite[pieceCount];
            for (int row = 0; row < gridSize; row++)
            {
                for (int col = 0; col < gridSize; col++)
                {
                    var rect = new Rect(col * cellPixels, textureSize - (row + 1) * cellPixels, cellPixels, cellPixels);
                    pieceSprites[row * gridSize + col] = Sprite.Create(artworkTexture, rect, new Vector2(0.5f, 0.5f));
                }
            }

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
        /// 방사형 색상 그라데이션(스월) 위에 무작위 색 원 몇 개를 얹은 그림을 만든다.
        /// 사진 리소스 없이도 16조각 각각이 서로 다른 색/무늬를 갖게 해서 퍼즐로 쓸 만하게 한다.
        /// </summary>
        private static Texture2D GenerateArtworkTexture(int size, System.Random rng)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color32[size * size];
            float cx = size / 2f;
            float cy = size / 2f;
            float hueOffset = (float)rng.NextDouble();
            float swirl = 1.5f + (float)rng.NextDouble() * 2.5f;
            float maxRadius = size * 0.72f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float angle = Mathf.Atan2(dy, dx);
                    float radius = Mathf.Sqrt(dx * dx + dy * dy) / maxRadius;
                    float hue = Mathf.Repeat(hueOffset + angle / (Mathf.PI * 2f) + radius * swirl, 1f);
                    var color = Color.HSVToRGB(hue, 0.6f, 0.95f);
                    pixels[y * size + x] = color;
                }
            }

            int circleCount = 5 + rng.Next(4);
            for (int i = 0; i < circleCount; i++)
            {
                float px = (float)rng.NextDouble() * size;
                float py = (float)rng.NextDouble() * size;
                float radius = size * (0.07f + (float)rng.NextDouble() * 0.10f);
                var circleColor = Color.HSVToRGB((float)rng.NextDouble(), 0.75f, 1f);

                int minX = Mathf.Max(0, Mathf.FloorToInt(px - radius));
                int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(px + radius));
                int minY = Mathf.Max(0, Mathf.FloorToInt(py - radius));
                int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(py + radius));

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        float dist = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                        if (dist <= radius)
                            pixels[y * size + x] = circleColor;
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private void DestroyArtwork()
        {
            if (pieceSprites != null)
            {
                foreach (var sprite in pieceSprites)
                    if (sprite != null)
                        Destroy(sprite);
                pieceSprites = null;
            }

            if (fullSprite != null)
            {
                Destroy(fullSprite);
                fullSprite = null;
            }

            if (artworkTexture != null)
            {
                Destroy(artworkTexture);
                artworkTexture = null;
            }
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
        // UI 생성
        // ----------------------------------------------------------------

        private void BuildUI()
        {
            canvasRoot = UIFactory.CreateGameCanvasRoot(transform, "JigsawCanvas");
            UIFactory.CreateGameTopBar(canvasRoot.transform, TopBarHeight, out scoreText, out timerText);
            BuildPreviewBar(canvasRoot.transform);
            BuildBoardRoot(canvasRoot.transform);
            hintText = UIFactory.CreateGameHint(canvasRoot.transform, HintBarHeight, "조각을 두 번 탭해 자리를 바꾸세요");
        }

        private void BuildPreviewBar(Transform parent)
        {
            var bar = UIFactory.CreateRect("PreviewBar", parent);
            bar.anchorMin = new Vector2(0, 1);
            bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(0.5f, 1);
            bar.sizeDelta = new Vector2(0, PreviewBarHeight);
            bar.anchoredPosition = new Vector2(0, -TopBarHeight);

            var label = UIFactory.CreateText("PreviewLabel", bar, "완성 모습", 44, TextAnchor.MiddleLeft);
            var labelRt = label.rectTransform;
            labelRt.anchorMin = new Vector2(0, 0);
            labelRt.anchorMax = new Vector2(0.5f, 1);
            labelRt.offsetMin = new Vector2(40, 0);
            labelRt.offsetMax = Vector2.zero;

            float thumbSize = PreviewBarHeight - 40f;
            previewImage = UIFactory.CreateImage("PreviewThumb", bar, Color.white);
            var thumbRt = previewImage.rectTransform;
            thumbRt.anchorMin = thumbRt.anchorMax = new Vector2(0.72f, 0.5f);
            thumbRt.pivot = new Vector2(0.5f, 0.5f);
            thumbRt.sizeDelta = new Vector2(thumbSize, thumbSize);
            previewImage.preserveAspect = true;
        }

        private void BuildBoardRoot(Transform parent)
        {
            boardRoot = UIFactory.CreateRect("BoardRoot", parent);
            boardRoot.anchorMin = boardRoot.anchorMax = new Vector2(0.5f, 0.5f);
            boardRoot.pivot = new Vector2(0.5f, 0.5f);

            float boardW = gridSize * cellSize + (gridSize - 1) * cellSpacing;
            float boardH = boardW;
            boardRoot.sizeDelta = new Vector2(boardW, boardH);
            boardRoot.anchoredPosition = new Vector2(0, (HintBarHeight - TopBarHeight - PreviewBarHeight) / 2f);

            var boardBg = UIFactory.CreateImage("BoardBackground", boardRoot, new Color(0, 0, 0, 0.25f));
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
