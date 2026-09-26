using UnityEngine;

namespace Mountains
{
    // NavMeshAgent를 직접 몰아 NPC를 움직이는 컴포넌트(NpcFollower, NpcPathWalker 등)를
    // 표시하는 마커. 등장/퇴장 연출 중에는 이들을 잠깐 꺼둬야 한다 — 그러지 않으면
    // 연출이 도는 동안 NPC가 태연히 걸어가 버리고, 컴포넌트를 모르는 채로 NavMeshAgent만
    // 끄면 AI 쪽 Update가 비활성 에이전트에 SetDestination을 불러 에러를 쏟는다.
    //
    // 타입 이름을 직접 나열하는 대신 마커로 둬서, 나중에 새 AI 컴포넌트를 추가해도
    // 이 인터페이스만 붙이면 연출 쪽은 손댈 필요가 없다.
    public interface INpcBrain
    {
    }
}
