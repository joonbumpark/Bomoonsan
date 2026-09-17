using UnityEngine;

namespace Mountains
{
    // 이벤트/대화/강제 이동처럼 "지금은 플레이어 조작을 막아야 하는" 상황을 한 곳에서
    // 관리한다. 조작을 읽는 쪽(CharacterMovement, TouchRotateInput)은 IsBlocked만 보고,
    // 막아야 하는 쪽은 Block()/Unblock()을 쌍으로 호출한다.
    //
    // bool이 아니라 참조 카운트인 이유: EventTrigger의 한 스텝 안에서 액션들이 병렬로
    // 돌고 그 안에서 대화까지 재생될 수 있어서, 겹쳐서 잠그는 경우가 정상적으로 생긴다.
    // bool이면 먼저 끝난 쪽이 잠금을 풀어버려 아직 진행 중인 연출이 조작에 방해받는다.
    public static class InputBlocker
    {
        static int _blockCount;

        public static bool IsBlocked => _blockCount > 0;

        public static void Block()
        {
            _blockCount++;
        }

        public static void Unblock()
        {
            _blockCount = Mathf.Max(0, _blockCount - 1);
        }

        // 이 프로젝트는 Enter Play Mode Options에서 도메인 리로드를 꺼둬서 static 값이
        // Play 세션 사이에 그대로 남는다 — 이벤트 도중에 Play를 멈추면 카운트가 0으로
        // 안 돌아가서 다음 Play 때 조작이 영영 막히는 상태로 시작한다. 씬 로드 전에
        // 무조건 초기화해서 그 상황을 원천 차단한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            _blockCount = 0;
        }
    }
}
