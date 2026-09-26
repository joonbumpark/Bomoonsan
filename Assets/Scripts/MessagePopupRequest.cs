using System;

namespace Mountains
{
    // 팝업 한 번 띄우는 요청. 인자가 여섯 개를 넘어가서 오버로드로 풀면 호출부가 무엇을
    // 넘기는지 알아보기 어려워지므로 데이터로 묶었다 — 단순한 경우는 MessagePopup의 짧은
    // 정적 메서드를 쓰면 되고, 버튼 문구까지 바꾸고 싶을 때만 이걸 직접 만든다.
    public class MessagePopupRequest
    {
        public string title;
        public string message;

        public string confirmText = "확인";
        public string cancelText = "취소";
        // 취소 버튼을 보일지. 끄면 확인 하나만 있는 알림 팝업이 된다.
        public bool showCancel;
        // 배경을 탭해서 닫을 수 있게 할지. 닫히면 취소로 취급한다 — 확인만 있는 팝업에서도
        // "안 읽고 닫기"를 허용하고 싶지 않으면 끄면 된다.
        public bool closeOnBackground = true;

        public Action onConfirm;
        public Action onCancel;

        // 확인이든 취소든 팝업이 닫힌 뒤 호출된다. TriggerAction이 "팝업이 닫힐 때까지
        // 다음 스텝으로 넘어가지 않기"를 구현하는 데 쓴다 — 어느 버튼을 눌렀든 시퀀스는
        // 진행돼야 하므로 onConfirm/onCancel과 별도로 필요하다.
        public Action onClosed;
    }
}
