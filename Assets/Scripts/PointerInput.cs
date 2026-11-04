using UnityEngine;

namespace Mountains
{
    // 연출 액션들이 쓰는 저수준 포인터(터치/마우스) 읽기. 이벤트가 아니라 매 프레임 상태를 읽는다 —
    // EventSystem이나 UI 레이캐스트와 무관하게 동작해야 화면을 덮는 안내 UI 위에서도 입력을 받을 수 있다.
    //
    // 이 프로젝트는 Input Handling이 Both라 두 경로가 다 켜져 있다. 새 Input System 장치가 있으면
    // 그쪽만 읽고(같은 터치를 두 번 세지 않게), 없을 때만 예전 Input Manager로 내려간다.
    public static class PointerInput
    {
        // 이번 프레임에 "새로" 눌린 손가락/클릭 수. 두 손가락으로 동시에 치면 2가 나온다(연타 카운트용).
        public static int PressesThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var touchscreen = UnityEngine.InputSystem.Touchscreen.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (touchscreen != null || mouse != null)
            {
                int count = 0;
                if (touchscreen != null)
                {
                    foreach (var touch in touchscreen.touches)
                    {
                        if (touch.press.wasPressedThisFrame)
                        {
                            count++;
                        }
                    }
                }

                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    count++;
                }

                return count;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0)
            {
                int began = 0;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    // InputSystem에도 TouchPhase가 있어서 이름을 풀어 쓴다.
                    if (Input.GetTouch(i).phase == UnityEngine.TouchPhase.Began)
                    {
                        began++;
                    }
                }
                return began;
            }

            return Input.GetMouseButtonDown(0) ? 1 : 0;
#else
            return 0;
#endif
        }

        // 이번 프레임에 새로 눌린 지점들의 화면 좌표를 results에 담는다(여러 손가락 동시 터치 포함).
        public static void GetPressPositionsThisFrame(System.Collections.Generic.List<Vector2> results)
        {
            results.Clear();
#if ENABLE_INPUT_SYSTEM
            var touchscreen = UnityEngine.InputSystem.Touchscreen.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (touchscreen != null || mouse != null)
            {
                if (touchscreen != null)
                {
                    foreach (var touch in touchscreen.touches)
                    {
                        if (touch.press.wasPressedThisFrame)
                        {
                            results.Add(touch.position.ReadValue());
                        }
                    }
                }

                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    results.Add(mouse.position.ReadValue());
                }
                return;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var touch = Input.GetTouch(i);
                    if (touch.phase == UnityEngine.TouchPhase.Began)
                    {
                        results.Add(touch.position);
                    }
                }
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                results.Add(Input.mousePosition);
            }
#endif
        }

        // 첫 번째 손가락(없으면 마우스)의 화면 좌표와 눌림 여부. 읽을 장치가 없으면 false.
        public static bool TryGetPrimary(out Vector2 screenPosition, out bool pressed)
        {
#if ENABLE_INPUT_SYSTEM
            var touchscreen = UnityEngine.InputSystem.Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
            {
                screenPosition = touchscreen.primaryTouch.position.ReadValue();
                pressed = true;
                return true;
            }

            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                screenPosition = mouse.position.ReadValue();
                pressed = mouse.leftButton.isPressed;
                return true;
            }

            if (touchscreen != null)
            {
                screenPosition = touchscreen.primaryTouch.position.ReadValue();
                pressed = false;
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                screenPosition = touch.position;
                pressed = touch.phase != UnityEngine.TouchPhase.Ended && touch.phase != UnityEngine.TouchPhase.Canceled;
                return true;
            }

            screenPosition = Input.mousePosition;
            pressed = Input.GetMouseButton(0);
            return true;
#else
            screenPosition = Vector2.zero;
            pressed = false;
            return false;
#endif
        }
    }
}
