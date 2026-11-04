using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// 과일 썰기(후르츠닌자 스타일). 화면 아래에서 과일이 포물선으로 튀어 오르면 손가락으로
    /// 쓱 그어서 자른다. 과일 하나당 1점, 한 번 긋기에 3개 이상 자르면 그 개수만큼 콤보
    /// 보너스. 가끔 섞여 나오는 폭탄을 자르면 bombPenalty만큼 깎인다(0점 밑으로는 안 내려감).
    /// 드물게 나오는 복주머니는 자르면 pouchPoints점. 놓친 과일/복주머니는 감점이 없다.
    ///
    /// 복주머니 잡기처럼 시드로 "언제 어디서 무엇이 어떤 궤적으로 뜨는지"(스폰 스케줄)를
    /// 정하고, 플레이어 입력과 무관하게 그대로 진행된다. 과일은 따로 아트 없이 원형
    /// 스프라이트에 색을 입혀 그리고, 잘린 반쪽은 Filled 이미지로 원을 절반만 그려 만든다.
    /// 복주머니는 Resources/Sprites/FruitNinja/pouch 이미지를 쓰고, 없으면 매치3의 빨간
    /// 복주머니(TileArt)로 대신한다 - 반쪽도 같은 이미지를 절반만 채워 만든다.
    /// </summary>
    public class FruitNinjaGameManager : MonoBehaviour, IRoundGame
    {
        [Header("라운드 설정")]
        public float roundDurationSeconds = 60f;
        public int bombPenalty = 10;
        [Tooltip("한 번 긋기에 이 개수 이상 자르면 자른 개수만큼 보너스")]
        public int comboMinCount = 3;
        public int pouchPoints = 5;

        [Header("난이도 (시간이 지날수록 이 값들 사이로 서서히 어려워진다)")]
        public float initialSpawnInterval = 1.2f;
        public float minSpawnInterval = 0.55f;
        public int initialWaveSize = 1;
        public int maxWaveSize = 4;
        public float initialBombChance = 0.08f;
        public float maxBombChance = 0.2f;
        [Tooltip("폭탄이 아닌 칸 하나가 복주머니로 나올 확률")]
        public float pouchChance = 0.16f;
        [Tooltip("라운드 시작 후 이 시간 동안은 폭탄이 안 나온다")]
        public float bombFreeSeconds = 5f;
        public float difficultyRampSeconds = 40f;

        [Header("물리 (판 로컬 좌표 기준, 1080x1920 캔버스 단위)")]
        public float gravity = 2400f;
        [Tooltip("최고점 높이 범위 - 판 높이에 대한 비율")]
        public Vector2 apexHeightRange = new Vector2(0.55f, 0.88f);

        private struct FruitKind
        {
            public Color Skin;
            public Color Flesh;
            public float Radius;

            public FruitKind(Color skin, Color flesh, float radius)
            {
                Skin = skin;
                Flesh = flesh;
                Radius = radius;
            }
        }

        private static readonly FruitKind[] FruitKinds =
        {
            new FruitKind(new Color(0.22f, 0.58f, 0.24f), new Color(0.95f, 0.30f, 0.36f), 176f), // 수박
            new FruitKind(new Color(1.00f, 0.58f, 0.10f), new Color(1.00f, 0.78f, 0.35f), 132f), // 오렌지
            new FruitKind(new Color(0.86f, 0.16f, 0.18f), new Color(1.00f, 0.95f, 0.78f), 124f), // 사과
            new FruitKind(new Color(0.98f, 0.88f, 0.20f), new Color(1.00f, 0.97f, 0.62f), 116f), // 레몬
            new FruitKind(new Color(0.55f, 0.40f, 0.22f), new Color(0.56f, 0.80f, 0.26f), 112f), // 키위
            new FruitKind(new Color(0.56f, 0.26f, 0.66f), new Color(0.88f, 0.78f, 0.42f), 108f), // 자두
        };

        private enum FlyerType
        {
            Fruit,
            Bomb,
            Pouch,
        }

        private const float BombRadius = 124f;
        private const float PouchRadius = 160f;
        private const string PouchSpritePath = "Sprites/FruitNinja/pouch";
        private static readonly Color PouchTextColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color BombColor = new Color(0.16f, 0.16f, 0.20f);
        private static readonly Color SparkColor = new Color(1f, 0.55f, 0.15f);
        private static readonly Color TrailColor = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color ComboTextColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color PenaltyTextColor = new Color(1f, 0.35f, 0.3f);

        private const string CanvasPrefabPath = "Prefabs/FruitNinjaCanvas";
        private const float TrailWidth = 16f;
        private const float TrailLife = 0.18f;
        private const float HalfSplitSpeed = 260f;
        private const float BombFlashSeconds = 0.35f;

        public event Action<int> RoundEnded;
        public int CurrentScore => score;

        private class Flyer
        {
            public RectTransform Rt;
            public Vector2 Pos;
            public Vector2 Vel;
            public float Angle;
            public float Spin;
            public float Radius;
            public FlyerType Type;
            public int Kind; // Fruit일 때만 FruitKinds 인덱스
            public RectTransform Spark; // 폭탄 심지 불꽃(깜빡임용)
        }

        private class TrailSegment
        {
            public RectTransform Rt;
            public Image Image;
            public float Age;
        }

        private GameObject canvasRoot;
        private RectTransform boardRoot;
        private RectTransform pieceLayer;
        private RectTransform flyerLayer;
        private RectTransform trailLayer;
        private RectTransform fxLayer;
        private Image flashImage;
        private FruitSwipeArea swipeArea;
        private TextMeshProUGUI scoreText;
        private TextMeshProUGUI timerText;

        private readonly List<Flyer> flyers = new List<Flyer>();
        private readonly List<Flyer> pieces = new List<Flyer>(); // 잘린 반쪽 - 판정 없이 떨어지기만 한다.
        private readonly List<TrailSegment> trail = new List<TrailSegment>();

        private int score;
        private bool roundActive;
        private float timeRemaining;
        private float elapsed;
        private float flashTimer;
        private int strokeSliceCount;
        private Vector2 strokeLastSlicePos;
        private int lastDisplayedSeconds;
        private System.Random rng;

        private static Sprite circleSprite;
        private static Sprite CircleSprite => circleSprite != null ? circleSprite : (circleSprite = CreateCircleSprite());

        private static Sprite pouchSprite;
        private static Sprite PouchSprite
        {
            get
            {
                if (pouchSprite == null)
                    pouchSprite = Resources.Load<Sprite>(PouchSpritePath);
                if (pouchSprite == null)
                    pouchSprite = TileArt.NormalFor(0);
                return pouchSprite;
            }
        }

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            canvasRoot.SetActive(visible);
            if (!visible)
                swipeArea.ResetSwipe();
        }

        public void BeginRound(int? seed = null)
        {
            StopAllCoroutines();
            ClearBoard();

            score = 0;
            roundActive = true;
            elapsed = 0f;
            flashTimer = 0f;
            strokeSliceCount = 0;
            timeRemaining = roundDurationSeconds;
            lastDisplayedSeconds = -1;
            rng = new System.Random(seed ?? Environment.TickCount);

            UpdateHud();
            SetVisible(true);
            StartCoroutine(SpawnLoop());
        }

        /// <summary>진행 중인 라운드를 결과 없이 멈춘다(RoundEnded 안 보냄) - 설정 팝업의
        /// 다시하기/그만하기에서 AppFlowManager가 부른다.</summary>
        public void AbortRound()
        {
            StopAllCoroutines();
            roundActive = false;
            swipeArea.ResetSwipe();
        }

        private void Update()
        {
            if (!roundActive)
                return;

            float dt = Time.deltaTime;
            elapsed += dt;
            timeRemaining -= dt;
            if (timeRemaining <= 0f)
            {
                timeRemaining = 0f;
                roundActive = false;
                StopAllCoroutines();
                swipeArea.ResetSwipe();
                UpdateHud();
                RoundEnded?.Invoke(score);
                return;
            }

            StepFlyers(flyers, dt);
            StepFlyers(pieces, dt);
            StepTrail(dt);
            StepFlash(dt);
            UpdateHud();
        }

        // ----------------------------------------------------------------
        // 스폰
        // ----------------------------------------------------------------

        private IEnumerator SpawnLoop()
        {
            // 첫 과일은 화면이 뜨자마자가 아니라 잠깐 뒤에.
            yield return new WaitForSeconds(0.6f);

            while (roundActive)
            {
                float t = Mathf.Clamp01(elapsed / difficultyRampSeconds);
                float interval = Mathf.Lerp(initialSpawnInterval, minSpawnInterval, t);
                int maxWave = Mathf.RoundToInt(Mathf.Lerp(initialWaveSize, maxWaveSize, t));
                float bombChance = elapsed < bombFreeSeconds ? 0f : Mathf.Lerp(initialBombChance, maxBombChance, t);

                int waveSize = 1 + rng.Next(Mathf.Max(1, maxWave));
                bool bombPlaced = false;
                for (int i = 0; i < waveSize; i++)
                {
                    // 한 번에 폭탄은 최대 하나, 과일 없이 폭탄만 뜨는 일은 없게.
                    bool bomb = !bombPlaced && waveSize > 1 && i == waveSize - 1 && rng.NextDouble() < bombChance * waveSize;
                    bombPlaced |= bomb;
                    if (bomb)
                        Launch(FlyerType.Bomb);
                    else
                        Launch(rng.NextDouble() < pouchChance ? FlyerType.Pouch : FlyerType.Fruit);
                }

                yield return new WaitForSeconds(interval);
            }
        }

        private void Launch(FlyerType type)
        {
            var rect = boardRoot.rect;
            int kind = type == FlyerType.Fruit ? rng.Next(FruitKinds.Length) : -1;
            float radius = type switch
            {
                FlyerType.Bomb => BombRadius,
                FlyerType.Pouch => PouchRadius,
                _ => FruitKinds[kind].Radius,
            };

            float halfW = rect.width / 2f;
            // 큰 과일도 화면 밖으로 삐져나가지 않게 반지름만큼 안쪽에서 출발한다.
            float spawnHalfW = Mathf.Max(0f, Mathf.Min(halfW * 0.75f, halfW - radius));
            float startX = Mathf.Lerp(-spawnHalfW, spawnHalfW, (float)rng.NextDouble());
            float startY = rect.yMin - radius;

            // 최고점 높이를 먼저 정하고, 거기 도달하는 속도/시간으로 역산한다.
            float apex = rect.height * Mathf.Lerp(apexHeightRange.x, apexHeightRange.y, (float)rng.NextDouble());
            float vy = Mathf.Sqrt(2f * gravity * apex);
            float timeToApex = vy / gravity;
            float apexX = Mathf.Lerp(-halfW * 0.4f, halfW * 0.4f, (float)rng.NextDouble());
            float vx = (apexX - startX) / timeToApex;

            var flyer = new Flyer
            {
                Pos = new Vector2(startX, startY),
                Vel = new Vector2(vx, vy),
                Angle = (float)rng.NextDouble() * 360f,
                Spin = Mathf.Lerp(-180f, 180f, (float)rng.NextDouble()),
                Radius = radius,
                Type = type,
                Kind = kind,
            };
            flyer.Rt = type switch
            {
                FlyerType.Bomb => CreateBombVisual(flyer),
                FlyerType.Pouch => CreatePouchVisual(),
                _ => CreateFruitVisual(FruitKinds[kind]),
            };
            ApplyTransform(flyer);
            flyers.Add(flyer);
        }

        // ----------------------------------------------------------------
        // 움직임
        // ----------------------------------------------------------------

        private void StepFlyers(List<Flyer> list, float dt)
        {
            float killY = boardRoot.rect.yMin - 200f;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var f = list[i];
                f.Vel.y -= gravity * dt;
                f.Pos += f.Vel * dt;
                f.Angle += f.Spin * dt;
                ApplyTransform(f);

                if (f.Spark != null)
                    f.Spark.localScale = Vector3.one * (0.8f + 0.3f * Mathf.Sin(elapsed * 30f));

                if (f.Vel.y < 0f && f.Pos.y < killY)
                {
                    Destroy(f.Rt.gameObject);
                    list.RemoveAt(i);
                }
            }
        }

        private static void ApplyTransform(Flyer f)
        {
            f.Rt.anchoredPosition = f.Pos;
            f.Rt.localRotation = Quaternion.Euler(0, 0, f.Angle);
        }

        // ----------------------------------------------------------------
        // 자르기
        // ----------------------------------------------------------------

        private void OnSwiped(Vector2 from, Vector2 to)
        {
            if (!roundActive)
                return;

            AddTrailSegment(from, to);

            Vector2 dir = to - from;
            if (dir.sqrMagnitude < 4f)
                return;

            for (int i = flyers.Count - 1; i >= 0; i--)
            {
                var f = flyers[i];
                if (DistanceToSegment(f.Pos, from, to) > f.Radius)
                    continue;

                flyers.RemoveAt(i);
                switch (f.Type)
                {
                    case FlyerType.Bomb:
                        SliceBomb(f);
                        break;
                    case FlyerType.Pouch:
                        SlicePouch(f, dir);
                        break;
                    default:
                        SliceFruit(f, dir);
                        break;
                }
            }
        }

        private void OnSwipeEnded()
        {
            if (roundActive && strokeSliceCount >= comboMinCount)
            {
                int bonus = strokeSliceCount;
                score += bonus;
                UpdateHud();
                SoundManager.Play(Sfx.Match3Item);
                Match3EffectSpawner.SpawnPopupText(this, fxLayer, strokeLastSlicePos + Vector2.up * 80f,
                    $"{strokeSliceCount} 콤보! +{bonus}", ComboTextColor, 90f);
            }
            strokeSliceCount = 0;
        }

        private void SliceFruit(Flyer f, Vector2 swipeDir)
        {
            var kind = FruitKinds[f.Kind];
            score++;
            strokeSliceCount++;
            strokeLastSlicePos = f.Pos;
            UpdateHud();
            SoundManager.Play(Sfx.Match3Match);

            // 긋는 방향을 따라 둘로 갈라져 서로 반대쪽으로 벌어지며 떨어진다.
            float cutAngle = Mathf.Atan2(swipeDir.y, swipeDir.x) * Mathf.Rad2Deg;
            Vector2 normal = new Vector2(-swipeDir.y, swipeDir.x).normalized;
            SpawnHalf(f, cutAngle, Image.OriginVertical.Top, normal, rt => CreateFruitHalfVisual(rt, kind, Image.OriginVertical.Top));
            SpawnHalf(f, cutAngle, Image.OriginVertical.Bottom, -normal, rt => CreateFruitHalfVisual(rt, kind, Image.OriginVertical.Bottom));

            Match3EffectSpawner.SpawnPop(this, fxLayer, f.Pos, kind.Flesh);
            Destroy(f.Rt.gameObject);
        }

        private void SlicePouch(Flyer f, Vector2 swipeDir)
        {
            score += pouchPoints;
            strokeSliceCount++;
            strokeLastSlicePos = f.Pos;
            UpdateHud();
            SoundManager.Play(Sfx.Match3Item);

            float cutAngle = Mathf.Atan2(swipeDir.y, swipeDir.x) * Mathf.Rad2Deg;
            Vector2 normal = new Vector2(-swipeDir.y, swipeDir.x).normalized;
            SpawnHalf(f, cutAngle, Image.OriginVertical.Top, normal, rt => CreateHalfImage("Pouch", rt, PouchSprite, Color.white, 1f, Image.OriginVertical.Top));
            SpawnHalf(f, cutAngle, Image.OriginVertical.Bottom, -normal, rt => CreateHalfImage("Pouch", rt, PouchSprite, Color.white, 1f, Image.OriginVertical.Bottom));

            Match3EffectSpawner.SpawnCelebration(this, fxLayer, f.Pos);
            Match3EffectSpawner.SpawnPopupText(this, fxLayer, f.Pos, $"+{pouchPoints}", PouchTextColor, 110f);
            Destroy(f.Rt.gameObject);
        }

        private void SliceBomb(Flyer f)
        {
            score = Mathf.Max(0, score - bombPenalty);
            UpdateHud();
            SoundManager.Play(Sfx.QuizWrong);

            flashTimer = BombFlashSeconds;
            Match3EffectSpawner.SpawnAreaBomb(this, fxLayer, f.Pos, SparkColor);
            Match3EffectSpawner.SpawnPopupText(this, fxLayer, f.Pos, $"-{bombPenalty}", PenaltyTextColor, 110f);
            Destroy(f.Rt.gameObject);
        }

        private void SpawnHalf(Flyer source, float cutAngle, Image.OriginVertical keep, Vector2 push, Action<RectTransform> buildVisual)
        {
            var rt = UIFactory.CreateRect("Half", pieceLayer);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.one * source.Radius * 2f;
            buildVisual(rt);

            var piece = new Flyer
            {
                Rt = rt,
                Pos = source.Pos + push * (source.Radius * 0.1f),
                Vel = new Vector2(source.Vel.x * 0.5f, Mathf.Max(source.Vel.y * 0.3f, 0f)) + push * HalfSplitSpeed,
                Angle = cutAngle,
                Spin = (keep == Image.OriginVertical.Bottom ? 1f : -1f) * 140f,
                Radius = source.Radius,
            };
            ApplyTransform(piece);
            pieces.Add(piece);
        }

        /// <summary>껍질(바깥 원) 위에 과육(조금 작은 원)을 겹치고, 둘 다 같은 쪽 절반만 채운다.</summary>
        private static void CreateFruitHalfVisual(RectTransform parent, FruitKind kind, Image.OriginVertical keep)
        {
            CreateHalfImage("Skin", parent, CircleSprite, kind.Skin, 1f, keep);
            CreateHalfImage("Flesh", parent, CircleSprite, kind.Flesh, 0.86f, keep);
        }

        private static void CreateHalfImage(string name, RectTransform parent, Sprite sprite, Color color, float scale, Image.OriginVertical keep)
        {
            var image = UIFactory.CreateImage(name, parent, color);
            image.sprite = sprite;
            image.preserveAspect = true;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Vertical;
            image.fillOrigin = (int)keep;
            image.fillAmount = 0.5f;
            image.raycastTarget = false;

            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = parent.sizeDelta * scale;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        // ----------------------------------------------------------------
        // 칼자국 / 폭탄 플래시
        // ----------------------------------------------------------------

        private void AddTrailSegment(Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length < 1f)
                return;

            var image = UIFactory.CreateImage("Trail", trailLayer, TrailColor);
            image.raycastTarget = false;
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(length + TrailWidth * 0.5f, TrailWidth);
            rt.anchoredPosition = (from + to) / 2f;
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            trail.Add(new TrailSegment { Rt = rt, Image = image });
        }

        private void StepTrail(float dt)
        {
            for (int i = trail.Count - 1; i >= 0; i--)
            {
                var seg = trail[i];
                seg.Age += dt;
                float p = seg.Age / TrailLife;
                if (p >= 1f)
                {
                    Destroy(seg.Rt.gameObject);
                    trail.RemoveAt(i);
                    continue;
                }

                seg.Rt.sizeDelta = new Vector2(seg.Rt.sizeDelta.x, TrailWidth * (1f - p));
                seg.Image.color = new Color(TrailColor.r, TrailColor.g, TrailColor.b, TrailColor.a * (1f - p));
            }
        }

        private void StepFlash(float dt)
        {
            if (flashTimer <= 0f)
                return;

            flashTimer = Mathf.Max(0f, flashTimer - dt);
            flashImage.color = new Color(1f, 0.25f, 0.2f, 0.45f * (flashTimer / BombFlashSeconds));
        }

        private void ClearBoard()
        {
            foreach (var f in flyers)
                Destroy(f.Rt.gameObject);
            foreach (var f in pieces)
                Destroy(f.Rt.gameObject);
            foreach (var seg in trail)
                Destroy(seg.Rt.gameObject);
            flyers.Clear();
            pieces.Clear();
            trail.Clear();

            // 라운드가 끝날 때 멈춘 채 남은 점수 글자/파편도 치운다.
            foreach (Transform child in fxLayer)
                Destroy(child.gameObject);

            flashImage.color = Color.clear;
            swipeArea.ResetSwipe();
        }

        // ----------------------------------------------------------------
        // UI 생성
        // ----------------------------------------------------------------

        private void BuildUI()
        {
            // 배경/점수바(TopBar)/안내바는 매치3와 같은 모양을 쓰려고 Match3Canvas를 복제한
            // 프리팹을 띄우고, 과일이 날아다니는 판만 코드로 배경 바로 위에 끼워 넣는다.
            canvasRoot = Instantiate(Resources.Load<GameObject>(CanvasPrefabPath), transform, false);
            canvasRoot.name = "FruitNinjaCanvas";
            foreach (var scaler in canvasRoot.GetComponentsInChildren<CanvasScaler>(true))
                Mountains.ResponsiveCanvas.Apply(scaler);

            var root = canvasRoot.transform;
            scoreText = root.Find("TopBar/ScoreText").GetComponent<TextMeshProUGUI>();
            timerText = root.Find("TopBar/TimerText").GetComponent<TextMeshProUGUI>();

            // 점수바/안내바 위로 그어도 칼질이 끊기지 않게 입력은 전부 판으로 흘려보낸다.
            foreach (var graphic in root.Find("TopBar").GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            foreach (var graphic in root.Find("HintBg").GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;

            boardRoot = UIFactory.CreateRect("BoardRoot", root);
            UIFactory.StretchFull(boardRoot);
            boardRoot.SetSiblingIndex(root.Find("Background").GetSiblingIndex() + 1);

            // 판 전체를 덮는 투명 입력 영역 - 과일/칼자국은 raycastTarget을 꺼서 입력을 가로채지 않는다.
            var swipeImage = UIFactory.CreateImage("SwipeArea", boardRoot, Color.clear);
            UIFactory.StretchFull(swipeImage.rectTransform);
            swipeArea = swipeImage.gameObject.AddComponent<FruitSwipeArea>();
            swipeArea.Init(boardRoot);
            swipeArea.Swiped += OnSwiped;
            swipeArea.SwipeEnded += OnSwipeEnded;

            pieceLayer = CreateLayer("Pieces");
            flyerLayer = CreateLayer("Flyers");
            trailLayer = CreateLayer("Trail");
            fxLayer = CreateLayer("Fx");

            flashImage = UIFactory.CreateImage("BombFlash", boardRoot, Color.clear);
            flashImage.raycastTarget = false;
            UIFactory.StretchFull(flashImage.rectTransform);
        }

        private RectTransform CreateLayer(string name)
        {
            var rt = UIFactory.CreateRect(name, boardRoot);
            UIFactory.StretchFull(rt);
            return rt;
        }

        private RectTransform CreateFruitVisual(FruitKind kind)
        {
            var body = CreateCircle("Fruit", flyerLayer, kind.Skin, kind.Radius * 2f);

            // 왼쪽 위 하이라이트 - 원이 납작한 동그라미로만 보이지 않게.
            var shine = CreateCircle("Shine", body, new Color(1f, 1f, 1f, 0.35f), kind.Radius * 0.55f);
            shine.anchoredPosition = new Vector2(-kind.Radius * 0.38f, kind.Radius * 0.38f);
            return body;
        }

        private RectTransform CreatePouchVisual()
        {
            var image = UIFactory.CreateImage("Pouch", flyerLayer, Color.white);
            image.sprite = PouchSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.one * PouchRadius * 2f;
            return rt;
        }

        private RectTransform CreateBombVisual(Flyer flyer)
        {
            var body = CreateCircle("Bomb", flyerLayer, BombColor, BombRadius * 2f);

            var ring = CreateCircle("Ring", body, new Color(0.85f, 0.15f, 0.15f), BombRadius * 1.1f);
            CreateCircle("Core", ring, BombColor, BombRadius * 0.75f);

            flyer.Spark = CreateCircle("Spark", body, SparkColor, BombRadius * 0.45f);
            flyer.Spark.anchoredPosition = new Vector2(0f, BombRadius * 1.05f);
            return body;
        }

        private static RectTransform CreateCircle(string name, Transform parent, Color color, float diameter)
        {
            var image = UIFactory.CreateImage(name, parent, color);
            image.sprite = CircleSprite;
            image.raycastTarget = false;

            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.one * diameter;
            return rt;
        }

        /// <summary>Match3EffectSpawner와 같은 방식의 원형 텍스처지만, 과일은 크게 그려지므로
        /// 해상도를 높여 가장자리가 뭉개지지 않게 한다.</summary>
        private static Sprite CreateCircleSprite()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 1f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    byte alpha = (byte)(Mathf.Clamp01(radius - dist) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        // ----------------------------------------------------------------
        // HUD
        // ----------------------------------------------------------------

        private void UpdateHud()
        {
            scoreText.text = score.ToString();

            int secondsLeft = Mathf.CeilToInt(timeRemaining);
            if (secondsLeft == lastDisplayedSeconds)
                return;

            lastDisplayedSeconds = secondsLeft;
            timerText.text = $"{secondsLeft / 60}:{secondsLeft % 60:00}";
        }
    }
}
