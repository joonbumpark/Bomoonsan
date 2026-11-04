using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Mountains
{
    // 화면을 연타해서 게이지를 채우는 액션. 게이지는 가만히 두면 계속 줄어들기 때문에, 목표선 위에서
    // holdSeconds 동안 버티려면 쉬지 않고 두드려야 한다 — "막 연타"를 유도하는 미니 이벤트.
    //
    // 화면 구성: 위쪽 안내 문구, 가운데 게이지(목표선 표시), 그 아래 유지 시간 막대, 아래쪽에 두드리는
    // 손가락 그림. 두드릴 때마다 손가락 위치에 물결이 퍼지고 게이지가 살짝 튄다.
    [Serializable]
    public class TapGaugeAction : InteractionActionBase
    {
        [Header("게이지")]
        [Tooltip("한 번 두드릴 때 차는 양 (게이지 전체 = 1).")]
        [Range(0.005f, 1f)] public float gainPerTap = 0.07f;
        [Tooltip("초당 자연적으로 줄어드는 양. 클수록 더 빨리 두드려야 한다.")]
        [Min(0f)] public float decayPerSecond = 0.35f;
        [Tooltip("이 선 위에 머물러야 유지 시간이 쌓인다.")]
        [Range(0.05f, 1f)] public float targetFill = 0.8f;
        [Tooltip("목표선 위에서 이만큼(초) 버티면 성공. 0이면 목표선에 닿는 순간 성공.")]
        [Min(0f)] public float holdSeconds = 1.5f;
        [Tooltip("켜면 목표선 아래로 떨어질 때 유지 시간을 처음부터 다시 잰다. 끄면 떨어져 있는 동안 서서히 줄어든다.")]
        public bool resetHoldWhenBelow = false;
        [Tooltip("이 시간(초) 안에 성공하지 못해도 다음 스텝으로 넘어간다. 0이면 성공할 때까지 기다린다.")]
        [Min(0f)] public float timeLimitSeconds = 0f;

        [Header("표시")]
        [Range(0f, 1f)] public float gaugeHeight = 0.5f;
        [Min(100f)] public float gaugeWidth = 780f;
        [Range(0f, 1f)] public float handHeight = 0.3f;
        public Color fillColor = new Color(1f, 0.72f, 0.2f, 1f);
        public Color reachedColor = new Color(0.35f, 0.9f, 0.45f, 1f);

        [Header("소리")]
        public AudioClip tapSfx;

        public enum FxPlacement
        {
            [Tooltip("tapFxAt(비우면 트리거) 위치에 생성")] AtTarget,
            [Tooltip("두드린 화면 지점이 가리키는 월드 위치(지형/콜라이더)에 생성")] AtTapPoint
        }

        [Header("두드릴 때 FX")]
        [Tooltip("두드릴 때마다 생성할 FX 프리팹(파티클 등). 비우면 생성하지 않는다.")]
        public GameObject tapFxPrefab;
        public FxPlacement fxPlacement = FxPlacement.AtTarget;
        [Tooltip("AtTarget일 때 FX가 나올 위치. 비우면 트리거 위치.")]
        [GizmoTarget("연타 FX")] public Transform tapFxAt;
        public Vector3 tapFxOffset;
        [Tooltip("생성 위치를 이 반경(m) 안에서 무작위로 흩뜨린다 — 같은 자리에 겹쳐 보이지 않게.")]
        [Min(0f)] public float tapFxScatter = 0.3f;
        [Min(0.01f)] public float tapFxScale = 1f;
        [Tooltip("생성된 FX를 이 시간(초) 뒤에 지운다. 0 이하면 FX가 스스로 정리한다고 본다.")]
        public float tapFxLifetime = 2f;
        [Tooltip("FX 사이 최소 간격(초). 아주 빠른 연타에서 FX가 너무 많이 쌓이는 걸 막는다. 0이면 매번 생성.")]
        [Min(0f)] public float tapFxMinInterval = 0.05f;
        [Tooltip("AtTapPoint에서 아무것도 맞지 않았을 때, 카메라 앞 이 거리(m)에 생성한다.")]
        [Min(0.1f)] public float tapPointFallbackDistance = 6f;

        const float BarHeight = 64f;
        const float HoldBarHeight = 16f;

        RectTransform _gauge;
        RectTransform _fill;
        Image _fillImage;
        RectTransform _holdFill;
        Image _hand;
        Tween _handLoop;
        float _nextFxTime;

        public TapGaugeAction()
        {
            message = "화면을 빠르게 연타하세요!";
        }

        protected override void BuildContent(RectTransform root)
        {
            // 게이지 바탕 + 채움. 채움은 앵커의 오른쪽 끝(anchorMax.x)으로 길이를 조절한다.
            var background = CreatePill("Gauge", root, new Color(0f, 0f, 0f, 0.55f), BarHeight);
            _gauge = background.rectTransform;
            _gauge.anchorMin = _gauge.anchorMax = new Vector2(0.5f, gaugeHeight);
            _gauge.sizeDelta = new Vector2(gaugeWidth, BarHeight);

            var fillArea = UiOverlayRoot.CreateRect("FillArea", _gauge);
            UiOverlayRoot.Stretch(fillArea, 6f);

            _fillImage = CreatePill("Fill", fillArea, fillColor, BarHeight - 12f);
            _fill = _fillImage.rectTransform;
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = new Vector2(0f, 1f);
            _fill.offsetMin = Vector2.zero;
            _fill.offsetMax = Vector2.zero;

            // 목표선: 게이지를 위아래로 조금 넘는 흰 막대.
            var marker = UiOverlayRoot.CreateImage("Target", _gauge, Color.white);
            marker.raycastTarget = false;
            var markerRect = marker.rectTransform;
            markerRect.anchorMin = markerRect.anchorMax = new Vector2(targetFill, 0.5f);
            markerRect.sizeDelta = new Vector2(8f, BarHeight + 28f);

            // 유지 시간 막대 (목표를 버틴 정도).
            if (holdSeconds > 0f)
            {
                var holdBackground = CreatePill("Hold", root, new Color(0f, 0f, 0f, 0.45f), HoldBarHeight);
                var holdRect = holdBackground.rectTransform;
                holdRect.anchorMin = holdRect.anchorMax = new Vector2(0.5f, gaugeHeight);
                holdRect.anchoredPosition = new Vector2(0f, -(BarHeight * 0.5f + 34f));
                holdRect.sizeDelta = new Vector2(gaugeWidth * 0.6f, HoldBarHeight);

                var holdFill = CreatePill("HoldFill", holdRect, reachedColor, HoldBarHeight);
                _holdFill = holdFill.rectTransform;
                _holdFill.anchorMin = Vector2.zero;
                _holdFill.anchorMax = new Vector2(0f, 1f);
                _holdFill.offsetMin = Vector2.zero;
                _holdFill.offsetMax = Vector2.zero;
            }

            // 두드리는 손가락: 살짝 눌렀다 떼는 동작을 반복한다.
            _hand = CreateHand(root, new Vector2(0.5f, handHeight), "TAP");
            var handRect = _hand.rectTransform;
            _handLoop = handRect.DOScale(0.82f, 0.16f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(handRect.gameObject);
        }

        protected override async UniTask<bool> RunAsync(CancellationToken cancellationToken)
        {
            float gauge = 0f;
            float hold = 0f;
            float elapsed = 0f;

            while (true)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

                float deltaTime = Time.unscaledDeltaTime;
                elapsed += deltaTime;

                int presses = PointerInput.PressesThisFrame();
                if (presses > 0)
                {
                    gauge = Mathf.Min(1f, gauge + gainPerTap * presses);
                    OnTapped();
                }

                gauge = Mathf.Max(0f, gauge - decayPerSecond * deltaTime);

                bool reached = gauge >= targetFill;
                if (reached)
                {
                    hold += deltaTime;
                }
                else
                {
                    hold = resetHoldWhenBelow ? 0f : Mathf.Max(0f, hold - deltaTime);
                }

                UpdateVisuals(gauge, hold, reached);

                if (holdSeconds <= 0f ? reached : hold >= holdSeconds)
                {
                    return true;
                }

                if (timeLimitSeconds > 0f && elapsed >= timeLimitSeconds)
                {
                    return false;
                }
            }
        }

        void OnTapped()
        {
            if (tapSfx != null)
            {
                SoundManager.GetOrCreate().PlaySfx(tapSfx, 1f, UnityEngine.Random.Range(0.95f, 1.08f));
            }

            bool hasPosition = PointerInput.TryGetPrimary(out var position, out _);
            if (hasPosition)
            {
                SpawnRipple(position, new Color(1f, 1f, 1f, 0.6f));
            }

            SpawnTapFx(hasPosition, position);

            // 게이지가 탁탁 튀는 느낌. 연타 중 트윈이 쌓이지 않게 직전 것을 끝내고 다시 건다.
            _gauge.DOComplete();
            _gauge.DOPunchScale(new Vector3(0.04f, 0.12f, 0f), 0.12f, 1, 0f).SetUpdate(true).SetLink(_gauge.gameObject);
        }

        void SpawnTapFx(bool hasScreenPosition, Vector2 screenPosition)
        {
            if (tapFxPrefab == null || Time.unscaledTime < _nextFxTime)
            {
                return;
            }
            _nextFxTime = Time.unscaledTime + tapFxMinInterval;

            Vector3 position = ResolveFxPosition(hasScreenPosition, screenPosition) + tapFxOffset;
            if (tapFxScatter > 0f)
            {
                Vector2 scatter = UnityEngine.Random.insideUnitCircle * tapFxScatter;
                position += new Vector3(scatter.x, 0f, scatter.y);
            }

            var fx = FxUtility.Spawn(tapFxPrefab, position, tapFxLifetime);
            // 필드가 생기기 전에 저장된 액션은 0으로 읽힐 수 있다 — 0배면 안 보이므로 1로 본다.
            FxUtility.ApplyScale(fx, tapFxScale > 0f ? tapFxScale : 1f);
        }

        Vector3 ResolveFxPosition(bool hasScreenPosition, Vector2 screenPosition)
        {
            if (fxPlacement == FxPlacement.AtTapPoint && hasScreenPosition)
            {
                var camera = Camera.main;
                if (camera != null)
                {
                    // 안내 UI는 화면을 덮지만 물리 레이캐스트에는 영향이 없다 — 화면 너머의 지형/오브젝트를 맞춘다.
                    Ray ray = camera.ScreenPointToRay(screenPosition);
                    return Physics.Raycast(ray, out var hit, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                        ? hit.point
                        : ray.GetPoint(tapPointFallbackDistance > 0f ? tapPointFallbackDistance : 6f);
                }
            }

            if (tapFxAt != null)
            {
                return tapFxAt.position;
            }

            return Context != null && Context.Transform != null ? Context.Transform.position : Vector3.zero;
        }

        void UpdateVisuals(float gauge, float hold, bool reached)
        {
            _fill.anchorMax = new Vector2(gauge, 1f);
            _fillImage.color = reached ? reachedColor : fillColor;

            if (_holdFill != null)
            {
                _holdFill.anchorMax = new Vector2(Mathf.Clamp01(hold / holdSeconds), 1f);
            }
        }

        protected override void OnSucceeded()
        {
            _handLoop?.Kill();
            if (_hand != null)
            {
                _hand.gameObject.SetActive(false);
            }

            if (_fill != null)
            {
                _fill.anchorMax = Vector2.one;
                _fillImage.color = reachedColor;
            }

            if (_holdFill != null)
            {
                _holdFill.anchorMax = Vector2.one;
            }
        }
    }
}
