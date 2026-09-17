using System;
using UnityEngine;

namespace Mountains
{
    // 대화 한 편을 재생하는 액션. dialogData가 비어 있으면 DialogUI.Play가 경고만 찍고
    // 즉시 완료 처리하므로(DialogUI.cs 기존 동작), 대화 없는 액션 그룹에도 그대로 쓸 수 있다.
    public class PlayDialogAction : TriggerAction
    {
        public DialogData dialogData;

        public override void Execute(Action onComplete)
        {
            if (DialogUI.Instance == null)
            {
                Debug.LogWarning("[PlayDialogAction] DialogUI.Instance가 없어 대화를 건너뜁니다.");
                onComplete?.Invoke();
                return;
            }

            DialogUI.Instance.Play(dialogData, () => onComplete?.Invoke());
        }
    }
}
