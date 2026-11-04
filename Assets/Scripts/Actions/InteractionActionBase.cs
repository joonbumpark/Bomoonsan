using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mountains
{
    // 플레이어의 입력(연타, 스와이프 등)을 받아야 다음 스텝으로 넘어가는 연출 액션들의 공통 뼈대.
    // WaitForTouchAction과 같은 방식이다:
    //   - UiOverlayRoot의 오버레이 캔버스 위에 화면 전체를 덮는 반투명 판을 깔고(뒤쪽 조이스틱/버튼이
    //     터치를 가져가지 않게), 그 위에 안내 문구와 손가락 그림을 만든다.
    //   - 입력은 PointerInput으로 매 프레임 직접 읽는다.
    //   - 어떤 경로로 끝나든(성공, 시간 초과, 트리거 파괴로 취소) finally에서 UI와 입력 잠금을 정리한다.
    //
    // 손가락 그림(handSprite)을 비워 두면 임시로 동그라미를 그린다 — 실제 이미지가 정해지면
    // 인스펙터에서 스프라이트만 넣으면 된다.
    [Serializable]
    public abstract class InteractionActionBase : TriggerAction
    {
        [Header("안내")]
        [TextArea(1, 3)] public string message = "";
        [Min(1f)] public float fontSize = 56f;
        [Tooltip("안내 문구의 세로 위치. 0 = 화면 맨 아래, 1 = 맨 위.")]
        [Range(0f, 1f)] public float messageHeight = 0.78f;
        [Tooltip("화면을 살짝 어둡게 깐다. 0이면 그대로 둔다.")]
        [Range(0f, 1f)] public float dimAlpha = 0.35f;

        [Header("손가락 그림")]
        [Tooltip("비워 두면 기본 손가락 그림(Resources/Interaction/Hand)을 쓰고, 그것도 없으면 임시 동그라미를 쓴다.")]
        public Sprite handSprite;
        [Min(16f)] public float handSize = 180f;
        [Tooltip("그림에서 손가락 끝의 위치(0~1). 이 점이 안내 위치에 오고, 누르는 동작도 이 점을 중심으로 한다.")]
        public Vector2 handTipPivot = new Vector2(0.43f, 0.93f);

        [Header("입력")]
        [Tooltip("안내가 뜬 뒤 이 시간(초) 동안은 입력을 무시한다 — 직전 대화를 넘기던 손가락이 바로 반영되지 않게.")]
        [Min(0f)] public float ignoreInputSeconds = 0.3f;
        [Tooltip("켜두면 진행 중에 플레이어 조작(조이스틱/드래그 회전)을 막는다.")]
        public bool blockPlayerInput = true;

        [Header("성공")]
        [Tooltip("성공했을 때 잠깐 보여줄 문구. 비우면 문구를 바꾸지 않는다.")]
        public string successMessage = "성공!";
        [Tooltip("성공 연출을 보여주는 시간(초). 이 시간이 지나야 다음 스텝으로 넘어간다.")]
        [Min(0f)] public float successHoldSeconds = 0.7f;
        public AudioClip successSfx;

        protected RectTransform Root { get; private set; }
        protected TriggerContext Context { get; private set; }
        protected TextMeshProUGUI MessageText { get; private set; }

        public override async UniTask ExecuteAsync(TriggerContext context, CancellationToken cancellationToken)
        {
            Context = context;
            GameObject cover = BuildCover();

            if (blockPlayerInput)
            {
                InputBlocker.Block();
            }

            try
            {
                BuildContent(Root);

                // unscaledTime: 일시정지(timeScale 0) 중에도 흘러야 한다.
                float readyTime = Time.unscaledTime + ignoreInputSeconds;
                while (Time.unscaledTime < readyTime)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }

                bool succeeded = await RunAsync(cancellationToken);
                if (succeeded)
                {
                    await CelebrateAsync(cancellationToken);
                }
            }
            finally
            {
                if (blockPlayerInput)
                {
                    InputBlocker.Unblock();
                }

                if (cover != null)
                {
                    // 트윈은 전부 SetLink로 이 오브젝트들에 묶어 두어서, 지우면 함께 정리된다.
                    UnityEngine.Object.Destroy(cover);
                }

                Root = null;
                MessageText = null;
                Context = null;
            }
        }

        // 안내 판 위에 이 액션 고유의 UI(게이지, 트랙 등)를 만든다.
        protected abstract void BuildContent(RectTransform root);

        // 입력을 받는 본체. 성공하면 true, 성공 없이 끝나면(시간 초과 등) false.
        protected abstract UniTask<bool> RunAsync(CancellationToken cancellationToken);

        // 성공했을 때 손가락 등을 숨기고 싶으면 덮어쓴다.
        protected virtual void OnSucceeded()
        {
        }

        async UniTask CelebrateAsync(CancellationToken cancellationToken)
        {
            OnSucceeded();

            if (successSfx != null)
            {
                SoundManager.GetOrCreate().PlaySfx(successSfx);
            }

            if (MessageText != null)
            {
                if (!string.IsNullOrEmpty(successMessage))
                {
                    MessageText.text = successMessage;
                }

                var rect = MessageText.rectTransform;
                rect.DOKill();
                rect.localScale = Vector3.one;
                rect.DOPunchScale(Vector3.one * 0.25f, 0.4f, 6, 0.6f).SetUpdate(true).SetLink(rect.gameObject);
            }

            if (successHoldSeconds > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(successHoldSeconds), ignoreTimeScale: true,
                    cancellationToken: cancellationToken);
            }
        }

        GameObject BuildCover()
        {
            var parent = UiOverlayRoot.GetOrCreate();

            var cover = UiOverlayRoot.CreateImage(GetType().Name, parent, new Color(0f, 0f, 0f, dimAlpha));
            cover.raycastTarget = true;
            // 알파가 0이어도 레이캐스트는 받아야 하므로 투명 메시 컬링을 끈다.
            cover.canvasRenderer.cullTransparentMesh = false;
            UiOverlayRoot.Stretch(cover.rectTransform);
            Root = cover.rectTransform;

            MessageText = UiOverlayRoot.CreateText("Message", cover.transform, fontSize,
                TextAlignmentOptions.Center, UiOverlayRoot.ResolveFont());
            MessageText.text = message;

            var rect = MessageText.rectTransform;
            rect.anchorMin = new Vector2(0.05f, messageHeight);
            rect.anchorMax = new Vector2(0.95f, messageHeight);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 220f);

            return cover.gameObject;
        }

        // ───────────── UI 헬퍼 ─────────────

        // 화면의 한 점(앵커, 0~1)에 놓이는 고정 크기 사각형.
        protected static RectTransform CreateAnchored(string name, Transform parent, Vector2 anchor, Vector2 size)
        {
            var rect = UiOverlayRoot.CreateRect(name, parent);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            return rect;
        }

        const string DefaultHandResource = "Interaction/Hand";

        // 손가락 그림. 지정한 스프라이트 → 기본 그림(Resources) → 동그라미 + dummyLabel 순으로 쓴다.
        // 손가락 끝(handTipPivot)이 안내 지점에 오도록 피벗을 맞춘다.
        protected Image CreateHand(Transform parent, Vector2 anchor, string dummyLabel)
        {
            var rect = CreateAnchored("Hand", parent, anchor, new Vector2(handSize, handSize));

            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;

            var sprite = handSprite != null ? handSprite : Resources.Load<Sprite>(DefaultHandResource);
            if (sprite != null)
            {
                // 이 필드가 생기기 전에 저장된 액션은 [SerializeReference]라 초기값 대신 (0,0)으로 읽힐 수 있다.
                rect.pivot = handTipPivot != Vector2.zero ? handTipPivot : new Vector2(0.43f, 0.93f);
                image.sprite = sprite;
                image.color = Color.white;
            }
            else
            {
                rect.pivot = new Vector2(0.5f, 0.85f);
                image.sprite = CircleSprite();
                image.color = new Color(1f, 1f, 1f, 0.85f);

                var label = UiOverlayRoot.CreateText("DummyLabel", rect, handSize * 0.24f,
                    TextAlignmentOptions.Center, UiOverlayRoot.ResolveFont());
                label.text = dummyLabel;
                label.color = new Color(0.15f, 0.15f, 0.2f, 1f);
                UiOverlayRoot.Stretch(label.rectTransform);
            }

            return image;
        }

        // 둥근 막대(게이지 등). 높이의 절반을 모서리 반지름으로 쓴다.
        protected static Image CreatePill(string name, Transform parent, Color color, float height)
        {
            var rect = UiOverlayRoot.CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.sprite = RoundSprite();
            image.type = Image.Type.Sliced;
            // 스프라이트 테두리(64px)가 화면에서 height/2 크기가 되게 맞춘다.
            image.pixelsPerUnitMultiplier = 64f / Mathf.Max(1f, height * 0.5f);
            image.color = color;
            return image;
        }

        // 화면 좌표의 지점에서 동그라미가 퍼지며 사라지는 터치 피드백.
        protected void SpawnRipple(Vector2 screenPosition, Color color, float size = 140f)
        {
            if (Root == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screenPosition, null, out var local))
            {
                return;
            }

            var rect = CreateAnchored("Ripple", Root, new Vector2(0.5f, 0.5f), new Vector2(size, size));
            rect.anchoredPosition = local;
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.sprite = CircleSprite();
            image.color = color;

            rect.localScale = Vector3.one * 0.3f;
            rect.DOScale(1.4f, 0.35f).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(rect.gameObject);
            image.DOFade(0f, 0.35f).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(rect.gameObject)
                .OnComplete(() => UnityEngine.Object.Destroy(rect.gameObject));
        }

        // ───────────── 임시 그림 ─────────────
        // 실제 이미지가 없을 때 쓰는 동그라미/둥근 막대를 코드로 한 번만 만들어 둔다.

        static Sprite _circle;
        static Sprite _round;

        protected static Sprite CircleSprite()
        {
            if (_circle == null)
            {
                _circle = MakeCircleSprite(Vector4.zero);
            }
            return _circle;
        }

        static Sprite RoundSprite()
        {
            if (_round == null)
            {
                _round = MakeCircleSprite(new Vector4(63f, 63f, 63f, 63f));
            }
            return _round;
        }

        static Sprite MakeCircleSprite(Vector4 border)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "InteractionCircle",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };

            var pixels = new Color32[size * size];
            float radius = size * 0.5f - 1f;
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    byte alpha = (byte)(Mathf.Clamp01(radius - distance + 0.5f) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
