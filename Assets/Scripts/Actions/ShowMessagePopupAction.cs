using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Mountains
{
    // 메시지 팝업을 띄우는 액션. 기본값(waitForClose)은 팝업이 닫힐 때까지 다음 스텝으로
    // 넘어가지 않는다 — 안내를 읽기 전에 연출이 진행되면 의미가 없다.
    [Serializable]
    public class ShowMessagePopupAction : TriggerAction
    {
        [Tooltip("비워두면 제목 줄 자체가 숨겨진다.")]
        public string title;
        [TextArea(2, 6)] public string message;

        [Header("버튼")]
        public string confirmText = "확인";
        [Tooltip("켜면 취소 버튼이 함께 나오고, 배경 탭도 취소로 처리된다.")]
        public bool showCancel;
        public string cancelText = "취소";
        [Tooltip("배경을 탭해서 닫을 수 있게 할지. 반드시 버튼을 누르게 하려면 끈다.")]
        public bool closeOnBackground = true;

        [Header("콜백")]
        public UnityEvent onConfirm;
        public UnityEvent onCancel;

        [Tooltip("켜두면 팝업이 닫힐 때까지 다음 스텝으로 넘어가지 않는다.")]
        public bool waitForClose = true;

        public override UniTask ExecuteAsync(TriggerContext context, CancellationToken cancellationToken)
        {
            var request = new MessagePopupRequest
            {
                title = title,
                message = message,
                confirmText = confirmText,
                cancelText = cancelText,
                showCancel = showCancel,
                closeOnBackground = closeOnBackground,
                onConfirm = () => onConfirm?.Invoke(),
                onCancel = () => onCancel?.Invoke()
            };

            if (!waitForClose)
            {
                MessagePopup.Open(request);
                return UniTask.CompletedTask;
            }

            return CallbackTask.Run(done =>
            {
                request.onClosed = done;
                MessagePopup.Open(request);
            }, cancellationToken);
        }
    }
}
