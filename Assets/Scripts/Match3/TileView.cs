using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// 보드 위의 타일 하나를 표현하는 뷰. 터치/드래그 입력을 받아 스와이프 방향을 계산해
    /// Match3GameManager에 스왑을 요청하고, 색상/위치/선택 표시 등 시각적인 부분을 담당한다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class TileView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public int Col { get; private set; }
        public int Row { get; private set; }
        public int Type { get; private set; }

        public Image Image { get; private set; }
        public RectTransform RectTransform { get; private set; }

        private Match3GameManager manager;
        private Vector2 dragStartScreenPos;
        private bool swapTriggered;

        public void Init(Match3GameManager owner, int col, int row, int type, Color color)
        {
            manager = owner;
            Image = GetComponent<Image>();
            RectTransform = (RectTransform)transform;

            SetPosition(col, row);
            SetType(type, color);
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
    }
}
