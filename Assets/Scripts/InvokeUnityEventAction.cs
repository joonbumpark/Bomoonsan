using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Mountains
{
    // 완료 신호가 없는 기존 메서드(예: Reward.GenerateCoin())를 감싸는 범용 어댑터.
    // 이벤트를 즉시 호출하고, completeAfterSeconds가 0이면 그 자리에서, 아니면 그만큼
    // 기다린 뒤 완료 처리한다 — 연출 길이를 대략 아는 기존 동작을 시퀀스에 끼워 넣을 때 쓴다.
    public class InvokeUnityEventAction : TriggerAction
    {
        public UnityEvent unityEvent;
        [Tooltip("0이면 이벤트 호출과 동시에 완료 처리한다.")]
        public float completeAfterSeconds = 0f;

        public override void Execute(Action onComplete)
        {
            unityEvent?.Invoke();

            if (completeAfterSeconds <= 0f)
            {
                onComplete?.Invoke();
                return;
            }

            StartCoroutine(CompleteAfterDelay(completeAfterSeconds, onComplete));
        }

        IEnumerator CompleteAfterDelay(float seconds, Action onComplete)
        {
            yield return new WaitForSeconds(seconds);
            onComplete?.Invoke();
        }
    }
}
