using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Mountains
{
    // EventTrigger 안에서 실행되는 액션 하나. MonoBehaviour가 아니라 [SerializeReference]로
    // 직렬화되는 순수 클래스다 — 액션을 추가할 때마다 자식 GameObject를 만들고 컴포넌트를
    // 붙이지 않아도, EventTrigger 인스펙터의 목록에서 바로 추가할 수 있다.
    //
    // 완료 통보가 콜백(Action onComplete)에서 UniTask로 바뀐 이유: 콜백 방식은 액션마다
    // "어떤 경로로 끝나든 정확히 한 번" 부르는 책임을 손으로 지켜야 했고(한 번이라도
    // 빠뜨리면 시퀀스가 영원히 멈춘다), 순차 실행을 쓰려면 코루틴을 따로 열어야 했다.
    // await는 그 두 가지를 언어가 보장해준다.
    [Serializable]
    public abstract class TriggerAction
    {
        // cancellationToken은 트리거가 파괴되거나 시퀀스가 중단될 때 발동한다. 기다리는
        // 동안 대상이 사라질 수 있는 액션은 이 토큰을 그대로 넘겨주면 된다.
        public abstract UniTask ExecuteAsync(TriggerContext context, CancellationToken cancellationToken);
    }
}
