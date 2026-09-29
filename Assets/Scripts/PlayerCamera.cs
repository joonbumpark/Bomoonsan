using UnityEngine;

namespace Mountains
{
    // Cinemachine 없이 직접 구현한 카메라. 기본은 플레이어를 따라다니는 3인칭 추적 모드고,
    // toggleKey를 누르면 플레이어 고정을 풀고 지형을 자유롭게 둘러볼 수 있는 Free-fly 모드로
    // 전환된다. Main Camera에 직접 붙여서 이 스크립트가 카메라 Transform을 바로 제어한다.
    public class PlayerCamera : MonoBehaviour
    {
        public enum Mode
        {
            FollowPlayer,
            FreeFly,
        }

        [Header("Target")]
        public Transform player;
        public CharacterMovement playerMovement;

        [Header("Follow Mode")]
        public float distance = 8f;
        public float heightOffset = 3f;
        [Range(0f, 80f)] public float pitchAngle = 25f;
        public float followSmoothTime = 0.15f;

        [Header("Free Fly Mode")]
        public float flySpeed = 20f;
        public float flyBoostMultiplier = 3f;
        public float mouseSensitivity = 3f;

        [Header("Input")]
        public KeyCode toggleKey = KeyCode.Tab;

        public Mode CurrentMode { get; private set; } = Mode.FollowPlayer;

        // 이벤트 트리거가 대화 중 이 카메라를 잠깐 꺼뒀다 다시 켤 때 참조한다.
        public static PlayerCamera Instance { get; private set; }

        Vector3 _followVelocity;
        float _flyYaw;
        float _flyPitch;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            if (player == null)
            {
                var found = GameObject.FindGameObjectWithTag("Player");
                if (found != null)
                {
                    player = found.transform;
                }
            }

            if (playerMovement == null && player != null)
            {
                playerMovement = player.GetComponent<CharacterMovement>();
            }

            SetMode(Mode.FollowPlayer);
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                SetMode(CurrentMode == Mode.FollowPlayer ? Mode.FreeFly : Mode.FollowPlayer);
            }

            if (CurrentMode != Mode.FreeFly) return;

            HandleCursorLockInput();

            // Game 뷰가 포커스를 잃으면(마우스가 Inspector 등 다른 패널로 나가면), 혹은
            // 커서가 잠겨있지 않으면(Esc를 눌렀거나 아직 클릭 전) 회전·이동을 멈춘다.
            // Application.isFocused만으로는 부족하다 — 에디터 안에서 마우스가 다른 패널로
            // 가도 유니티 애플리케이션 자체는 여전히 포커스된 상태라 안 걸러진다.
            bool canLook = Cursor.lockState == CursorLockMode.Locked
                && Application.isFocused
                && IsGameViewFocused();

            if (canLook)
            {
                UpdateFreeFly();
            }
        }

        // 유니티 에디터의 Game 뷰는 실제 OS 커서 잠금이 아니라서, 빌드된 플레이어와 달리
        // Esc를 눌러도 Cursor.lockState가 저절로 바뀌지 않는다 — 우리가 직접 감지해서
        // 풀어줘야 한다. 다시 마우스룩을 하고 싶으면 Game 뷰를 클릭해서 재잠금한다
        // (흔한 FPS류 컨트롤러 패턴).
        void HandleCursorLockInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0) && IsGameViewFocused())
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        static bool IsGameViewFocused()
        {
#if UNITY_EDITOR
            var focused = UnityEditor.EditorWindow.focusedWindow;
            return focused != null && focused.GetType().Name == "GameView";
#else
            return true;
#endif
        }

        void LateUpdate()
        {
            if (CurrentMode == Mode.FollowPlayer)
            {
                UpdateFollow();
            }
        }

        public void SetMode(Mode mode)
        {
            CurrentMode = mode;

            if (playerMovement != null)
            {
                // Free-fly 중에는 WASD가 카메라를 움직이는 데 쓰이므로 플레이어 이동은 멈춰둔다.
                playerMovement.enabled = mode == Mode.FollowPlayer;
            }

            bool freeFly = mode == Mode.FreeFly;
            Cursor.lockState = freeFly ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !freeFly;

            if (freeFly)
            {
                Vector3 euler = transform.eulerAngles;
                _flyYaw = euler.y;
                _flyPitch = euler.x > 180f ? euler.x - 360f : euler.x;
            }
        }

        void UpdateFollow()
        {
            if (player == null) return;

            Quaternion rotation = player.rotation * Quaternion.Euler(pitchAngle, 0f, 0f);
            Vector3 focusPoint = player.position + Vector3.up * heightOffset;
            Vector3 desiredPosition = focusPoint - rotation * Vector3.forward * distance;

            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _followVelocity, followSmoothTime);

            // LookAt(focusPoint)을 쓰면, 위치가 SmoothDamp로 desiredPosition을 뒤늦게
            // 쫓아가는 동안(특히 회전 중) 카메라가 "이상적인 배후 직선"에서 살짝 벗어난
            // 위치에 있게 되고, 그 벗어난 위치에서 focusPoint를 바라보면 rotation과는
            // 다른 각도가 나온다. 이 오차가 CharacterMovement의 카메라 기준 이동 방향
            // 계산에 다시 들어가서(카메라→이동 방향→플레이어 회전→카메라... 순환), 살짝만
            // 방향을 틀어도 카메라가 계속 흔들리며 도는 원인이었다. 대신 위치가 어디에
            // 있든 상관없이 desiredPosition을 구한 것과 같은 rotation을 그대로 카메라
            // 회전에 써서, 카메라 회전이 오직 player.rotation(과 그걸 스무딩하는
            // CharacterMovement의 회전 데드존)에만 좌우되게 한다.
            transform.rotation = rotation;
        }

        void UpdateFreeFly()
        {
            _flyYaw += Input.GetAxis("Mouse X") * mouseSensitivity;
            _flyPitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            _flyPitch = Mathf.Clamp(_flyPitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(_flyPitch, _flyYaw, 0f);

            float speed = flySpeed * (Input.GetKey(KeyCode.LeftShift) ? flyBoostMultiplier : 1f);

            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += transform.forward;
            if (Input.GetKey(KeyCode.S)) move -= transform.forward;
            if (Input.GetKey(KeyCode.A)) move -= transform.right;
            if (Input.GetKey(KeyCode.D)) move += transform.right;
            if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space)) move += Vector3.up;
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftControl)) move -= Vector3.up;

            transform.position += move.normalized * speed * Time.deltaTime;
        }
    }
}
