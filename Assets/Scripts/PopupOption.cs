using System;
using UnityEngine;

namespace Mountains
{
    // 옵션 팝업. 버튼의 OnClick에 아래 메서드들을 연결해서 쓴다.
    public class PopupOption : MonoBehaviour
    {
        [Tooltip("타이틀 버튼이 이동할 씬 이름. File > Build Settings 목록에 등록돼 있어야 한다.")]
        public string titleSceneName = "TitleScene";

        [Header("확인 팝업")]
        [Tooltip("끄면 확인 없이 바로 전환한다. 켜두면 실수로 눌렀을 때 진행 상황을 잃지 않는다.")]
        public bool confirmBeforeLeaving = true;
        public string reloadMessage = "현재 씬을 처음부터 다시 시작할까요?";
        public string titleMessage = "타이틀로 돌아갈까요?";

        public void OnClickBG()
        {
            gameObject.SetActive(false);
        }

        // 현재 씬을 처음부터 다시 시작한다.
        public void OnClickReload()
        {
            ConfirmThen(reloadMessage, SceneFlow.ReloadCurrent);
        }

        public void OnClickTitle()
        {
            ConfirmThen(titleMessage, () => SceneFlow.Load(titleSceneName));
        }

        void ConfirmThen(string message, Action action)
        {
            if (!confirmBeforeLeaving)
            {
                action();
                return;
            }

            // 취소를 누르면 아무 일도 일어나지 않고 옵션 팝업이 그대로 남는다.
            MessagePopup.Confirm(message, action);
        }
    }
}
