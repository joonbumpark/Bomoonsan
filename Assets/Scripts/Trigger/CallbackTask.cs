using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Mountains
{
    // "콜백으로 끝을 알려주는 기존 API"를 await할 수 있게 감싼다.
    //
    // DialogUI.Play, NpcDespawner.Despawn, NpcTweenUtility.Play처럼 UniTask 이전부터 있던
    // 코드는 완료를 Action으로 돌려준다. 액션마다 UniTaskCompletionSource를 직접 만들어
    // 쓰다 보면 취소 연결(AttachExternalCancellation)을 빠뜨리기 쉬운데, 그러면 콜백이
    // 영영 오지 않는 상황에서 시퀀스가 멈춘 채 남는다 — 그 조합을 한 곳에 고정한다.
    public static class CallbackTask
    {
        public static UniTask Run(Action<Action> begin, CancellationToken cancellationToken)
        {
            var completion = new UniTaskCompletionSource();
            begin(() => completion.TrySetResult());
            return completion.Task.AttachExternalCancellation(cancellationToken);
        }
    }
}
