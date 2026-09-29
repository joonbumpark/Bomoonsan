using System;

namespace Mountains
{
    // 여러 대상의 완료를 세어, 마지막 하나가 끝날 때 딱 한 번 통보한다. EventTrigger는
    // 액션의 완료 콜백을 세서 다음 스텝으로 넘어갈 시점을 정하므로, 이 계산이 틀리면
    // 시퀀스가 너무 일찍 넘어가거나 영원히 멈춘다 — 액션마다 손으로 세지 않고 여기 모았다.
    public class CompletionCounter
    {
        readonly Action _onComplete;
        int _remaining;

        public bool IsDone => _remaining <= 0;

        public CompletionCounter(int total, Action onComplete)
        {
            _onComplete = onComplete;
            _remaining = total;

            // 대상이 하나도 없으면 기다릴 것도 없다.
            if (_remaining <= 0)
            {
                _onComplete?.Invoke();
            }
        }

        // 대상 하나가 끝났음을 알린다. 콜백이 두 번 불려도(트윈이 완료된 뒤 Kill되는 등)
        // 통보가 중복되지 않게 0에서 더 내려가지 않도록 막는다.
        public void Signal()
        {
            if (_remaining <= 0)
            {
                return;
            }

            _remaining--;
            if (_remaining == 0)
            {
                _onComplete?.Invoke();
            }
        }
    }
}
