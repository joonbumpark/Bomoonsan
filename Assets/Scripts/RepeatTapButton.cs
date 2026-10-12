using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mountains
{
    // 숨은 버튼용: 같은 자리를 requiredTaps번 연속으로 눌러야 Button.onClick이 실행된다.
    // 탭 사이가 maxIntervalSeconds보다 벌어지면 처음부터 다시 센다.
    //
    // Hud의 "Hidden"(좌측 상단 투명 버튼 - 누르면 미니게임 씬으로 바로 이동)에 붙어 있다.
    // onClick 연결은 Mountain 5 씬(바이너리)에 오버라이드로 들어 있어서 그대로 두고, 대신
    // Button 자체의 클릭 처리를 꺼두고 여기서 탭을 센 뒤 onClick을 직접 실행한다.
    [RequireComponent(typeof(Button))]
    public class RepeatTapButton : MonoBehaviour, IPointerClickHandler
    {
        [Min(1)] public int requiredTaps = 10;
        public float maxIntervalSeconds = 1f;

        Button _button;
        int _count;
        float _lastTapTime = float.NegativeInfinity;

        void Awake()
        {
            _button = GetComponent<Button>();
            // 꺼진 컴포넌트는 이벤트 시스템이 클릭을 넘겨주지 않는다 - 탭마다 바로 실행되는 걸 막는다.
            // (onClick.Invoke()는 컴포넌트가 꺼져 있어도 동작한다.)
            _button.enabled = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            float now = Time.unscaledTime;
            if (now - _lastTapTime > maxIntervalSeconds)
                _count = 0;

            _lastTapTime = now;
            _count++;

            if (_count < requiredTaps)
                return;

            _count = 0;
            _button.onClick.Invoke();
        }
    }
}
