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

        public void Init(Match3GameManager owner, int col, int row, int type, Color color)
        {
            manager = owner;
            Image = GetComponent<Image>();
            Image.preserveAspect = true;
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

        /// <summary>
        /// 이 타일에 아이템 블록을 붙이거나(뗀다). 아이템 종류에 맞는 모양(TileArt)으로
        /// 스프라이트 자체를 바꿔치기한다 - 색상(Image.color)은 그대로 유지되므로
        /// "이 색의 폭탄/줄삭제/무지개" 임을 한눈에 알 수 있다.
        /// </summary>
        public void SetItem(ItemType item)
        {
            Item = item;
            Image.sprite = TileArt.For(item);
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
