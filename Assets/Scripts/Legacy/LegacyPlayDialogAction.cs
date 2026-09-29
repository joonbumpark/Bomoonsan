using System;
using UnityEngine;

namespace Mountains
{
    // 대화 한 편을 재생하는 액션. dialogData가 비어 있으면 DialogUI.Play가 경고만 찍고
    // 즉시 완료 처리하므로(DialogUI.cs 기존 동작), 대화 없는 액션 그룹에도 그대로 쓸 수 있다.
    // 새로 붙이지 못하게 Add Component 메뉴에서 감춘다 — 이미 붙어 있는
    // 컴포넌트는 그대로 동작하므로 마이그레이션에는 영향이 없다.
    [AddComponentMenu("")]
    public class LegacyPlayDialogAction : LegacyTriggerAction
    {
        public DialogData dialogData;

        public override void Execute(Action onComplete)
        {
            if (DialogUI.Instance == null)
            {
                Debug.LogWarning("[LegacyPlayDialogAction] DialogUI.Instance가 없어 대화를 건너뜁니다.");
                onComplete?.Invoke();
                return;
            }

            DialogUI.Instance.Play(dialogData, () => onComplete?.Invoke());
        }
    }
}
