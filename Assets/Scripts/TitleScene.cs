
using UnityEngine;

namespace Mountains
{
    // 타이틀의 버튼(OnClick)에 연결해서 게임 씬으로 넘어간다. 화면 아무 곳이나 눌러
    // 시작하는 흐름은 PrologueScreen이 담당한다.
    public class TitleScene : MonoBehaviour
    {
        [Tooltip("이동할 게임 씬. File > Build Settings 목록에 있어야 한다.")]
        public string gameSceneName = "Mountain 5";

        public void OnClickTitle()
        {
            SceneFlow.Load(gameSceneName);
        }
    }
}