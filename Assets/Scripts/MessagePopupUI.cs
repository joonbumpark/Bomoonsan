using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mountains
{
    // 범용 모달 메시지 팝업의 뷰. 씬에 배치해서 디자인을 직접 붙일 수도 있고, 없으면
    // GetOrCreate()가 코드로 기본 모양을 만든다. 호출은 보통 MessagePopup 정적 파사드로 한다.
    //
    // 큐를 두는 이유: 모달이라 동시에 두 개를 띄울 수 없는데, 이벤트 시퀀스에서 여러 액션이
    // 병렬로 돌다가 팝업을 겹쳐 요청하는 일이 실제로 생긴다(EventTrigger의 한 스텝). 뒤에
    // 온 요청을 버리면 콜백이 유실되므로 순서대로 보여준다.
    public class MessagePopupUI : MonoBehaviour
    {
        public static MessagePopupUI Instance { get; private set; }

        [Header("References")]
        public CanvasGroup canvasGroup;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI messageText;
        public Button confirmButton;
        public TextMeshProUGUI confirmLabel;
        public Button cancelButton;
        public TextMeshProUGUI cancelLabel;
        [Tooltip("화면 전체를 덮는 배경 버튼. 탭하면 취소로 처리하며 닫는다.")]
        public Button backgroundButton;

        readonly Queue<MessagePopupRequest> _queue = new Queue<MessagePopupRequest>();
        MessagePopupRequest _current;
        bool _initialized;
        bool _inputBlocked;

        public bool IsShowing => _current != null;

        void Awake()
        {
            Instance = this;
            // 씬에 배치된 경우엔 참조가 이미 채워져 있으므로 여기서 초기화한다. 코드로
            // 만드는 경우엔 AddComponent 시점에 참조가 비어 있어서 Build()가 나중에 부른다.
            if (canvasGroup != null)
            {
                Initialize();
            }
        }

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }
            _initialized = true;

            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(() => Close(true));
            }
            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(() => Close(false));
            }
            if (backgroundButton != null)
            {
                backgroundButton.onClick.AddListener(OnClickBackground);
            }

            SetVisible(false);
        }

        public static MessagePopupUI GetOrCreate()
        {
            Instance = UiOverlayRoot.ResolveOrCreate(Instance,
                UiPrefabCatalog.Load()?.messagePopupPrefab, MessagePopupBuilder.Build);
            // 씬 배치본이든 프리팹이든 코드 생성이든 버튼 연결은 한 번은 해줘야 한다
            // (두 번 불러도 안전하다).
            Instance.Initialize();
            return Instance;
        }

        public void Enqueue(MessagePopupRequest request)
        {
            if (request == null)
            {
                return;
            }

            Initialize();

            if (_current != null)
            {
                _queue.Enqueue(request);
                return;
            }

            Show(request);
        }

        void Show(MessagePopupRequest request)
        {
            _current = request;

            if (titleText != null)
            {
                titleText.text = request.title ?? string.Empty;
                // 제목이 비면 줄만 차지하므로 아예 숨긴다.
                titleText.gameObject.SetActive(!string.IsNullOrEmpty(request.title));
            }
            if (messageText != null)
            {
                messageText.text = request.message ?? string.Empty;
            }
            if (confirmLabel != null)
            {
                confirmLabel.text = request.confirmText;
            }
            if (cancelLabel != null)
            {
                cancelLabel.text = request.cancelText;
            }
            if (cancelButton != null)
            {
                cancelButton.gameObject.SetActive(request.showCancel);
            }

            SetVisible(true);

            // 팝업이 떠 있는 동안은 대화창과 같은 잠금으로 플레이어 조작을 막는다. 큐가
            // 이어지는 동안은 계속 잠근 상태를 유지하고, 마지막 팝업이 닫힐 때 한 번만 푼다.
            if (!_inputBlocked)
            {
                InputBlocker.Block();
                _inputBlocked = true;
            }
        }

        void OnClickBackground()
        {
            if (_current == null || !_current.closeOnBackground)
            {
                return;
            }
            Close(false);
        }

        void Close(bool confirmed)
        {
            if (_current == null)
            {
                return;
            }

            var closing = _current;
            _current = null;

            // 콜백이 또 팝업을 띄울 수 있으므로(연속 안내 등) _current를 먼저 비워둔다.
            if (confirmed)
            {
                closing.onConfirm?.Invoke();
            }
            else
            {
                closing.onCancel?.Invoke();
            }
            closing.onClosed?.Invoke();

            if (_current != null)
            {
                // 콜백 안에서 새 팝업이 곧바로 떠버린 경우 — 그대로 둔다.
                return;
            }

            if (_queue.Count > 0)
            {
                Show(_queue.Dequeue());
                return;
            }

            SetVisible(false);
            ReleaseInputBlock();
        }

        void SetVisible(bool visible)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        // 팝업이 떠 있는 채로 씬이 바뀌거나 오브젝트가 꺼지면 잠금이 남아 조작이 영영
        // 막힌다 — EventTrigger와 같은 이유로 여기서도 반드시 풀어준다.
        void OnDisable()
        {
            ReleaseInputBlock();
        }

        void ReleaseInputBlock()
        {
            if (!_inputBlocked)
            {
                return;
            }

            InputBlocker.Unblock();
            _inputBlocked = false;
        }
    }
}
