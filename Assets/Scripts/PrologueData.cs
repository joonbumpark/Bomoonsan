using UnityEngine;

namespace Mountains
{
    // 프롤로그로 차례차례 보여줄 이미지들. DialogData와 같은 이유로 에셋에 담는다 —
    // 연출 순서를 바꾸거나 장면을 추가할 때 씬이나 코드를 건드리지 않아도 된다.
    [CreateAssetMenu(menuName = "Mountains/Prologue Data")]
    public class PrologueData : ScriptableObject
    {
        [Tooltip("보여줄 순서대로. 클릭할 때마다 다음으로 넘어간다.")]
        public Sprite[] images;

        [Tooltip("이미지가 바뀔 때 페이드에 걸리는 시간(초). 0이면 즉시 바뀐다.")]
        [Min(0f)] public float fadeDuration = 0.3f;
    }
}
