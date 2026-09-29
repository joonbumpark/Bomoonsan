using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace Mountains
{
    // DOTween 트윈을 await할 수 있게 해주는 확장.
    //
    // UniTask에 DOTween 연동(ToUniTask)이 있지만 완료 기준을 직접 정하려고 따로 둔다:
    // 여기서는 OnComplete가 아니라 OnKill을 기다린다. 연출 도중 대상이 파괴되면 트윈은
    // 완료가 아니라 Kill로 끝나는데, OnComplete만 걸어두면 그 await가 영원히 깨어나지
    // 않아 시퀀스가 멈춘다(콜백 시절에도 같은 이유로 OnKill을 썼다).
    public static class TweenAwaitExtensions
    {
        public static UniTask AwaitKill(this Tween tween, CancellationToken cancellationToken)
        {
            if (tween == null || !tween.IsActive())
            {
                return UniTask.CompletedTask;
            }

            var completion = new UniTaskCompletionSource();

            tween.OnKill(() => completion.TrySetResult());

            // 트리거가 파괴되면 트윈도 정리하고 기다림을 끝낸다 — 죽은 오브젝트를 붙잡고
            // 계속 도는 트윈이 남지 않게 한다.
            var registration = cancellationToken.Register(() =>
            {
                if (tween.IsActive())
                {
                    tween.Kill();
                }
                completion.TrySetResult();
            });

            return AwaitAndDispose(completion, registration);
        }

        static async UniTask AwaitAndDispose(UniTaskCompletionSource completion,
            CancellationTokenRegistration registration)
        {
            try
            {
                await completion.Task;
            }
            finally
            {
                registration.Dispose();
            }
        }
    }
}
