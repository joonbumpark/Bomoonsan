using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mountains
{
    // 팝업/토스트가 공유하는 런타임 오버레이 Canvas와, UI 요소를 코드로 만드는 헬퍼들.
    //
    // 씬에 UI를 미리 배치해두지 않아도 쓸 수 있게 만든다(CharacterPortraitStage가 초상화
    // 스테이지를 런타임에 만드는 것과 같은 방식) — 팝업 하나 띄우려고 프리팹을 먼저
    // 준비해야 하면 "범용"이라 하기 어렵다. 나중에 디자인이 정해지면 씬에 배치한
    // MessagePopupUI/ToastUI가 우선하므로 코드 생성은 자동으로 비켜준다.
    public static class UiOverlayRoot
    {
        // HUD(기본 0)와 대화창보다 확실히 위에 오도록 큰 값을 준다.
        const int SortingOrder = 500;

        static Transform _root;

        public static Transform GetOrCreate()
        {
            // 파괴된 오브젝트를 참조하고 있으면 Unity의 == null이 true를 돌려주므로,
            // 씬을 다시 로드해도 자동으로 새로 만든다.
            if (_root != null)
            {
                return _root;
            }

            var go = new GameObject("UiOverlay", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();

            _root = go.transform;
            return _root;
        }

        // 버튼 클릭은 EventSystem이 없으면 아예 들어오지 않는다. 조이스틱이 있는 씬이면
        // 이미 있지만, 빈 테스트 씬에서도 팝업이 동작하게 보장해둔다.
        static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }
            if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        // 코드로 만든 TMP 텍스트는 TMP 기본 폰트(LiberationSans)를 쓰는데 한글 글리프가
        // 없어서 전부 네모로 나온다. 프로젝트에 NotoSansKR이 있지만 Resources 폴더 밖이라
        // 런타임에 경로로 못 불러오므로, 씬의 대화창이 쓰고 있는 폰트를 그대로 빌려온다.
        public static TMP_FontAsset ResolveFont()
        {
            var dialog = DialogUI.Instance != null
                ? DialogUI.Instance
                : Object.FindFirstObjectByType<DialogUI>(FindObjectsInactive.Include);

            if (dialog != null && dialog.bodyText != null && dialog.bodyText.font != null)
            {
                return dialog.bodyText.font;
            }

            return TMP_Settings.defaultFontAsset;
        }

        // 런타임 UI를 찾거나 만드는 순서: 이미 잡아둔 인스턴스 -> 씬에 배치된 것 ->
        // 카탈로그의 프리팹 -> 코드 생성. 팝업과 토스트가 같은 순서를 각자 구현하고
        // 있었는데, 한쪽만 고치면 두 시스템의 동작이 조용히 달라지므로 여기로 모은다.
        public static T ResolveOrCreate<T>(T current, T prefab, System.Func<Transform, T> build)
            where T : Component
        {
            if (current != null)
            {
                return current;
            }

            var existing = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (existing != null)
            {
                return existing;
            }

            if (prefab != null)
            {
                var spawned = Object.Instantiate(prefab, GetOrCreate());
                // Instantiate가 붙이는 "(Clone)"을 떼서 하이어라키를 읽기 쉽게 한다.
                spawned.name = prefab.name;
                return spawned;
            }

            return build(GetOrCreate());
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        public static Image CreateImage(string name, Transform parent, Color color)
        {
            var rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI CreateText(string name, Transform parent, float fontSize,
            TextAlignmentOptions alignment, TMP_FontAsset font)
        {
            var rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
            }
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        // 버튼 배경 + 가운데 라벨 한 쌍을 만든다. 라벨은 out으로 돌려줘서 호출한 쪽이
        // 문구를 바꿀 수 있게 한다.
        public static Button CreateButton(string name, Transform parent, Color color, float fontSize,
            TMP_FontAsset font, out TextMeshProUGUI label)
        {
            var image = CreateImage(name, parent, color);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            label = CreateText("Label", image.transform, fontSize, TextAlignmentOptions.Center, font);
            Stretch(label.rectTransform);

            return button;
        }

        // 부모를 꽉 채우도록 앵커를 맞춘다.
        public static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        // 이 프로젝트는 Enter Play Mode Options에서 도메인 리로드를 꺼둬서 static 값이
        // Play 세션 사이에 남는다 — InputBlocker와 같은 이유로 씬 로드 전에 초기화한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            _root = null;
        }
    }
}
