using UnityEngine;

namespace Match3
{
    /// <summary>
    /// 복주머니 컨셉 UI 프레임(팝업 배경, 버튼)을 이름으로 불러와 캐싱해둔다.
    /// 실제 파일은 Assets/Resources/Sprites/UI/ 아래에 있다.
    /// </summary>
    public static class UIArt
    {
        private static Sprite popupPanel;
        private static Sprite button;

        /// <summary>모서리에 매듭/태슬 장식이 있는 팝업 배경 프레임. 9-slice로 임포트되어 있다.</summary>
        public static Sprite PopupPanel => popupPanel != null ? popupPanel : (popupPanel = Load("popup_panel"));

        /// <summary>위쪽 중앙에 매듭/태슬 장식이 있는 알약 모양 버튼 프레임.</summary>
        public static Sprite Button => button != null ? button : (button = Load("button"));

        private static Sprite Load(string name) => Resources.Load<Sprite>("Sprites/UI/" + name);
    }
}
