using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mountains
{
    // 대화 한 편을 재생하는 액션. dialogData가 비어 있으면 DialogUI.Play가 경고만 찍고
    // 즉시 완료 처리하므로, 대화 없는 액션 그룹에도 그대로 쓸 수 있다.
    [Serializable]
    public class PlayDialogAction : TriggerAction
    {
        public DialogData dialogData;

        public override UniTask ExecuteAsync(TriggerContext context, CancellationToken cancellationToken)
        {
            if (DialogUI.Instance == null)
            {
                Debug.LogWarning("[PlayDialogAction] DialogUI.Instance가 없어 대화를 건너뜁니다.");
                return UniTask.CompletedTask;
            }

            // DialogUI는 아직 콜백 방식이라 완료 신호를 UniTask로 옮겨 담는다.
            return CallbackTask.Run(done => DialogUI.Instance.Play(dialogData, done), cancellationToken);
        }
    }
}
