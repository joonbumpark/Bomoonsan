using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Cinemachine;

namespace Mountains
{
    // Player/Npc "몸통"(NavMeshAgent+Collider+Rigidbody+역할별 컴포넌트)은 고정
    // 프리팹(playerPrefab/npcPrefab)으로 미리 구성해두고, 여기서는 그 프리팹을
    // Instantiate한 뒤 CharacterData의 외형(modelPrefab)만 그 프리팹의 "Root" 자식
    // 밑에 갈아끼운다 — 캐릭터마다 골격/충돌체는 같고 모델만 다른 경우에 맞는 구조.
    // GameManager/DialogUI처럼 씬에 배치해서 쓰는 싱글턴이다(플레이어 프리팹을
    // 인스펙터로 할당해야 하므로 더 이상 코드로 자기 자신을 만들지 않는다).
    public class CharacterManager : MonoBehaviour
    {
        public static CharacterManager Instance { get; private set; }

        [Header("캐릭터 베이스 프리팹")]
        public GameObject playerPrefab;
        public GameObject npcPrefab;

        void Awake()
        {
            Instance = this;
        }

        public GameObject CreatePlayer(CharacterData data, Vector3 position)
        {
            var go = InstantiateCharacter(playerPrefab, data, position, "Player");
            if (go == null)
            {
                return null;
            }

            WirePlayerCamera(go);
            WirePlayerMobileControls(go, go.GetComponent<CharacterMovement>());
            WirePlayerTreeOcclusionFader(go);

            return go;
        }
        public GameObject CreateNpc(CharacterData data, Vector3 position, Quaternion rotation)
        {
            return InstantiateCharacter(npcPrefab, data, position, rotation, "Npc");
        }
        
        public GameObject CreateNpc(CharacterData data, Vector3 position)
        {
            return InstantiateCharacter(npcPrefab, data, position, "Npc");
        }

        // 플레이어를 쫓아오는 대신, 플레이어가 detectRadius 안에 들어오면 pathPoints를
        // 따라 걷는 NPC를 만든다 — 몸통은 npcPrefab을 그대로 재사용하고(NavMeshAgent/
        // Collider/Rigidbody는 이미 있음), 프리팹에 기본으로 들어있는 NpcFollower만
        // NpcPathWalker로 바꿔 단다.
        public GameObject CreatePathWalkerNpc(CharacterData data, Vector3 spawnPoint, Transform[] pathPoints)
        {
            var go = InstantiateCharacter(npcPrefab, data, spawnPoint, "Npc");
            if (go == null)
            {
                return null;
            }

            var follower = go.GetComponent<NpcFollower>();
            if (follower != null)
            {
                Destroy(follower);
            }

            var walker = go.AddComponent<NpcPathWalker>();
            walker.pathPoints = pathPoints;

            return go;
        }

        // 프리팹을 그대로 찍어내고(NavMeshAgent/Collider/Rigidbody/역할별 컴포넌트는
        // 전부 프리팹에 이미 있음), CharacterData의 외형만 "Root" 자식 밑에 심는다.
        // "Root"가 없는 프리팹이면 루트 트랜스폼 바로 밑에 심는다.
        // 프리팹에 드라이버를 미리 붙여 파라미터 이름을 튜닝해뒀으면 그걸 그대로 쓰고,
        // 없으면 기본값으로 붙인다 — 이 프로젝트의 다른 "Ensure" 패턴과 같은 방식이라
        // 튜닝한 값이 런타임 생성 때문에 덮어써질 일이 없다.
        static CharacterAnimatorDriver EnsureAnimatorDriver(GameObject character)
        {
            var driver = character.GetComponent<CharacterAnimatorDriver>();
            return driver != null ? driver : character.AddComponent<CharacterAnimatorDriver>();
        }

        GameObject InstantiateCharacter(GameObject prefab, CharacterData data, Vector3 position, string fallbackName)
        {
            return InstantiateCharacter(prefab, data, position, Quaternion.identity, fallbackName);
        }

        GameObject InstantiateCharacter(GameObject prefab, CharacterData data, Vector3 position, Quaternion rotation, string fallbackName)
        {
            if (prefab == null)
            {
                Debug.LogWarning($"[CharacterManager] {fallbackName} 프리팹이 지정되지 않았습니다.");
                return null;
            }

            // NavMeshAgent는 컴포넌트가 활성화되는 순간(=Instantiate 시점) 자기 위치
            // 근처의 NavMesh에 스스로 올라타려고 시도한다 — 위치 없이 Instantiate하면
            // 월드 원점(0,0,0)에서 시도하게 되고, 그 근처에 NavMesh가 없으면 "Failed to
            // create agent because it is not close enough to the NavMesh" 경고와 함께
            // 에이전트가 NavMesh에 안 붙은 상태로 남는다. 그래서 Instantiate할 때부터
            // 목표 위치를 바로 넣어준다 — 아래 Warp()는 그 위치를 NavMesh 위 정확한
            // 지점으로 한 번 더 스냅하는 역할이다.
            var go = Instantiate(prefab, position, Quaternion.identity);
            go.name += data != null && !string.IsNullOrEmpty(data.characterName) ? data.characterName : fallbackName;

            if (data != null && data.modelPrefab != null)
            {
                var modelRoot = go.transform.Find("Root");
                var model = Instantiate(data.modelPrefab, modelRoot != null ? modelRoot : go.transform);

                // Animator는 방금 만든 모델 안에 있다 — 모델이 런타임에 생기므로 프리팹
                // 단계에서는 참조를 걸어둘 수 없고, 만들어진 직후 여기서 물려준다.
                EnsureAnimatorDriver(go).Bind(model);
            }

            // Warp는 NavMesh 위 가장 가까운 지점으로 안전하게 순간이동시켜준다. NavMesh가
            // 아직 없는 씬이면 실패할 수 있으니, 그때는 일단 좌표만 맞춰두고 안내한다.
            var agent = go.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                // 이동 성능은 캐릭터의 성질이라 CharacterData가 들고 있다 — 스폰 경로가
                // 여럿이므로(테스트 스폰, 트리거 액션, 팔로워) 여기 한 곳에서 적용해야
                // 어디서 만들든 같은 값이 나온다. 스폰 액션이 더 덮어쓸 수 있다.
                if (data != null)
                {
                    data.agentSettings.Apply(agent);
                }

                if (!agent.Warp(position))
                {
                    go.transform.position = position;
                    Debug.LogWarning($"[CharacterManager] {go.name}: NavMesh가 아직 없어 NavMesh 위로 정확히 놓지 못했습니다. " +
                        "Mountains > Bake NavMesh를 실행한 뒤 확인하세요.");
                }
            }

            return go;
        }

        // 평상시 카메라는 씬에 미리 배치해둔 Cinemachine 3인칭 리그("Main Virtual
        // Camera")가 직접 몬다 — 이름으로 찾아서 Follow/LookAt만 이 플레이어로 연결한다
        // (카메라 자체 세팅/CinemachineBrain 여부는 손대지 않음 — TerrainSceneSetup의
        // EnsureCinemachineBrain이 지형을 만들 때 이미 보장해둔다).
        void WirePlayerCamera(GameObject player)
        {
            var mainVCamObject = GameObject.Find("Main Virtual Camera");
            if (mainVCamObject == null)
            {
                Debug.LogWarning("[CharacterManager] 'Main Virtual Camera'를 찾을 수 없어 카메라 연결을 건너뜁니다.");
                return;
            }

            var mainVCam = mainVCamObject.GetComponent<CinemachineVirtualCamera>();
            if (mainVCam != null)
            {
                mainVCam.Follow = player.transform;
                mainVCam.LookAt = player.transform.Find("CamPivot") ?? player.transform;
            }
        }

        // 씬에 미리 배치해둔 Canvas/조이스틱(Joystick Pack)을 찾아서 이동·회전 스크립트에
        // 연결한다. Canvas나 조이스틱이 없으면(모바일 컨트롤을 아직 안 붙인 씬이면) 경고만
        // 남기고 나머지 설정은 그대로 진행한다.
        void WirePlayerMobileControls(GameObject player, CharacterMovement movement)
        {
            // 씬에 Canvas가 여러 개일 수 있다(조이스틱 프리팹이 자기 전용 Canvas를 내장한
            // 경우 등) — Canvas를 먼저 아무거나 고르면 조이스틱이 없는 쪽을 잡을 수 있으니,
            // 조이스틱을 먼저 찾고 그 조이스틱이 속한 Canvas를 기준으로 삼는다.
            // 이름으로 찾으면("Fixed Joystick") Joystick Pack의 다른 변형(Floating Joystick
            // 등)으로 바꿔 쓸 때마다 다시 안 맞는다 — 타입으로 찾아서 어떤 변형이든 잡는다.
            var joystick = Object.FindFirstObjectByType<Joystick>(FindObjectsInactive.Include);
            var canvas = joystick != null ? joystick.GetComponentInParent<Canvas>() : Object.FindFirstObjectByType<Canvas>();

            if (joystick != null)
            {
                movement.moveJoystick = joystick;
            }
            else
            {
                Debug.LogWarning("[CharacterManager] 조이스틱을 찾을 수 없어 조이스틱 이동 연결을 건너뜁니다.");
            }

            if (canvas == null)
            {
                Debug.LogWarning("[CharacterManager] Canvas를 찾을 수 없어 회전 드래그 연결을 건너뜁니다.");
                return;
            }

            var dragCatcher = FindOrCreateDragCatcher(canvas);
            dragCatcher.GetComponent<TouchRotateInput>().player = player.transform;
        }

        // 화면 전체를 덮는 투명 UI Image + TouchRotateInput. 조이스틱보다 하이어라키
        // 앞쪽(= 레이캐스트 우선순위상 뒤쪽)에 둬서, 조이스틱 영역의 드래그는 조이스틱이
        // 먼저 가로채고 나머지 화면만 회전 드래그로 잡히게 한다.
        static GameObject FindOrCreateDragCatcher(Canvas canvas)
        {
            // Find는 직계 자식만 찾는다 — 이 캐처가 Canvas 바로 밑이 아니라 중간 컨테이너
            // (예: "Root") 밑에 있을 수도 있으므로, 컴포넌트 기준으로 재귀 검색해야 이미
            // 있는 걸 매번 놓치고 중복 생성하는 일이 없다.
            var existingComponent = canvas.GetComponentInChildren<TouchRotateInput>(true);
            GameObject go;
            if (existingComponent != null)
            {
                go = existingComponent.gameObject;
            }
            else
            {
                go = new GameObject("RotateDragCatcher", typeof(RectTransform), typeof(Image), typeof(TouchRotateInput));
                go.transform.SetParent(canvas.transform, false);

                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                var image = go.GetComponent<Image>();
                image.color = new Color(0f, 0f, 0f, 0f);
                image.raycastTarget = true;
            }

            go.transform.SetAsFirstSibling();
            return go;
        }

        // 나무 프리팹엔 Collider가 없어 레이캐스트로 카메라-플레이어 사이를 가리는지
        // 감지할 수 없다 — 위치 기반으로 판정해서 가려지면 반투명하게 페이드시키는
        // TreeViewOcclusionFader를 Main Camera에 붙인다.
        void WirePlayerTreeOcclusionFader(GameObject player)
        {
            var cam = Camera.main;
            if (cam == null) return;

            var fader = cam.GetComponent<TreeViewOcclusionFader>();
            bool isNewFader = fader == null;
            if (isNewFader)
            {
                fader = cam.gameObject.AddComponent<TreeViewOcclusionFader>();
            }

            fader.player = player.transform;
            fader.vegetationScatter = Object.FindFirstObjectByType<VegetationScatter>();

            // 새로 추가한 경우 이번 프레임 안에 TreeViewOcclusionFader.Start()가 알아서
            // CollectTrees()를 한 번 불러준다 — 여기서 또 부르면 나무 수백 개를 두 번
            // 훑는 중복 비용이 든다(Play 진입이 느려지는 원인). 이미 있던(=Start()가
            // 벌써 지나간) 컴포넌트를 재사용할 때만 수동으로 다시 갱신해준다.
            if (!isNewFader)
            {
                fader.CollectTrees();
            }
        }
    }
}
