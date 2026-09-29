using UnityEngine;

namespace Mountains
{
    // 대화 한 줄. 화자 정보(이름/초상화/대화창 색상)는 CharacterData 에셋 하나로 묶어서
    // 재사용한다 — 화자가 바뀌는 대화는 줄마다 다른 CharacterData를 참조하면 된다.
    [System.Serializable]
    public class DialogLine
    {
        public CharacterData character;
        [TextArea(2, 5)] public string text;
    }

    // 대화 하나(여러 줄)를 담는 데이터 에셋. DialogUI.Play(data)로 재생한다.
    [CreateAssetMenu(menuName = "Mountains/Dialog Data")]
    public class DialogData : ScriptableObject
    {
        public DialogLine[] lines;
    }
}
