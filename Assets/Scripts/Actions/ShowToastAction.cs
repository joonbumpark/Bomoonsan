using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mountains
{
    // 토스트를 띄우는 액션. 흐름을 끊지 않는 알림이라 기본은 곧바로 완료 처리하고,
    // 연출 순서상 다 보여준 뒤 넘어가야 할 때만 기다린다.
    [Serializable]
    public class ShowToastAction : TriggerAction
    {
        [TextArea(1, 4)] public string message;
        [Tooltip("0 이하면 ToastUI의 기본 표시 시간을 쓴다.")]
        public float duration = 0f;
        [Tooltip("켜두면 표시 시간이 끝날 때까지 다음 스텝으로 넘어가지 않는다.")]
        public bool waitForDuration;

        public override UniTask ExecuteAsync(TriggerContext context, CancellationToken cancellationToken)
        {
            Toast.Show(message, duration);

            if (!waitForDuration)
            {
                return UniTask.CompletedTask;
            }

            float wait = duration > 0f ? duration : ToastUI.GetOrCreate().defaultDuration;
            return UniTask.Delay(TimeSpan.FromSeconds(wait), cancellationToken: cancellationToken);
        }
    }
}
