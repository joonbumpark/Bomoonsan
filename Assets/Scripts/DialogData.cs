using UnityEngine;

namespace Mountains
{
    // 대화 한 줄. 화자가 바뀌는 대화도 표현할 수 있도록 이름/초상화를 줄마다 따로 둔다.
    [System.Serializable]
    public class DialogLine
    {
        public string speakerName;
        public Sprite portrait;
        [TextArea(2, 5)] public string text;
    }

    // 대화 하나(여러 줄)를 담는 데이터 에셋. DialogUI.Play(data)로 재생한다.
    [CreateAssetMenu(menuName = "Mountains/Dialog Data")]
    public class DialogData : ScriptableObject
    {
        public DialogLine[] lines;
    }
}
