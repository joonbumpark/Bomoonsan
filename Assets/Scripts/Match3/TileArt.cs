using UnityEngine;

namespace Match3
{
    /// <summary>
    /// 타일/아이템 블록에 쓰는 스프라이트를 이름으로 불러와 캐싱해둔다. 전부 이미 완성된
    /// 색의 이미지라 틴트 없이 그대로 쓴다 - 일반 타일은 Match3GameManager.Palette 색상
    /// 순서에 맞춘 pouch_&lt;색&gt; 이미지, 아이템(폭탄/줄삭제/무지개)은 색 구분 없는
    /// 고유 모양 이미지 하나씩이다.
    /// </summary>
    public static class TileArt
    {
        // Match3GameManager.Palette와 같은 순서(빨강/주황/노랑/초록/파랑/보라)여야 한다.
        private static readonly string[] ColorNames = { "red", "orange", "yellow", "green", "blue", "purple" };

        private static Sprite baseSprite;
        private static Sprite bombSprite;
        private static Sprite rowClearSprite;
        private static Sprite colClearSprite;
        private static Sprite rainbowSprite;
        private static readonly Sprite[] coloredSprites = new Sprite[ColorNames.Length];

        /// <summary>일반 타일(복주머니 기본형). Whack/Simon 등 다른 미니게임이 공용으로도 쓴다.</summary>
        public static Sprite Base => baseSprite != null ? baseSprite : (baseSprite = Load("pouch_base"));

        /// <summary>색상 인덱스(0=빨강..5=보라)에 맞는, 이미 색이 입혀진 일반 타일 스프라이트.</summary>
        public static Sprite NormalFor(int colorType)
        {
            if (colorType < 0 || colorType >= ColorNames.Length)
                return Base;

            return coloredSprites[colorType] != null
                ? coloredSprites[colorType]
                : (coloredSprites[colorType] = Load("pouch_" + ColorNames[colorType]));
        }

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
