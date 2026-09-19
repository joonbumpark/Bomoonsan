using UnityEngine;

namespace Mountains
{
    // NPC 모델의 "정상 상태"(원래 스케일/로컬 위치)를 한 번만 기억해두는 런타임 캐시.
    //
    // 연출 때마다 현재 값을 정상 상태로 간주하면, 퇴장 연출로 스케일이 0이 된 NPC를
    // 비활성화해뒀다가 나중에 등장 연출로 되살릴 때 0을 "원래 크기"로 잡아 영영 안 보이게
    // 된다. 첫 연출 직전의 값만 붙잡아두면 몇 번을 오가도 같은 값으로 돌아온다.
    public class NpcVisualState : MonoBehaviour
    {
        public Vector3 shownScale = Vector3.one;
        public Vector3 shownLocalPosition;

        bool _captured;

        public static NpcVisualState CaptureOrGet(Transform visual)
        {
            var state = visual.GetComponent<NpcVisualState>();
            if (state == null)
            {
                state = visual.gameObject.AddComponent<NpcVisualState>();
            }

            if (!state._captured)
            {
                state.shownScale = visual.localScale;
                state.shownLocalPosition = visual.localPosition;
                state._captured = true;
            }

            return state;
        }
    }
}
