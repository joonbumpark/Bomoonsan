using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Mountains
{
    // 타이틀 화면 계층을 한 번에 만들어준다. 손으로 만들면 빠뜨리기 쉬운 것들이 있어서
    // 도구로 둔다 — 프롤로그 패널을 타이틀보다 뒤(위에 그려지는 쪽)에 두는 순서,
    // 레터박스를 가리는 검정 배경, CanvasGroup 초기 상태, PrologueScreen의 참조 연결.
    static class TitleCanvasSetup
    {
        [MenuItem("Mountains/타이틀 캔버스 생성")]
        static void CreateTitleCanvas()
        {
            var existing = Object.FindFirstObjectByType<PrologueScreen>(FindObjectsInactive.Include);
            if (existing != null)
            {
                EditorUtility.DisplayDialog("타이틀 캔버스 생성",
                    "이 씬에 이미 PrologueScreen이 있습니다. 새로 만들려면 먼저 지워주세요.", "확인");
                Selection.activeObject = existing.gameObject;
                return;
            }

            // 폰트 해석은 런타임/에디터 공통 규칙을 그대로 쓴다(에디터에서는 프로젝트를 뒤진다).
            var font = UiOverlayRoot.ResolveFont();

            var canvas = CreateCanvas();
            var titleRoot = UiOverlayRoot.CreateRect("TitleRoot", canvas.transform);
            UiOverlayRoot.Stretch(titleRoot);
            BuildTitle(titleRoot, font);

            // ProloguePanel은 TitleRoot 다음에 만든다 — UGUI는 나중 형제가 위에 그려지므로
            // 순서가 뒤바뀌면 프롤로그가 타이틀 아트에 가려진다.
            var panel = UiOverlayRoot.CreateRect("ProloguePanel", canvas.transform);
            UiOverlayRoot.Stretch(panel);
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            // 검정 배경: Preserve Aspect로 비율을 지키면 남는 여백으로 타이틀이 비쳐 보인다.
            var backdrop = UiOverlayRoot.CreateImage("Backdrop", panel, Color.black);
            UiOverlayRoot.Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = false;

            var prologueImage = UiOverlayRoot.CreateImage("PrologueImage", panel, Color.white);
            UiOverlayRoot.Stretch(prologueImage.rectTransform);
            prologueImage.preserveAspect = true;
            prologueImage.raycastTarget = false;
            prologueImage.enabled = false;

            var screenGo = new GameObject("PrologueScreen");
            Undo.RegisterCreatedObjectUndo(screenGo, "Create Title Canvas");
            var screen = screenGo.AddComponent<PrologueScreen>();
            screen.imageTarget = prologueImage;
            screen.canvasGroup = group;

            EnsureEventSystem();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeObject = screenGo;

            Debug.Log("[TitleCanvasSetup] 타이틀 캔버스를 만들었습니다.\n" +
                "  PrologueScreen의 data에 PrologueData 에셋을, nextSceneName에 게임 씬 이름을 지정하세요.\n" +
                "  TitleRoot/Background와 Logo에는 타이틀 아트를 넣으면 됩니다.");
        }

        static Canvas CreateCanvas()
        {
            var go = new GameObject("TitleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(go, "Create Title Canvas");

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 팝업/토스트가 런타임에 만드는 오버레이 캔버스가 500이라, 기본값 0이면
            // 그 위로 정상적으로 뜬다.
            canvas.sortingOrder = 0;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        static void BuildTitle(RectTransform parent, TMP_FontAsset font)
        {
            var background = UiOverlayRoot.CreateImage("Background", parent, new Color(0.08f, 0.1f, 0.13f, 1f));
            UiOverlayRoot.Stretch(background.rectTransform);
            background.raycastTarget = false;

            var logo = UiOverlayRoot.CreateImage("Logo", parent, new Color(1f, 1f, 1f, 0.15f));
            var logoRect = logo.rectTransform;
            logoRect.anchorMin = new Vector2(0.5f, 0.68f);
            logoRect.anchorMax = new Vector2(0.5f, 0.68f);
            logoRect.sizeDelta = new Vector2(760f, 320f);
            logo.raycastTarget = false;

            var touch = UiOverlayRoot.CreateText("TouchToStart", parent, 46f,
                TextAlignmentOptions.Center, font);
            var touchRect = touch.rectTransform;
            touchRect.anchorMin = new Vector2(0.5f, 0.22f);
            touchRect.anchorMax = new Vector2(0.5f, 0.22f);
            touchRect.sizeDelta = new Vector2(800f, 90f);
            touch.text = "터치하여 시작";
            touch.color = Color.white;

            // 입력을 기다리는 안내라 깜빡이게 한다. 색이 아니라 CanvasGroup을 흔들어서
            // 나중에 아이콘 같은 걸 자식으로 붙여도 함께 깜빡인다.
            touch.gameObject.AddComponent<CanvasGroup>();
            touch.gameObject.AddComponent<UiBlink>();
        }


        static void EnsureEventSystem()
        {
            // 만들어진 경우에만 Undo에 등록한다 — 이미 있던 EventSystem을 되돌리면 안 된다.
            var created = UiOverlayRoot.EnsureEventSystem();
            if (created != null)
            {
                Undo.RegisterCreatedObjectUndo(created, "Create Title Canvas");
            }
        }
    }
}
