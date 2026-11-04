using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Mountains
{
    // 화면을 정해진 방향(기본: 아래에서 위)으로 쓸어 넘겨야 다음 스텝으로 넘어가는 액션.
    // 손가락 그림이 트랙을 따라 반복해서 미끄러지며 방향을 보여주고, 화살표가 차례로 깜빡인다.
    //
    // 판정: 누른 지점에서 정해진 방향으로 requiredDistance(화면 크기 비율)만큼 이동하면 성공.
    // 방향에서 maxAngle 이상 벗어났거나 maxSwipeSeconds보다 오래 걸린 드래그는 세지 않는다.
    // 손가락을 떼기 전, 거리를 채우는 순간 바로 성공 처리해서 반응이 빠르게 느껴지게 한다.
    [Serializable]
    public class SwipeAction : InteractionActionBase
    {
        public enum SwipeDirection { Up, Down, Left, Right }

        [Header("스와이프")]
        public SwipeDirection direction = SwipeDirection.Up;
        [Tooltip("필요한 이동 거리. 위/아래면 화면 높이, 좌/우면 화면 너비에 대한 비율.")]
        [Range(0.05f, 0.9f)] public float requiredDistance = 0.25f;
        [Tooltip("정해진 방향에서 이 각도(도)보다 많이 기울어진 드래그는 인정하지 않는다.")]
        [Range(5f, 89f)] public float maxAngle = 35f;
        [Tooltip("누르고 이 시간(초) 안에 거리를 채워야 한다. 0이면 시간 제한 없음.")]
        [Min(0f)] public float maxSwipeSeconds = 1.2f;
        [Tooltip("몇 번 쓸어 넘겨야 끝나는지.")]
        [Min(1)] public int requiredCount = 1;
        [Tooltip("이 시간(초) 안에 성공하지 못해도 다음 스텝으로 넘어간다. 0이면 성공할 때까지 기다린다.")]
        [Min(0f)] public float timeLimitSeconds = 0f;

        [Header("표시")]
        [Tooltip("안내 트랙(손가락이 움직이는 길)의 길이. 위/아래면 화면 높이 비율.")]
        [Range(0.1f, 0.8f)] public float trackLength = 0.4f;
        [Tooltip("트랙 가운데의 위치(0~1 화면 비율).")]
        public Vector2 trackCenter = new Vector2(0.5f, 0.45f);
        public Color trackColor = new Color(1f, 1f, 1f, 0.18f);
        public Color progressColor = new Color(1f, 0.85f, 0.3f, 0.9f);

        [Header("소리")]
        [Tooltip("여러 번 넘겨야 할 때 한 번 넘길 때마다 나는 소리.")]
        public AudioClip swipeSfx;

        const float TrackThickness = 22f;
        const int ChevronCount = 3;

        Vector2 _start;   // 트랙 시작 앵커(화면 비율)
        Vector2 _end;     // 트랙 끝 앵커
        RectTransform _progress;
        Image _hand;
        Sequence _handLoop;
        Sequence _chevronLoop;
        TMPro.TextMeshProUGUI _counter;

        public SwipeAction()
        {
            message = "화면을 위로 쓸어 올리세요";
        }

        Vector2 DirectionVector
        {
            get
            {
                switch (direction)
                {
                    case SwipeDirection.Down: return Vector2.down;
                    case SwipeDirection.Left: return Vector2.left;
                    case SwipeDirection.Right: return Vector2.right;
                    default: return Vector2.up;
                }
            }
        }

        bool IsVertical => direction == SwipeDirection.Up || direction == SwipeDirection.Down;

        protected override void BuildContent(RectTransform root)
        {
            Vector2 dir = DirectionVector;
            Vector2 half = dir * (trackLength * 0.5f);
            _start = trackCenter - half;
            _end = trackCenter + half;

            // 트랙: 시작-끝을 잇는 흐린 막대. 앵커를 두 점으로 잡아 화면 비율이 달라도 같이 늘어난다.
            var track = CreatePill("Track", root, trackColor, TrackThickness);
            SpanBetween(track.rectTransform, _start, _end);

            // 진행 막대: 드래그한 만큼 트랙 위에 차오른다.
            var progress = CreatePill("Progress", root, progressColor, TrackThickness);
            _progress = progress.rectTransform;
            SetProgress(0f);

            BuildChevrons(root, dir);

            _hand = CreateHand(root, _start, "DRAG");
            _handLoop = BuildHandLoop();

            if (requiredCount > 1)
            {
                _counter = UiOverlayRoot.CreateText("Counter", root, fontSize * 0.8f,
                    TMPro.TextAlignmentOptions.Center, UiOverlayRoot.ResolveFont());
                var rect = _counter.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, messageHeight);
                rect.anchoredPosition = new Vector2(0f, -fontSize * 1.6f);
                rect.sizeDelta = new Vector2(600f, fontSize * 1.4f);
                UpdateCounter(0);
            }
        }

        // 두 앵커 점 사이를 잇는 막대로 배치한다(세로/가로 트랙 모두).
        void SpanBetween(RectTransform rect, Vector2 from, Vector2 to)
        {
            Vector2 min = Vector2.Min(from, to);
            Vector2 max = Vector2.Max(from, to);
            if (IsVertical)
            {
                rect.anchorMin = new Vector2(from.x, min.y);
                rect.anchorMax = new Vector2(from.x, max.y);
                rect.sizeDelta = new Vector2(TrackThickness, 0f);
            }
            else
            {
                rect.anchorMin = new Vector2(min.x, from.y);
                rect.anchorMax = new Vector2(max.x, from.y);
                rect.sizeDelta = new Vector2(0f, TrackThickness);
            }
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        void SetProgress(float t)
        {
            if (_progress == null)
            {
                return;
            }

            _progress.gameObject.SetActive(t > 0.001f);
            SpanBetween(_progress, _start, Vector2.Lerp(_start, _end, t));
        }

        // 트랙을 따라 놓인 화살표(꺾쇠) 3개가 시작 쪽부터 차례로 밝아진다.
        void BuildChevrons(RectTransform root, Vector2 dir)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f; // 꺾쇠의 기본 모양은 위를 가리킨다
            _chevronLoop = DOTween.Sequence().SetUpdate(true).SetLink(root.gameObject);

            for (int i = 0; i < ChevronCount; i++)
            {
                float t = (i + 1f) / (ChevronCount + 1f);
                var chevron = CreateAnchored($"Chevron{i}", root, Vector2.Lerp(_start, _end, t), new Vector2(90f, 60f));
                chevron.localRotation = Quaternion.Euler(0f, 0f, angle);
                var group = chevron.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0.25f;

                // 꺾쇠 = 서로 기울어진 막대 두 개.
                for (int side = -1; side <= 1; side += 2)
                {
                    var bar = UiOverlayRoot.CreateImage("Bar", chevron, Color.white);
                    bar.raycastTarget = false;
                    var barRect = bar.rectTransform;
                    barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 0.5f);
                    barRect.sizeDelta = new Vector2(52f, 12f);
                    barRect.anchoredPosition = new Vector2(side * 17f, 0f);
                    barRect.localRotation = Quaternion.Euler(0f, 0f, side * -35f);
                }

                _chevronLoop.Insert(i * 0.18f, group.DOFade(1f, 0.25f));
                _chevronLoop.Insert(i * 0.18f + 0.3f, group.DOFade(0.25f, 0.35f));
            }

            _chevronLoop.AppendInterval(0.35f).SetLoops(-1);
        }

        // 손가락이 트랙 시작에서 나타나 끝까지 미끄러진 뒤 사라지는 동작을 반복한다.
        Sequence BuildHandLoop()
        {
            var rect = _hand.rectTransform;
            var group = _hand.gameObject.AddComponent<CanvasGroup>();
            float t = 0f;

            void Place(float value)
            {
                t = value;
                rect.anchorMin = rect.anchorMax = Vector2.Lerp(_start, _end, value);
            }

            Place(0f);
            group.alpha = 0f;

            return DOTween.Sequence()
                .AppendCallback(() => Place(0f))
                .Append(group.DOFade(1f, 0.2f))
                .Append(DOTween.To(() => t, value => Place(value), 1f, 0.75f).SetEase(Ease.InOutSine))
                .Append(group.DOFade(0f, 0.2f))
                .AppendInterval(0.35f)
                .SetLoops(-1)
                .SetUpdate(true)
                .SetLink(rect.gameObject);
        }

        void SetGuideVisible(bool visible)
        {
            if (_hand == null)
            {
                return;
            }

            if (visible)
            {
                _hand.gameObject.SetActive(true);
                _handLoop?.Restart();
            }
            else
            {
                _handLoop?.Pause();
                _hand.gameObject.SetActive(false);
            }
        }

        void UpdateCounter(int done)
        {
            if (_counter != null)
            {
                _counter.text = $"{done} / {requiredCount}";
            }
        }

        protected override async UniTask<bool> RunAsync(CancellationToken cancellationToken)
        {
            Vector2 dir = DirectionVector;
            int done = 0;
            float elapsed = 0f;

            // 이미 누르고 있던 손가락은 무시하고, 새로 누른 것부터 센다.
            PointerInput.TryGetPrimary(out _, out bool wasPressed);
            bool dragging = false;
            bool counted = false;
            Vector2 startPosition = Vector2.zero;
            float startTime = 0f;

            while (true)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                elapsed += Time.unscaledDeltaTime;

                if (!PointerInput.TryGetPrimary(out Vector2 position, out bool pressed))
                {
                    continue;
                }

                if (pressed && !wasPressed)
                {
                    dragging = true;
                    counted = false;
                    startPosition = position;
                    startTime = Time.unscaledTime;
                    SetGuideVisible(false);
                }

                if (pressed && dragging && !counted)
                {
                    float needed = requiredDistance * (IsVertical ? Screen.height : Screen.width);
                    Vector2 delta = position - startPosition;
                    float along = Vector2.Dot(delta, dir);
                    SetProgress(Mathf.Clamp01(along / Mathf.Max(1f, needed)));

                    bool farEnough = along >= needed;
                    bool straightEnough = Vector2.Angle(delta, dir) <= maxAngle;
                    bool fastEnough = maxSwipeSeconds <= 0f || Time.unscaledTime - startTime <= maxSwipeSeconds;

                    if (farEnough && straightEnough && fastEnough)
                    {
                        counted = true;
                        done++;
                        UpdateCounter(done);

                        if (done >= requiredCount)
                        {
                            return true;
                        }

                        if (swipeSfx != null)
                        {
                            SoundManager.GetOrCreate().PlaySfx(swipeSfx);
                        }
                    }
                }

                if (!pressed && wasPressed)
                {
                    dragging = false;
                    SetProgress(0f);
                    SetGuideVisible(true);
                }

                wasPressed = pressed;

                if (timeLimitSeconds > 0f && elapsed >= timeLimitSeconds)
                {
                    return false;
                }
            }
        }

        protected override void OnSucceeded()
        {
            _handLoop?.Kill();
            _chevronLoop?.Kill();
            if (_hand != null)
            {
                _hand.gameObject.SetActive(false);
            }
            SetProgress(1f);
        }
    }
}
