using UnityEngine;

namespace Match3
{
    /// <summary>
    /// 타일/아이템 블록에 쓰는 스프라이트를 이름으로 불러와 캐싱해둔다.
    /// 실제 파일은 Assets/Resources/Sprites/Tiles/ 아래에 있고, 흰색 채우기 +
    /// 검은 외곽선의 "색칠 전" 라인아트라서 Image.color로 원하는 색을 입혀 쓴다.
    /// </summary>
    public static class TileArt
    {
        private static Sprite baseSprite;
        private static Sprite bombSprite;
        private static Sprite rowClearSprite;
        private static Sprite colClearSprite;
        private static Sprite rainbowSprite;

        /// <summary>일반 타일(복주머니 기본형).</summary>
        public static Sprite Base => baseSprite != null ? baseSprite : (baseSprite = Load("pouch_base"));

        /// <summary>3x3 범위를 지우는 폭탄 아이템.</summary>
        public static Sprite Bomb => bombSprite != null ? bombSprite : (bombSprite = Load("item_bomb"));

        /// <summary>가로 한 줄을 지우는 아이템.</summary>
        public static Sprite RowClear => rowClearSprite != null ? rowClearSprite : (rowClearSprite = Load("item_row_clear"));

        /// <summary>세로 한 줄을 지우는 아이템.</summary>
        public static Sprite ColClear => colClearSprite != null ? colClearSprite : (colClearSprite = Load("item_col_clear"));

        /// <summary>같은 색 전체를 지우는 무지개(컬러 폭탄) 아이템.</summary>
        public static Sprite Rainbow => rainbowSprite != null ? rainbowSprite : (rainbowSprite = Load("item_rainbow"));

        /// <summary>아이템 종류에 맞는 스프라이트를 반환한다. None이면 기본 타일 스프라이트.</summary>
        public static Sprite For(ItemType item)
        {
            switch (item)
            {
                case ItemType.AreaBomb: return Bomb;
                case ItemType.LineHorizontal: return RowClear;
                case ItemType.LineVertical: return ColClear;
                case ItemType.ColorBomb: return Rainbow;
                default: return Base;
            }
        }

        private static Sprite Load(string name) => Resources.Load<Sprite>("Sprites/Tiles/" + name);
    }
}
