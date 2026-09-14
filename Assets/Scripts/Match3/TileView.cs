using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// 보드 위의 타일 하나를 표현하는 뷰. 터치/드래그 입력을 받아 스와이프 방향을 계산해
    /// Match3GameManager에 스왑을 요청하고, 색상/위치/선택 표시 등 시각적인 부분을 담당한다.
    /// 매치로 생긴 아이템 블록이면(Item != None) 클릭했을 때도 발동을 요청한다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class TileView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public int Col { get; private set; }
        public int Row { get; private set; }
        public int Type { get; private set; }
        public ItemType Item { get; private set; } = ItemType.None;

        public Image Image { get; private set; }
        public RectTransform RectTransform { get; private set; }

        private Match3GameManager manager;
        private Vector2 dragStartScreenPos;
        private bool swapTriggered;
        private Text itemBadge;

        public void Init(Match3GameManager owner, int col, int row, int type, Color color)
        {
            manager = owner;
            Image = GetComponent<Image>();
            RectTransform = (RectTransform)transform;

            SetPosition(col, row);
            SetType(type, color);
            SetItem(ItemType.None);
        }

        public void SetPosition(int col, int row)
        {
            Col = col;
            Row = row;
        }

        public void SetType(int type, Color color)
        {
            Type = type;
            Image.color = color;
        }

        /// <summary>이 타일에 아이템 블록을 붙이거나(뗀다). 아이템 종류를 나타내는 기호를 겹쳐 그린다.</summary>
        public void SetItem(ItemType item)
        {
            Item = item;

            if (item == ItemType.None)
            {
                if (itemBadge != null)
                    itemBadge.text = string.Empty;
                return;
            }

            EnsureItemBadge();
            itemBadge.text = SymbolFor(item);
        }

        private void EnsureItemBadge()
        {
            if (itemBadge != null)
                return;

            // UIFactory.CreateText를 써서 한글 폰트(나눔고딕)/기본 폰트 대체 로직을 그대로 재사용한다.
            itemBadge = UIFactory.CreateText("ItemBadge", transform, string.Empty, 48, TextAnchor.MiddleCenter);
            itemBadge.fontStyle = FontStyle.Bold;
            itemBadge.raycastTarget = false; // 클릭/드래그 입력은 아래 타일 이미지가 받도록 한다.
            UIFactory.StretchFull(itemBadge.rectTransform);
        }

        // 폰트에 없을 수 있는 화살표/장식 기호 대신, 어떤 폰트에도 확실히 존재하는 알파벳 한 글자로 표시한다.
        private static string SymbolFor(ItemType item)
        {
            switch (item)
            {
                case ItemType.LineHorizontal: return "H"; // 가로 한 줄 지우기
                case ItemType.LineVertical: return "V"; // 세로 한 줄 지우기
                case ItemType.ColorBomb: return "C"; // 같은 색상 전체 지우기
                case ItemType.AreaBomb: return "B"; // 3x3 폭탄
                default: return string.Empty;
            }
        }

        public void SetSelected(bool selected)
        {
            RectTransform.localScale = selected ? Vector3.one * 1.12f : Vector3.one;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragStartScreenPos = eventData.position;
            swapTriggered = false;
            SetSelected(true);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (swapTriggered || manager == null)
                return;

            Vector2 delta = eventData.position - dragStartScreenPos;
            if (delta.magnitude < manager.swipeThreshold)
                return;

            // 델타가 더 큰 축을 기준으로 4방향(상/하/좌/우) 중 하나를 고른다.
            Vector2Int direction = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                ? (delta.x > 0 ? Vector2Int.right : Vector2Int.left)
                : (delta.y > 0 ? Vector2Int.up : Vector2Int.down);

            swapTriggered = true;
            manager.RequestSwap(this, direction);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            swapTriggered = false;
            SetSelected(false);
        }

        /// <summary>드래그로 인식되지 않은 짧은 탭. 아이템 블록이면 그 자리에서 바로 발동을 요청한다.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (manager == null)
                return;

            manager.RequestActivateItem(this);
        }
    }
}
