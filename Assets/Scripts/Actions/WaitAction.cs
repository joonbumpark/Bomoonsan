using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mountains
{
    // 순수 딜레이용 액션. 다른 액션과 같은 스텝(병렬)에 넣어서 "적어도 N초는 지나야 다음
    // 스텝으로 못 넘어간다" 같은 최소 대기 시간을 강제할 때 쓴다.
    [Serializable]
    public class WaitAction : TriggerAction
    {
        public float seconds = 1f;

        public override UniTask ExecuteAsync(TriggerContext context, CancellationToken cancellationToken)
        {
            if (seconds <= 0f)
            {
                return UniTask.CompletedTask;
            }

            return UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: cancellationToken);
        }
    }
}
