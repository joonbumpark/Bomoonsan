using System;

namespace Mountains
{
    // 액션이 참조하는 씬 오브젝트 중 "씬 뷰에 표시할 것"만 고르는 표식. Transform /
    // Transform[] / GameObject / GameObject[] 필드에 붙이면 EventTrigger가 그 위치에
    // 액션 이름을 그려준다.
    //
    // 필드 단위로 옵트인하는 이유: 액션이 참조하는 Transform을 전부 그리면 플레이어 참조나
    // 트윈 대상까지 딸려 나와 씬 뷰가 금세 지저분해진다. 스폰 지점이나 이동 목적지처럼
    // "거기에 무엇이 일어나는지 눈으로 봐야 하는" 것만 표시한다.
    [AttributeUsage(AttributeTargets.Field)]
    public class GizmoTargetAttribute : Attribute
    {
        // 비워두면 "액션이름.필드이름"으로 표시한다.
        public string Label { get; }

        public GizmoTargetAttribute(string label = null)
        {
            Label = label;
        }
    }
}
