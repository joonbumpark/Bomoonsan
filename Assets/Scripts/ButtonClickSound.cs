using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mountains
{
    // Mountain 씬의 모든 Button에 클릭 사운드를 붙인다. 씬 파일을 건드리지 않고 동작하도록
    // 런타임에 Button을 찾아 onClick에 리스너를 추가한다 — 나중에 생기는 팝업/다이얼로그 버튼도
    // 주기적으로 다시 찾아서 붙이므로 따로 등록할 필요가 없다.
    // 소리는 Resources/Sounds/Sfx/click.ogg. 다른 소리를 쓰려면 아래 상수만 바꾼다.
    public class ButtonClickSound : MonoBehaviour
    {
        const string ClipPath = "Sounds/Sfx/click";
        const float Volume = 0.8f;
        const float ScanInterval = 0.5f;

        readonly HashSet<Button> _hooked = new HashSet<Button>();
        AudioClip _clip;
        float _nextScanTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Register()
        {
            SceneBootstrap.Register<ButtonClickSound>(SceneBootstrap.MountainScene);
        }

        void Start()
        {
            _clip = Resources.Load<AudioClip>(ClipPath);
            if (_clip == null)
            {
                Debug.LogWarning($"[ButtonClickSound] 클릭 소리 파일이 없음: Resources/{ClipPath}");
            }

            Scan();
        }

        void Update()
        {
            if (Time.unscaledTime < _nextScanTime)
            {
                return;
            }

            _nextScanTime = Time.unscaledTime + ScanInterval;
            Scan();
        }

        void Scan()
        {
            _hooked.RemoveWhere(button => button == null);

            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (_hooked.Add(button))
                {
                    button.onClick.AddListener(PlayClick);
                }
            }
        }

        void PlayClick()
        {
            if (_clip != null)
            {
                SoundManager.GetOrCreate().PlaySfx(_clip, Volume);
            }
        }
    }
}
