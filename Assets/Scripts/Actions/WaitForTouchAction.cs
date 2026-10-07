using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mountains
{
    // 화면에 "터치하세요" 안내를 띄우고, 플레이어가 화면을 터치(클릭)해야만 다음 스텝으로
    // 넘어가는 액션. 대화·연출을 한 박자 쉬었다 가고 싶을 때, 또는 플레이어가 준비됐다는
    // 신호를 받아야 하는 지점에 쓴다.
    //
    // 안내 UI는 UiOverlayRoot의 오버레이 캔버스에 만들었다가 끝나면 지운다. 전체 화면을 덮는
    // 투명 Image가 뒤쪽 UI(조이스틱, 옵션 버튼)의 터치를 먼저 받아가므로, 이 액션이 도는
    // 동안 터치가 다른 UI로 새지 않는다.
    //
    // 입력은 이벤트가 아니라 매 프레임 "이번 프레임에 눌렸는가"로 읽는다 — EventSystem이나
    // 오버레이 레이캐스트 상태와 무관하게 동작하고, 안내를 숨겨도(showPrompt 끔) 똑같이 동작한다.
    [Serializable]
    public class WaitForTouchAction : TriggerAction
    {
        [Header("안내")]
        [Tooltip("끄면 안내 UI 없이 보이지 않는 채로 터치만 기다린다.")]
        public bool showPrompt = true;
        [TextArea(1, 3)] public string message = "화면을 터치하세요";
        [Min(1f)] public float fontSize = 56f;
        [Tooltip("안내 문구의 세로 위치. 0 = 화면 맨 아래, 1 = 맨 위.")]
        [Range(0f, 1f)] public float promptHeight = 0.25f;
        [Tooltip("켜두면 안내가 깜빡인다.")]
        public bool blink = true;
        [Tooltip("글자가 잘 보이도록 화면을 살짝 어둡게 깐다. 0이면 그대로 둔다.")]
        [Range(0f, 1f)] public float dimAlpha = 0.3f;

        [Header("입력")]
        [Tooltip("안내가 뜬 뒤 이 시간(초) 동안은 터치를 무시한다. 직전 대화를 넘기던 손가락이 " +
            "그대로 이 액션까지 넘겨버리는 것을 막는다.")]
        [Min(0f)] public float ignoreInputSeconds = 0.3f;
        [Tooltip("켜두면 기다리는 동안 플레이어 조작(조이스틱 이동/드래그 회전)을 막는다. " +
            "EventTrigger의 blockInputDuringEvent가 이미 켜져 있어도 겹쳐 잠그는 것이라 안전하다.")]
        public bool blockPlayerInput = true;

        public override async UniTask ExecuteAsync(TriggerContext context, CancellationToken cancellationToken)
        {
            GameObject prompt = showPrompt ? BuildPrompt() : null;

            if (blockPlayerInput)
            {
                InputBlocker.Block();
            }

            try
            {
                // unscaledTime: 일시정지(timeScale 0) 중에도 무시 구간이 정상적으로 흘러야 한다.
                float readyTime = Time.unscaledTime + ignoreInputSeconds;

                while (true)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

                    if (Time.unscaledTime < readyTime)
                    {
                        continue;
                    }

                    if (TouchedThisFrame())
                    {
                        return;
                    }
                }
            }
            finally
            {
                // 취소(트리거 파괴)나 예외로 끝나도 안내와 잠금이 남지 않게 여기서 정리한다.
                if (blockPlayerInput)
                {
                    InputBlocker.Unblock();
                }

                if (prompt != null)
                {
                    UnityEngine.Object.Destroy(prompt);
                }
            }
        }

        // 이번 프레임에 터치/클릭이 "새로" 시작됐는지. 이미 누르고 있던 손가락은 세지 않는다.
        // 이 프로젝트는 Input Handling이 Both라 두 경로가 다 켜져 있다 — 하나만 켜도
        // 컴파일되도록 정의 심볼로 갈라둔다.
        static bool TouchedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                return true;
            }

            var touchscreen = UnityEngine.InputSystem.Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0))
            {
                return true;
            }

            for (int i = 0; i < Input.touchCount; i++)
            {
                // InputSystem에도 TouchPhase가 있어서 이름을 풀어 쓴다.
                if (Input.GetTouch(i).phase == UnityEngine.TouchPhase.Began)
                {
                    return true;
                }
            }
#endif
            return false;
        }

        GameObject BuildPrompt()
        {
            var parent = UiOverlayRoot.GetOrCreate();

            // 투명(또는 반투명) 전체 화면 Image. raycastTarget을 켜서 뒤쪽 UI가 터치를 못 받게 한다.
            var cover = UiOverlayRoot.CreateImage("TouchPrompt", parent, new Color(0f, 0f, 0f, dimAlpha));
            cover.raycastTarget = true;
            // 알파가 0이어도 레이캐스트는 받아야 하므로 투명 메시 컬링을 끈다.
            cover.canvasRenderer.cullTransparentMesh = false;
            UiOverlayRoot.Stretch(cover.rectTransform);

            var text = UiOverlayRoot.CreateText("Message", cover.transform, fontSize,
                TextAlignmentOptions.Center, UiOverlayRoot.ResolveFont());
            text.text = message;

            // 가로는 화면 폭에 맞추고(양옆 5% 여백), 세로는 promptHeight 위치에 고정한다.
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.05f, promptHeight);
            rect.anchorMax = new Vector2(0.95f, promptHeight);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 200f);

            if (blink)
            {
                // UiBlink가 CanvasGroup을 요구해서 함께 붙는다. 글자만 깜빡이고 배경은 그대로다.
                text.gameObject.AddComponent<UiBlink>();
            }

            return cover.gameObject;
        }
    }
}
