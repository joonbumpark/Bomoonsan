using System;

namespace Mountains
{
    // 어디서든 한 줄로 팝업을 띄우는 정적 파사드. 호출하는 쪽이 UI 인스턴스를 찾거나
    // 참조를 들고 있을 필요가 없게 한다(DialogUI.Instance를 직접 쓰던 방식보다 부르기 쉽다).
    public static class MessagePopup
    {
        // 확인 버튼 하나만 있는 알림.
        public static void Show(string message, Action onConfirm = null)
        {
            Show(null, message, onConfirm);
        }

        public static void Show(string title, string message, Action onConfirm = null)
        {
            Open(new MessagePopupRequest
            {
                title = title,
                message = message,
                showCancel = false,
                onConfirm = onConfirm
            });
        }

        // 확인/취소 두 버튼. 배경 탭은 취소로 처리한다.
        public static void Confirm(string message, Action onConfirm, Action onCancel = null)
        {
            Confirm(null, message, onConfirm, onCancel);
        }

        public static void Confirm(string title, string message, Action onConfirm, Action onCancel = null)
        {
            Open(new MessagePopupRequest
            {
                title = title,
                message = message,
                showCancel = true,
                onConfirm = onConfirm,
                onCancel = onCancel
            });
        }

        public static void Open(MessagePopupRequest request)
        {
            MessagePopupUI.GetOrCreate().Enqueue(request);
        }

        public static bool IsShowing
        {
            get
            {
                var popup = MessagePopupUI.Instance;
                return popup != null && popup.IsShowing;
            }
        }
    }
}
