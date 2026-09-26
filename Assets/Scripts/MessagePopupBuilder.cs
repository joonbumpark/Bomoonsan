using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mountains
{
    // 씬에 팝업 UI가 없을 때 쓸 기본 모양을 코드로 만든다. 뷰 로직(MessagePopupUI)과
    // 레이아웃 생성을 나눠둔 이유: 나중에 디자인을 프리팹으로 만들면 이 파일만 안 쓰이게
    // 되고, 뷰 쪽은 그대로 재사용된다.
    public static class MessagePopupBuilder
    {
        // parent를 받는 이유: 에디터에서 프리팹을 만들 때는 런타임 오버레이 캔버스가 아니라
        // 임시 캔버스 밑에 지어서 저장한 뒤 지워야 한다(씬을 더럽히지 않게).
        public static MessagePopupUI Build(Transform root)
        {
            var font = UiOverlayRoot.ResolveFont();

            var panelRect = UiOverlayRoot.CreateRect("MessagePopup", root);
            UiOverlayRoot.Stretch(panelRect);
            var canvasGroup = panelRect.gameObject.AddComponent<CanvasGroup>();

            // 배경: 화면을 덮는 반투명 막 + 버튼(탭해서 닫기). 이게 레이캐스트를 가로채므로
            // 팝업이 떠 있는 동안 아래의 조이스틱/드래그 회전이 같이 반응하지 않는다.
            var background = UiOverlayRoot.CreateImage("Background", panelRect, new Color(0f, 0f, 0f, 0.6f));
            UiOverlayRoot.Stretch(background.rectTransform);
            var backgroundButton = background.gameObject.AddComponent<Button>();
            backgroundButton.targetGraphic = background;
            backgroundButton.transition = Selectable.Transition.None;

            // 본체 박스: 화면 가운데, 가로는 화면의 80%, 높이는 내용에 맞춰 늘어난다.
            var box = UiOverlayRoot.CreateImage("Box", panelRect, new Color(0.11f, 0.12f, 0.15f, 0.98f));
            var boxRect = box.rectTransform;
            boxRect.anchorMin = new Vector2(0.1f, 0.5f);
            boxRect.anchorMax = new Vector2(0.9f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(0f, 0f);

            var boxLayout = box.gameObject.AddComponent<VerticalLayoutGroup>();
            boxLayout.padding = new RectOffset(48, 48, 48, 48);
            boxLayout.spacing = 28f;
            boxLayout.childControlWidth = true;
            boxLayout.childControlHeight = true;
            boxLayout.childForceExpandWidth = true;
            boxLayout.childForceExpandHeight = false;
            boxLayout.childAlignment = TextAnchor.UpperCenter;

            var boxFitter = box.gameObject.AddComponent<ContentSizeFitter>();
            boxFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var titleText = UiOverlayRoot.CreateText("Title", boxRect, 56f, TextAlignmentOptions.Center, font);
            titleText.fontStyle = FontStyles.Bold;

            var messageText = UiOverlayRoot.CreateText("Message", boxRect, 44f, TextAlignmentOptions.Center, font);
            messageText.color = new Color(0.9f, 0.9f, 0.92f, 1f);

            // 버튼 줄: 취소가 꺼지면 확인 하나만 남아 가운데로 늘어난다.
            var buttonRow = UiOverlayRoot.CreateRect("Buttons", boxRect);
            var rowLayout = buttonRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 24f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            var rowElement = buttonRow.gameObject.AddComponent<LayoutElement>();
            rowElement.minHeight = 120f;
            rowElement.preferredHeight = 120f;

            var cancelButton = UiOverlayRoot.CreateButton("CancelButton", buttonRow,
                new Color(0.25f, 0.26f, 0.3f, 1f), 42f, font, out var cancelLabel);
            var confirmButton = UiOverlayRoot.CreateButton("ConfirmButton", buttonRow,
                new Color(0.2f, 0.5f, 0.85f, 1f), 42f, font, out var confirmLabel);

            // 컴포넌트는 참조를 다 만든 뒤에 붙인다 — AddComponent 시점에 Awake가 바로
            // 실행되므로, 먼저 붙이면 참조가 비어 있는 상태로 초기화가 돌아간다.
            var popup = panelRect.gameObject.AddComponent<MessagePopupUI>();
            popup.canvasGroup = canvasGroup;
            popup.titleText = titleText;
            popup.messageText = messageText;
            popup.confirmButton = confirmButton;
            popup.confirmLabel = confirmLabel;
            popup.cancelButton = cancelButton;
            popup.cancelLabel = cancelLabel;
            popup.backgroundButton = backgroundButton;
            popup.Initialize();

            return popup;
        }
    }
}
