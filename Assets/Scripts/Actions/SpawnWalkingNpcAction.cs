using System;

namespace Mountains
{
    // 경로를 따라 걷는 NPC를 스폰하되, 플레이어와 무관하게 스폰 즉시 출발한다.
    // 순찰병, 길을 오가는 행인처럼 "플레이어가 보든 말든 움직이는" 배경 NPC용이다.
    //
    // SpawnPathWalkerNpcAction과 로직이 같고 출발 조건만 다르므로 상속해서 기본값만 뒤집는다
    // — 경로 추적/도착 판정 코드를 두 벌로 만들지 않으려는 것이고, 드롭다운에는 별도 항목으로
    // 보여서 무엇을 고르는지 헷갈리지 않게 한다.
    [Serializable]
    public class SpawnWalkingNpcAction : SpawnPathWalkerNpcAction
    {
        public SpawnWalkingNpcAction()
        {
            waitForPlayer = false;
        }
    }
}
