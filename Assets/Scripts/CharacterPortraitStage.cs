using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

namespace Mountains
{
    // 대화창 초상화를 미리 그려둔 Sprite 대신 CharacterData의 프리팹을 실제로 렌더링해서
    // 만든다. 캐릭터를 추가할 때마다 초상화 이미지를 따로 그려 넣을 필요가 없고, 모델을
    // 바꾸면 초상화도 자동으로 따라간다.
    //
    // 전용 Layer를 파지 않고 "거리"로 격리하는 이유: Layer를 새로 만들려면
    // ProjectSettings/TagManager를 건드려야 하는데, 유니티가 열려 있는 동안 그 파일을
    // 외부에서 수정하면 덮어써진다(이 프로젝트에서 이미 한 번 겪음). 스테이지를 지형
    // 한참 아래(StageY)에 두고 초상화 카메라의 Far Clip을 짧게 잡으면, 본편 카메라는
    // Far Clip(지형 대각선 기준) 안에 스테이지가 들어오지 않아 서로를 절대 못 본다.
    public class CharacterPortraitStage : MonoBehaviour
    {
        public static CharacterPortraitStage Instance { get; private set; }

        // 본편 지형 아래로 충분히 떨어뜨린다 — 지형 높이(수십 m)와 겹칠 일이 없다.
        const float StageY = -5000f;

        [Tooltip("초상화 RenderTexture 한 변의 크기(정사각형).")]
        [Min(64)] public int textureSize = 512;
        [Tooltip("초상화 카메라의 화각. 작을수록 원근 왜곡이 줄어 인물 사진처럼 보인다.")]
        [Range(10f, 60f)] public float fieldOfView = 30f;

        // 인스턴스와 함께, 다시 프레이밍해야 하는지 판단할 재료를 같이 들고 있는다.
        class PortraitModel
        {
            public GameObject sourcePrefab;
            public Transform instance;
            public Renderer[] renderers;

            // 마지막으로 반영한 CharacterData 값 — 인스펙터에서 바뀌면 여기와 달라진다.
            public float zoom;
            public float yaw;
            public Vector3 offset;

            // 모델 경계를 인스턴스 로컬 기준으로 한 번만 재서 저장한 것. Idle 애니메이션이
            // 있으면 스키닝 경계가 프레임마다 달라서, 대화 스텝마다 다시 재면 같은 캐릭터인데도
            // 구도가 조금씩 어긋난다.
            public bool hasFramingBounds;
            public Vector3 localCenter;
            public float radius;

            public bool MatchesFraming(CharacterData character)
            {
                return Mathf.Approximately(zoom, character.portraitZoom)
                    && Mathf.Approximately(yaw, character.portraitYaw)
                    && offset == character.portraitOffset;
            }
        }

        Camera _camera;
        Transform _anchor;
        RenderTexture _texture;
        CharacterData _currentCharacter;
        PortraitModel _current;

        // 같은 화자가 여러 번 말할 때마다 프리팹을 새로 인스턴스화하지 않게 캐시한다.
        readonly Dictionary<CharacterData, PortraitModel> _models = new Dictionary<CharacterData, PortraitModel>();

        public static CharacterPortraitStage GetOrCreate()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var go = new GameObject("CharacterPortraitStage");
            return go.AddComponent<CharacterPortraitStage>();
        }

        // 초상화를 안 쓰는 대화에서까지 스테이지를 만들지 않도록, 이미 있을 때만 끈다.
        public static void HideIfExists()
        {
            if (Instance != null)
            {
                Instance.Hide();
            }
        }

        void Awake()
        {
            Instance = this;
            transform.position = new Vector3(0f, StageY, 0f);
        }

        void OnDestroy()
        {
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
                _texture = null;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        // 이 캐릭터의 초상화를 그리기 시작하고, 대화창에 붙일 RenderTexture를 돌려준다.
        // 모델이 없으면 null — 호출한 쪽이 기존 Sprite 초상화로 폴백하면 된다.
        public RenderTexture Show(CharacterData character)
        {
            var prefab = character != null ? character.PortraitPrefab : null;
            if (prefab == null)
            {
                Hide();
                return null;
            }

            EnsureStage();

            var model = GetOrCreateModel(character, prefab);
            if (_current != null && _current != model)
            {
                _current.instance.gameObject.SetActive(false);
            }
            _current = model;
            _currentCharacter = character;
            model.instance.gameObject.SetActive(true);

            FrameModel(character, model);

            // 표시하는 동안은 계속 렌더한다 — 모델에 Idle 애니메이션이 있으면 초상화도
            // 같이 살아 움직인다. 대화가 끝나면 Hide()에서 카메라를 꺼서 비용을 0으로 돌린다.
            _camera.enabled = true;
            return _texture;
        }

        public void Hide()
        {
            if (_current != null)
            {
                _current.instance.gameObject.SetActive(false);
                _current = null;
            }
            _currentCharacter = null;

            if (_camera != null)
            {
                _camera.enabled = false;
            }
        }

        // CharacterData의 zoom/yaw/offset(그리고 프리팹 교체)을 인스펙터에서 만지면 대화창을
        // 다시 띄우지 않아도 바로 반영되게 한다 — 초상화 구도를 맞추는 건 값을 조금씩 바꿔가며
        // 눈으로 확인하는 작업이라, 매번 대화를 다시 재생해야 하면 맞추기가 어렵다.
        //
        // 값이 바뀌었을 때만 다시 프레이밍한다. 경계는 모델마다 한 번만 재서 저장해 두므로
        // (PortraitModel.hasFramingBounds) Idle 애니메이션으로 스키닝 경계가 흔들려도
        // 카메라는 따라 흔들리지 않는다.
        void LateUpdate()
        {
            if (_current == null || _currentCharacter == null)
            {
                return;
            }

            // 초상화 프리팹 자체를 바꿨으면 인스턴스부터 다시 만들어야 한다.
            if (_current.sourcePrefab != _currentCharacter.PortraitPrefab)
            {
                Show(_currentCharacter);
                return;
            }

            if (_current.MatchesFraming(_currentCharacter))
            {
                return;
            }

            FrameModel(_currentCharacter, _current);
        }

        void EnsureStage()
        {
            if (_camera != null)
            {
                return;
            }

            _texture = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32)
            {
                name = "CharacterPortraitRT"
            };
            _texture.Create();

            _anchor = new GameObject("ModelAnchor").transform;
            _anchor.SetParent(transform, false);

            CreateCamera();
            CreateFillLight();
        }

        void CreateCamera()
        {
            var cameraGo = new GameObject("PortraitCamera");
            cameraGo.transform.SetParent(transform, false);

            _camera = cameraGo.AddComponent<Camera>();
            _camera.fieldOfView = fieldOfView;
            // 배경을 알파 0으로 지워서 캐릭터만 오려낸 초상화가 되게 한다 — 대화창 배경색
            // (CharacterData.dialogColor)이 그대로 뒤에 비친다.
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 50f; // 본편 지형이 들어올 수 없는 짧은 거리.
            _camera.targetTexture = _texture;
            _camera.enabled = false;

            var cameraData = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderType = CameraRenderType.Base;
            cameraData.renderPostProcessing = false;
            cameraData.renderShadows = false;
            cameraData.requiresColorOption = CameraOverrideOption.Off;
            cameraData.requiresDepthOption = CameraOverrideOption.Off;
        }

        // 씬의 태양(Directional)은 거리와 무관하게 여기도 비추므로 기본 조명은 이미 있다.
        // 얼굴이 어둡게 깔리지 않게 채움광만 하나 더한다 — Point 라이트라 도달 범위(range)
        // 밖인 본편 씬에는 아무 영향이 없다.
        void CreateFillLight()
        {
            var lightGo = new GameObject("PortraitFillLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(1.5f, 2f, -2.5f);

            var fillLight = lightGo.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.range = 20f;
            fillLight.intensity = 2f;
            fillLight.shadows = LightShadows.None;
        }

        PortraitModel GetOrCreateModel(CharacterData character, GameObject prefab)
        {
            if (_models.TryGetValue(character, out var cached) && cached.instance != null)
            {
                // 프리팹을 바꾼 경우에만 버리고 다시 만든다.
                if (cached.sourcePrefab == prefab)
                {
                    return cached;
                }

                Destroy(cached.instance.gameObject);
                _models.Remove(character);
            }

            var instance = Instantiate(prefab, _anchor).transform;
            instance.localPosition = Vector3.zero;
            instance.localRotation = Quaternion.identity;
            StripNonVisualComponents(instance);

            var model = new PortraitModel
            {
                sourcePrefab = prefab,
                instance = instance,
                // 프레이밍할 때마다 GetComponentsInChildren을 다시 돌리지 않게 여기서 한 번만 모은다.
                // 파티클 렌더러는 뺀다 — 프레이밍은 "몸통"을 기준으로 해야 하는데, 이펙트(바람, 오라 등)의
                // 경계는 모델보다 훨씬 커서 포함하면 카메라가 멀리 물러나 캐릭터가 점으로 보인다.
                renderers = System.Array.FindAll(instance.GetComponentsInChildren<Renderer>(true),
                    renderer => !(renderer is ParticleSystemRenderer))
            };

            _models[character] = model;
            return model;
        }

        // 초상화는 "보이기만" 하면 된다. 통째로 만든 캐릭터 프리팹을 지정했을 때 콜라이더가
        // 플레이어를 막거나, NavMeshAgent가 스테이지 위치에서 NavMesh를 못 찾아 경고를
        // 쏟는 걸 막는다.
        static void StripNonVisualComponents(Transform instance)
        {
            foreach (var agent in instance.GetComponentsInChildren<NavMeshAgent>(true))
            {
                Destroy(agent);
            }
            foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true))
            {
                Destroy(body);
            }
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
            {
                Destroy(collider);
            }

            // 월드용 이펙트(프리팹에 붙여둔 파티클)는 초상화에 필요 없고, 크기가 모델보다 커서
            // 구도를 망친다 — 파티클이 달린 오브젝트를 꺼둔다.
            foreach (var particles in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.gameObject.SetActive(false);
            }

            // Animator는 남겨서 Idle 애니메이션이 돌게 하되, 루트 모션은 끈다 — 켜져 있으면
            // 모델이 제자리에서 애니메이션대로 걸어 나가 초상화 프레임 밖으로 사라진다.
            foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
            {
                animator.applyRootMotion = false;
            }
        }

        // 캐릭터마다 카메라를 손으로 맞추지 않아도 되게, 모델의 Renderer 경계를 합쳐서
        // 화면에 꽉 차는 거리를 계산한다. 미세 조정은 CharacterData의 zoom/yaw/offset으로 한다.
        void FrameModel(CharacterData character, PortraitModel model)
        {
            model.zoom = character.portraitZoom;
            model.yaw = character.portraitYaw;
            model.offset = character.portraitOffset;

            if (!model.hasFramingBounds && !MeasureFramingBounds(model))
            {
                Debug.LogWarning($"[CharacterPortraitStage] {character.name}의 초상화 프리팹에 " +
                    "Renderer가 없어 프레이밍을 건너뜁니다.");
                return;
            }

            model.instance.localRotation = Quaternion.Euler(0f, character.portraitYaw, 0f);

            // 경계를 감싸는 구가 화각에 딱 들어오는 거리. zoom이 클수록 더 당겨 찍는다.
            float radius = model.radius;
            float zoom = Mathf.Max(0.01f, character.portraitZoom);
            float distance = radius / Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad) / zoom;

            // 저장해 둔 로컬 중심을 지금 회전(yaw)으로 옮긴다 — 구 반지름은 회전과 무관하다.
            Vector3 target = model.instance.TransformPoint(model.localCenter) + character.portraitOffset;
            _camera.transform.SetPositionAndRotation(target + new Vector3(0f, 0f, -distance), Quaternion.identity);
            _camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2f);
            _camera.farClipPlane = distance + radius * 4f;
        }

        // 회전하지 않은 기본 자세에서 한 번 재서 인스턴스 로컬 좌표로 저장한다. 회전을
        // 풀고 재는 이유: 축 정렬 경계(AABB)는 모델을 돌리면 크기가 달라져서, yaw마다
        // 반지름이 바뀌면 줌이 같아도 거리가 달라진다.
        static bool MeasureFramingBounds(PortraitModel model)
        {
            var rotation = model.instance.localRotation;
            model.instance.localRotation = Quaternion.identity;

            // 로컬 변환도 잰 자세(회전 없음) 그대로에서 해야 한다 — 회전을 되돌린 뒤에
            // 변환하면 중심이 그 회전만큼 엉뚱한 곳으로 옮겨진다.
            bool found = TryGetBounds(model, out var bounds);
            if (found)
            {
                model.localCenter = model.instance.InverseTransformPoint(bounds.center);
                model.radius = Mathf.Max(0.01f, bounds.extents.magnitude);
                model.hasFramingBounds = true;
            }

            model.instance.localRotation = rotation;
            return found;
        }

        static bool TryGetBounds(PortraitModel model, out Bounds bounds)
        {
            bounds = default;
            bool found = false;

            foreach (var renderer in model.renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return found;
        }
    }
}
