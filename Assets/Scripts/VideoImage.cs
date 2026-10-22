using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Mountains
{
    // UI 이미지 자리에 영상을 재생한다. 같은 오브젝트의 Image는 "포스터"로 남겨 두고, 영상의 첫 프레임이
    // 준비되는 순간 영상으로 바꿔 끼운다 — 로딩 동안 검은 화면이 보이지 않는다.
    //
    // 영상은 Assets/StreamingAssets 폴더에서 URL로 읽는다. WebGL은 VideoClip 에셋 재생을 지원하지
    // 않아서(URL만 가능) Android/WebGL/에디터 모두 같은 방식으로 맞췄다.
    //
    // 같은 오브젝트에 CoverFitter나 AspectRatioFitter가 있으면 영상 비율로 맞춰 준다.
    [RequireComponent(typeof(RectTransform))]
    public class VideoImage : MonoBehaviour
    {
        [Tooltip("Assets/StreamingAssets 기준 파일 이름 (예: Title.mp4)")]
        public string fileName = "Title.mp4";
        [Tooltip("끝나면 처음부터 다시 재생한다.")]
        public bool loop = true;
        [Tooltip("소리를 끈다. WebGL은 사용자가 화면을 누르기 전에는 소리 있는 영상을 자동 재생하지 못하므로 " +
            "처음부터 나오는 배경 영상은 켜 두는 게 안전하다.")]
        public bool mute = true;
        [Tooltip("켜지면 바로 재생한다. 끄면 Play()를 직접 불러야 한다.")]
        public bool playOnEnable = true;
        [Tooltip("같은 오브젝트의 CoverFitter/AspectRatioFitter 비율을 영상 비율로 맞춘다.")]
        public bool matchAspect = true;
        [Tooltip("지정하면 직접 재생하지 않고 그 VideoImage의 화면을 그대로 같이 보여준다 — 같은 영상을 " +
            "두 번 디코딩하지 않는다(어둡게 깐 채움 배경 등).")]
        public VideoImage mirrorOf;

        // 지금 화면에 영상이 나오고 있으면 그 텍스처, 아니면 null.
        public Texture CurrentTexture => _surface != null && _surface.enabled ? _surface.texture : null;
        public float VideoAspect { get; private set; }

        // loop가 꺼진 영상이 끝까지 재생됐을 때(또는 재생에 실패했을 때) 한 번 불린다.
        public event Action Finished;

        Image _poster;
        RawImage _surface;
        VideoPlayer _player;
        RenderTexture _texture;
        bool _finished;

        public static string Url(string file)
        {
            // WebGL에서는 http URL, Android에서는 jar:file:// 경로, 에디터/PC에서는 로컬 경로가 된다.
            return Application.streamingAssetsPath + "/" + file;
        }

        void Awake()
        {
            _poster = GetComponent<Image>();
            if (_poster != null)
            {
                // 영상 비율로 바뀐 칸에서 포스터 그림이 늘어나 보이지 않게 한다.
                _poster.preserveAspect = true;
            }

            var surfaceGo = new GameObject("Video", typeof(RectTransform));
            var rect = (RectTransform)surfaceGo.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _surface = surfaceGo.AddComponent<RawImage>();
            _surface.raycastTarget = false;
            // 어둡게 깐 채움 배경처럼 포스터에 색을 줘 둔 경우 영상에도 같은 색을 입힌다.
            _surface.color = _poster != null ? _poster.color : Color.white;
            _surface.enabled = false;

            if (mirrorOf != null)
            {
                return;
            }

            _player = gameObject.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.source = VideoSource.Url;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.waitForFirstFrame = true;
            _player.skipOnDrop = true;
            _player.prepareCompleted += OnPrepared;
            _player.loopPointReached += OnLoopPointReached;
            _player.errorReceived += OnError;
            _player.frameReady += OnFrameReady;
        }

        void OnEnable()
        {
            if (playOnEnable && mirrorOf == null)
            {
                Play();
            }
        }

        // 첫 프레임이 나오면 포스터를 영상으로 바꾼다. 따라 보여주는 쪽(mirrorOf)은 원본의 첫 프레임이
        // 나올 때까지 기다렸다가 같은 텍스처를 붙인다.
        void LateUpdate()
        {
            if (_surface.enabled)
            {
                return;
            }

            if (mirrorOf == null)
            {
                // frameReady 이벤트를 못 받는 플랫폼(WebGL 등)을 위한 대비 — 재생이 시작돼 프레임이
                // 넘어가기 시작하면 영상으로 바꾼다.
                if (_player != null && _player.isPlaying && _player.frame >= 1)
                {
                    ShowSurface();
                }
                return;
            }

            var texture = mirrorOf.CurrentTexture;
            if (texture == null)
            {
                return;
            }

            _surface.texture = texture;
            if (matchAspect && mirrorOf.VideoAspect > 0f)
            {
                ApplyAspect(mirrorOf.VideoAspect);
            }
            ShowSurface();
        }

        void OnDisable()
        {
            if (_player != null)
            {
                _player.Stop();
            }
        }

        void OnDestroy()
        {
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
                _texture = null;
            }
        }

        public void Play()
        {
            if (_player == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(fileName))
            {
                Debug.LogWarning($"[VideoImage] {name}: fileName이 비어 있습니다.");
                Finish();
                return;
            }

            _finished = false;
            _player.url = Url(fileName);
            _player.isLooping = loop;
            _player.audioOutputMode = mute ? VideoAudioOutputMode.None : VideoAudioOutputMode.Direct;
            _player.sendFrameReadyEvents = true;
            _player.Prepare();
        }

        public void Stop()
        {
            if (_player != null)
            {
                _player.Stop();
            }
        }

        void OnPrepared(VideoPlayer player)
        {
            int width = (int)player.width;
            int height = (int)player.height;
            if (width > 0 && height > 0)
            {
                if (_texture == null || _texture.width != width || _texture.height != height)
                {
                    if (_texture != null)
                    {
                        _texture.Release();
                        Destroy(_texture);
                    }

                    _texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = $"Video_{fileName}" };
                    _texture.Create();
                }

                player.targetTexture = _texture;
                _surface.texture = _texture;

                VideoAspect = width / (float)height;
                if (matchAspect)
                {
                    ApplyAspect(VideoAspect);
                }
            }

            player.Play();
        }

        // 첫 프레임이 실제로 그려진 뒤에 포스터와 바꿔 끼운다 — Play() 직후에는 텍스처가 아직 비어 있다.
        void OnFrameReady(VideoPlayer player, long frameIndex)
        {
            player.sendFrameReadyEvents = false;
            ShowSurface();
        }

        void ShowSurface()
        {
            _surface.enabled = true;
            if (_poster != null)
            {
                _poster.enabled = false;
            }
        }

        void ApplyAspect(float aspect)
        {
            var cover = GetComponent<CoverFitter>();
            if (cover != null && !Mathf.Approximately(cover.aspectRatio, aspect))
            {
                cover.aspectRatio = aspect;
                // CoverFitter는 부모 크기가 바뀔 때만 다시 배치하므로 껐다 켜서 즉시 반영한다.
                cover.enabled = false;
                cover.enabled = true;
            }

            var fitter = GetComponent<AspectRatioFitter>();
            if (fitter != null)
            {
                fitter.aspectRatio = aspect;
            }
        }

        void OnLoopPointReached(VideoPlayer player)
        {
            if (!loop)
            {
                Finish();
            }
        }

        void OnError(VideoPlayer player, string message)
        {
            Debug.LogWarning($"[VideoImage] {fileName} 재생 실패: {message}");
            // 배경 영상은 포스터 그림이 그대로 남으면 되고, 한 번 재생하는 영상은 멈춰 서지 않게 끝난 것으로 본다.
            if (!loop)
            {
                Finish();
            }
        }

        void Finish()
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            Finished?.Invoke();
        }
    }
}
