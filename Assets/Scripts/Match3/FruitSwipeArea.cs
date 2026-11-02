using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// 과일 썰기 판 전체를 덮는 투명한 입력 영역. 손가락(마우스)이 눌린 채 움직일 때마다
    /// 직전 위치 -> 현재 위치 구간을 판(boardRoot) 로컬 좌표로 바꿔 Swiped로 알려준다 -
    /// 실제로 어떤 과일이 잘렸는지는 FruitNinjaGameManager가 이 구간으로 판정한다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class FruitSwipeArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        /// <summary>(구간 시작, 구간 끝) - 판 로컬 좌표.</summary>
        public event Action<Vector2, Vector2> Swiped;
        /// <summary>손을 뗐을 때 - 한 번의 스와이프(콤보 집계 단위)가 끝났다는 뜻.</summary>
        public event Action SwipeEnded;

        private RectTransform board;
        private bool pressed;
        private Vector2 lastLocal;

        public void Init(RectTransform boardRoot) => board = boardRoot;

        public void OnPointerDown(PointerEventData eventData)
        {
            pressed = ToLocal(eventData, out lastLocal);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!pressed || !ToLocal(eventData, out var current))
                return;

            Swiped?.Invoke(lastLocal, current);
            lastLocal = current;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!pressed)
                return;

            pressed = false;
            SwipeEnded?.Invoke();
        }

        /// <summary>라운드가 끝나거나 화면이 숨겨질 때 눌림 상태를 버린다.</summary>
        public void ResetSwipe() => pressed = false;

        private bool ToLocal(PointerEventData eventData, out Vector2 local) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(board, eventData.position, eventData.pressEventCamera, out local);
    }
}
