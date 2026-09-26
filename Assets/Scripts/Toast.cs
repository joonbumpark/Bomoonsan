namespace Mountains
{
    // 짧은 알림을 한 줄로 띄우는 정적 파사드. 코인 획득, 저장 완료처럼 흐름을 끊지 않아야
    // 하는 안내에 쓴다 — 흐름을 멈추고 확인을 받아야 하면 MessagePopup 쪽이다.
    public static class Toast
    {
        // duration을 넘기지 않으면 ToastUI.defaultDuration을 쓴다.
        public static void Show(string message)
        {
            ToastUI.GetOrCreate().Show(message);
        }

        public static void Show(string message, float duration)
        {
            ToastUI.GetOrCreate().Show(message, duration);
        }
    }
}
