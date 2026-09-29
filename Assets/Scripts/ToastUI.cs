using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mountains
{
    // 화면 아래쪽에 잠깐 떴다 사라지는 짧은 알림(토스트)들을 관리한다. 보통 Toast 정적
    // 파사드로 호출한다.
    //
    // 팝업과 달리 모달이 아니라서 InputBlocker를 쓰지 않고, 레이캐스트도 받지 않는다
    // (raycastTarget 끔) — 코인 획득 같은 알림 때문에 조이스틱이 막히면 안 된다.
    public class ToastUI : MonoBehaviour
    {
        public static ToastUI Instance { get; private set; }

        [Header("References")]
        [Tooltip("토스트가 쌓이는 컨테이너. 비워두면 이 오브젝트의 RectTransform을 쓴다.")]
        public RectTransform container;
        public TMP_FontAsset font;

        [Header("동작")]
        public float defaultDuration = 2f;
        public float fadeDuration = 0.25f;
        [Tooltip("동시에 보일 수 있는 최대 개수. 넘치면 가장 오래된 것부터 사라진다.")]
        [Min(1)] public int maxVisible = 3;

        readonly List<CanvasGroup> _active = new List<CanvasGroup>();

        void Awake()
        {
            Instance = this;
            if (container == null)
            {
                container = transform as RectTransform;
            }
        }

        public static ToastUI GetOrCreate()
        {
            Instance = UiOverlayRoot.ResolveOrCreate(Instance,
                UiPrefabCatalog.Load()?.toastPrefab, Build);
            return Instance;
        }

        public void Show(string message, float duration = -1f)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }
            if (container == null)
            {
                container = transform as RectTransform;
            }

            // 넘치는 만큼 오래된 것을 먼저 치운다 — 새 알림이 화면 밖으로 밀려나가는 것보다
            // 가장 최근 알림이 보이는 게 중요하다.
            while (_active.Count >= maxVisible)
            {
                RemoveOldest();
            }

            var item = CreateItem(message);
            _active.Add(item);
            StartCoroutine(PlayToast(item, duration > 0f ? duration : defaultDuration));
        }

        CanvasGroup CreateItem(string message)
        {
            var bubble = UiOverlayRoot.CreateImage("Toast", container, new Color(0f, 0f, 0f, 0.75f));
            bubble.raycastTarget = false;

            var group = bubble.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            // 바깥 VerticalLayoutGroup이 폭을 정해주고, 이 Fitter가 줄바꿈된 글자 수에 맞춰
            // 높이만 늘린다 — 폭까지 Fitter에 맡기면 줄바꿈 계산과 서로 물려 요동친다.
            var padding = bubble.gameObject.AddComponent<VerticalLayoutGroup>();
            padding.padding = new RectOffset(32, 32, 20, 20);
            padding.childControlWidth = true;
            padding.childControlHeight = true;
            padding.childForceExpandWidth = true;
            padding.childForceExpandHeight = false;

            var fitter = bubble.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var text = UiOverlayRoot.CreateText("Message", bubble.transform, 40f,
                TextAlignmentOptions.Center, font != null ? font : UiOverlayRoot.ResolveFont());
            text.text = message;

            return group;
        }

        IEnumerator PlayToast(CanvasGroup item, float duration)
        {
            item.DOFade(1f, fadeDuration);
            yield return new WaitForSeconds(fadeDuration + duration);

            if (item == null)
            {
                yield break;
            }

            // 페이드 아웃이 끝나기 전에 maxVisible 초과로 먼저 치워질 수 있으므로,
            // 완료 콜백이 아니라 Kill로 정리 시점을 받는다(액션들과 같은 이유).
            _active.Remove(item);
            item.DOFade(0f, fadeDuration).OnKill(() =>
            {
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            });
        }

        void RemoveOldest()
        {
            var oldest = _active[0];
            _active.RemoveAt(0);

            if (oldest == null)
            {
                return;
            }

            oldest.DOKill();
            Destroy(oldest.gameObject);
        }

        // parent를 받는 이유: 에디터에서 프리팹을 만들 때는 런타임 오버레이 캔버스가 아니라
        // 임시 캔버스 밑에 지어서 저장한 뒤 지워야 한다(씬을 더럽히지 않게).
        public static ToastUI Build(Transform root)
        {
            // 화면 아래 중앙에 세로로 쌓는다. 조이스틱이 보통 하단에 있으므로 조금 띄운다.
            var containerRect = UiOverlayRoot.CreateRect("ToastContainer", root);
            containerRect.anchorMin = new Vector2(0.1f, 0f);
            containerRect.anchorMax = new Vector2(0.9f, 0f);
            containerRect.pivot = new Vector2(0.5f, 0f);
            containerRect.anchoredPosition = new Vector2(0f, 420f);
            containerRect.sizeDelta = new Vector2(0f, 0f);

            var layout = containerRect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = containerRect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var toast = containerRect.gameObject.AddComponent<ToastUI>();
            toast.container = containerRect;
            // 프리팹으로 저장될 수 있으므로 폰트를 값으로 박아둔다 — 프리팹은 씬의 대화창을
            // 참조할 수 없어서 런타임 해석에만 의존하면 한글이 네모로 나올 수 있다.
            toast.font = UiOverlayRoot.ResolveFont();
            return toast;
        }
    }
}
