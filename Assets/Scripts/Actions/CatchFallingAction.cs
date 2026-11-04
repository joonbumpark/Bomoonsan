using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Mountains
{
    // 화면 위에서 밤 같은 물건이 무작위로 떨어지고, 그걸 눌러서 잡는 액션. requiredCatches개를 잡으면 성공.
    // 놓친 물건은 화면 아래로 빠져나가 사라지고 계속 새로 떨어진다(놓쳐도 실패하지 않는다 —
    // 필요하면 timeLimitSeconds로 끝낼 수 있다).
    //
    // 모든 물건은 오버레이 UI 위에서 움직인다(월드가 아니다). 처음 잡기 전까지는 손가락 그림이 가장
    // 먼저 떨어지는 물건을 따라다니며 "이걸 누르세요"를 알려준다.
    //
    // 물건 그림(itemSprite)을 비워 두면 기본 밤 그림(Resources/Interaction/Chestnut)을 쓴다.
    [Serializable]
    public class CatchFallingAction : InteractionActionBase
    {
        [Header("떨어지는 물건")]
        [Tooltip("비워 두면 기본 밤 그림을 쓴다.")]
        public Sprite itemSprite;
        [Min(20f)] public float itemSize = 160f;
        [Tooltip("물건마다 크기를 이 비율만큼 무작위로 흔든다. 0.15면 85%~115%.")]
        [Range(0f, 0.5f)] public float sizeRandomness = 0.15f;
        [Tooltip("물건보다 이만큼(UI 단위) 넓게 눌러도 잡힌 것으로 본다 — 작은 손가락/빠른 물건을 위한 여유.")]
        [Min(0f)] public float hitPadding = 40f;

        [Header("규칙")]
        [Min(1)] public int requiredCatches = 5;
        [Tooltip("다음 물건이 떨어지기까지의 간격(초) 범위.")]
        public Vector2 spawnInterval = new Vector2(0.45f, 1.0f);
        [Tooltip("떨어지는 속도 범위 — 초당 화면 높이의 몇 배를 이동하는지. 0.3이면 약 3.3초에 화면을 다 내려온다.")]
        public Vector2 fallSpeed = new Vector2(0.22f, 0.38f);
        [Tooltip("물건이 나타나는 가로 범위(0~1 화면 비율).")]
        public Vector2 spawnRangeX = new Vector2(0.12f, 0.88f);
        [Tooltip("동시에 화면에 있을 수 있는 최대 개수.")]
        [Min(1)] public int maxOnScreen = 6;
        [Tooltip("이 시간(초) 안에 다 잡지 못해도 다음 스텝으로 넘어간다. 0이면 다 잡을 때까지 기다린다.")]
        [Min(0f)] public float timeLimitSeconds = 0f;

        [Header("연출")]
        [Tooltip("처음 하나를 잡을 때까지 손가락이 물건을 따라다니며 안내한다.")]
        public bool showHandHint = true;
        [Tooltip("떨어지면서 좌우로 흔들리는 폭(UI 단위).")]
        [Min(0f)] public float swayAmount = 40f;
        [Tooltip("떨어지면서 도는 최대 속도(도/초).")]
        [Min(0f)] public float maxSpin = 120f;

        [Header("소리")]
        public AudioClip catchSfx;

        const string DefaultItemResource = "Interaction/Chestnut";

        class Item
        {
            public RectTransform rect;
            public float baseX;
            public float speed;     // 초당 화면 높이 배수
            public float swayPhase;
            public float swaySpeed;
            public float spin;
            public float radius;    // 판정 반지름(UI 단위)
        }

        readonly List<Item> _items = new List<Item>();
        readonly List<Vector2> _presses = new List<Vector2>();
        Sprite _sprite;
        TMPro.TextMeshProUGUI _counter;
        Image _hand;
        bool _hintDone;

        public CatchFallingAction()
        {
            message = "떨어지는 밤을 눌러서 잡으세요!";
            messageHeight = 0.86f;
            dimAlpha = 0.25f;
        }

        protected override void BuildContent(RectTransform root)
        {
            _items.Clear();
            _hintDone = false;
            _sprite = itemSprite != null ? itemSprite : Resources.Load<Sprite>(DefaultItemResource);

            _counter = UiOverlayRoot.CreateText("Counter", root, fontSize * 0.9f,
                TMPro.TextAlignmentOptions.Center, UiOverlayRoot.ResolveFont());
            var rect = _counter.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, messageHeight);
            rect.anchoredPosition = new Vector2(0f, -fontSize * 1.5f);
            rect.sizeDelta = new Vector2(600f, fontSize * 1.4f);
            UpdateCounter(0);

            if (showHandHint)
            {
                _hand = CreateHand(root, new Vector2(0.5f, 0.5f), "TAP");
                // 물건을 따라다니므로 앵커는 왼쪽 아래(물건과 같은 좌표계)로 둔다.
                _hand.rectTransform.anchorMin = _hand.rectTransform.anchorMax = Vector2.zero;
                _hand.gameObject.SetActive(false);
                _hand.rectTransform.DOScale(0.85f, 0.25f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true).SetLink(_hand.gameObject);
            }
        }

        protected override async UniTask<bool> RunAsync(CancellationToken cancellationToken)
        {
            int caught = 0;
            float elapsed = 0f;
            float nextSpawn = 0f;   // 시작하자마자 하나 떨어뜨린다

            while (true)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                float deltaTime = Time.unscaledDeltaTime;
                elapsed += deltaTime;

                Vector2 area = Root.rect.size;

                // 생성
                nextSpawn -= deltaTime;
                if (nextSpawn <= 0f && _items.Count < maxOnScreen)
                {
                    Spawn(area);
                    nextSpawn = RandomIn(spawnInterval);
                }

                // 이동 + 화면 밖으로 빠진 것 정리
                for (int i = _items.Count - 1; i >= 0; i--)
                {
                    var item = _items[i];
                    var position = item.rect.anchoredPosition;
                    position.y -= item.speed * area.y * deltaTime;
                    item.swayPhase += item.swaySpeed * deltaTime;
                    position.x = item.baseX + Mathf.Sin(item.swayPhase) * swayAmount;
                    item.rect.anchoredPosition = position;
                    item.rect.Rotate(0f, 0f, item.spin * deltaTime);

                    if (position.y < -item.radius * 2f)
                    {
                        UnityEngine.Object.Destroy(item.rect.gameObject);
                        _items.RemoveAt(i);
                    }
                }

                // 터치 판정 — 한 번 누를 때 가장 가까운 물건 하나만 잡는다.
                PointerInput.GetPressPositionsThisFrame(_presses);
                foreach (var screen in _presses)
                {
                    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screen, null, out var local))
                    {
                        continue;
                    }

                    // Root 피벗은 가운데, 물건 좌표는 왼쪽 아래 기준이라 맞춰 준다.
                    Vector2 point = local + area * 0.5f;
                    int hit = FindHit(point);
                    if (hit < 0)
                    {
                        SpawnRipple(screen, new Color(1f, 1f, 1f, 0.35f), 100f);
                        continue;
                    }

                    Catch(hit, screen);
                    caught++;
                    UpdateCounter(caught);
                    if (caught >= requiredCatches)
                    {
                        return true;
                    }
                }

                UpdateHint();

                if (timeLimitSeconds > 0f && elapsed >= timeLimitSeconds)
                {
                    return false;
                }
            }
        }

        void Spawn(Vector2 area)
        {
            float scale = 1f + UnityEngine.Random.Range(-sizeRandomness, sizeRandomness);
            float size = itemSize * scale;

            var rect = UiOverlayRoot.CreateRect("FallingItem", Root);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            // 안내 문구/손가락보다 뒤에 그려지도록 맨 앞(형제 순서 처음 다음)에 둔다.
            rect.SetSiblingIndex(0);

            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            if (_sprite != null)
            {
                image.sprite = _sprite;
            }
            else
            {
                image.sprite = CircleSprite();
                image.color = new Color(0.55f, 0.3f, 0.15f, 1f);
            }

            float x = Mathf.Lerp(spawnRangeX.x, spawnRangeX.y, UnityEngine.Random.value) * area.x;
            rect.anchoredPosition = new Vector2(x, area.y + size);
            rect.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-25f, 25f));

            _items.Add(new Item
            {
                rect = rect,
                baseX = x,
                speed = RandomIn(fallSpeed),
                swayPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
                swaySpeed = UnityEngine.Random.Range(1.5f, 3f),
                spin = UnityEngine.Random.Range(-maxSpin, maxSpin),
                radius = size * 0.5f
            });
        }

        int FindHit(Vector2 point)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                float distance = Vector2.Distance(point, item.rect.anchoredPosition);
                if (distance <= item.radius + hitPadding && distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }
            return best;
        }

        void Catch(int index, Vector2 screen)
        {
            var item = _items[index];
            _items.RemoveAt(index);
            _hintDone = true;

            if (catchSfx != null)
            {
                SoundManager.GetOrCreate().PlaySfx(catchSfx, 1f, UnityEngine.Random.Range(0.95f, 1.1f));
            }

            SpawnRipple(screen, new Color(1f, 0.9f, 0.4f, 0.7f), item.radius * 2.2f);

            // 톡 커졌다가 작아지며 사라지는 잡기 연출.
            var rect = item.rect;
            var image = rect.GetComponent<Image>();
            DOTween.Sequence()
                .Append(rect.DOScale(1.35f, 0.1f).SetEase(Ease.OutQuad))
                .Append(rect.DOScale(0f, 0.18f).SetEase(Ease.InBack))
                .Join(image.DOFade(0f, 0.18f))
                .SetUpdate(true)
                .SetLink(rect.gameObject)
                .OnComplete(() => UnityEngine.Object.Destroy(rect.gameObject));

            if (_counter != null)
            {
                _counter.rectTransform.DOComplete();
                _counter.rectTransform.DOPunchScale(Vector3.one * 0.2f, 0.25f, 6, 0.6f).SetUpdate(true)
                    .SetLink(_counter.gameObject);
            }
        }

        // 처음 잡기 전까지 손가락이 가장 아래(가장 먼저 잡기 좋은) 물건 위를 가리킨다.
        void UpdateHint()
        {
            if (_hand == null)
            {
                return;
            }

            if (_hintDone || _items.Count == 0)
            {
                _hand.gameObject.SetActive(false);
                return;
            }

            Item target = null;
            foreach (var item in _items)
            {
                // 화면 안에 들어온 것 중 가장 아래에 있는 것.
                if (item.rect.anchoredPosition.y > Root.rect.height)
                {
                    continue;
                }
                if (target == null || item.rect.anchoredPosition.y < target.rect.anchoredPosition.y)
                {
                    target = item;
                }
            }

            if (target == null)
            {
                _hand.gameObject.SetActive(false);
                return;
            }

            _hand.gameObject.SetActive(true);
            // 손가락 끝(피벗)이 물건 가운데 조금 아래를 가리키게 한다.
            _hand.rectTransform.anchoredPosition = target.rect.anchoredPosition + new Vector2(0f, -target.radius * 0.3f);
            _hand.rectTransform.SetAsLastSibling();
        }

        void UpdateCounter(int caught)
        {
            if (_counter != null)
            {
                _counter.text = $"{caught} / {requiredCatches}";
            }
        }

        static float RandomIn(Vector2 range)
        {
            float min = Mathf.Min(range.x, range.y);
            float max = Mathf.Max(range.x, range.y);
            return UnityEngine.Random.Range(min, max);
        }

        protected override void OnSucceeded()
        {
            if (_hand != null)
            {
                _hand.gameObject.SetActive(false);
            }

            // 남은 물건은 빠르게 흩어 사라지게 한다.
            foreach (var item in _items)
            {
                var rect = item.rect;
                rect.DOScale(0f, 0.2f).SetUpdate(true).SetLink(rect.gameObject);
            }
            _items.Clear();
        }
    }
}
