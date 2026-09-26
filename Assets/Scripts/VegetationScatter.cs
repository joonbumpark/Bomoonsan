using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mountains
{
    // 하나의 배치 규칙(풀, 바위, 눈 오브젝트 등)을 나타낸다.
    // 높이/경사도 조건(0~1, ProceduralTerrainMesh의 GetNormalizedHeightAt/GetSlopeAt 기준)에
    // 맞는 정점 위에서만 프리팹을 랜덤하게 골라 배치한다.
    [System.Serializable]
    public class ScatterGroup
    {
        public string label = "Grass";
        public GameObject[] prefabs;
        [Min(0)] public int count = 1000;

        [Header("배치 조건 (0=최저/평지, 1=최고/수직절벽)")]
        [Range(0f, 1f)] public float minHeight = 0f;
        [Range(0f, 1f)] public float maxHeight = 1f;
        [Range(0f, 1f)] public float minSlope = 0f;
        [Range(0f, 1f)] public float maxSlope = 1f;

        [Tooltip("켜두면 길(Path)로 판정된 자리에는 배치하지 않는다.")]
        public bool avoidPath = true;
        [Tooltip("켜두면 호수(Water Area)로 판정된 자리에는 배치하지 않는다.")]
        public bool avoidWater = true;
        [Tooltip("켜두면 이 그룹의 개체가 카메라와 플레이어 사이를 가릴 때 " +
            "TreeViewOcclusionFader가 반투명하게 페이드시킨다. 예전엔 fader 쪽에 라벨 목록을 " +
            "따로 두는 방식이라 Tree2 같은 그룹을 추가하면 양쪽을 다 맞춰야 했고, 한쪽을 " +
            "빠뜨리면 조용히 동작하지 않았다 — 그룹 정의 한 곳에서만 켜면 되도록 옮겼다.")]
        public bool fadeWhenOccludingPlayer = false;

        [Tooltip("배치된 개체에 붙일 Unity 태그. 비워두거나 Untagged면 태그를 건드리지 않는다. " +
            "프로젝트에 아직 없는 태그 이름을 적으면 에디터에서 Scatter할 때 자동으로 " +
            "TagManager에 등록된다 — 등록 안 된 태그를 그냥 대입하면 UnityException이 난다.")]
        public string instanceTag = "";

        [Header("충돌")]
        [Tooltip("켜두면 개체 밑동에 CapsuleCollider를 붙여 플레이어가 뚫고 지나가지 못하게 한다. " +
            "렌더러는 프리팹 것을 그대로 쓰고 콜라이더만 얹는 방식이라, 나중에 렌더링을 GPU " +
            "인스턴싱으로 옮기더라도 이 오브젝트와 콜라이더만 남겨두면 충돌은 그대로 동작한다. " +
            "정적 오브젝트라 Rigidbody 없이 콜라이더만 있어도 CharacterController가 알아서 막힌다.")]
        public bool blockPlayer = false;
        [Tooltip("충돌 캡슐의 반지름(월드 단위). 잎이 아니라 몸통만 막도록 작게 잡는다. " +
            "개체별 스케일은 Transform이 알아서 곱해준다.")]
        [Min(0.01f)] public float colliderRadius = 0.4f;
        [Tooltip("충돌 캡슐의 높이(월드 단위). 밑동(y=0)에서 위로 서도록 배치된다.")]
        [Min(0.01f)] public float colliderHeight = 4f;

        [Header("군락")]
        [Tooltip("0이면 지형 전체에 고르게 흩뿌린다(기본). 올릴수록 노이즈가 높은 자리에만 " +
            "몰려서 군락과 빈터가 생긴다 — 자연스러운 풍경은 대부분 이 밀도 대비에서 온다. " +
            "1이면 후보의 절반쯤만 살아남으므로 같은 개수를 유지하려면 count를 늘리거나, " +
            "이미 밀도 한계에 가까운 그룹(촘촘한 풀)은 개수가 줄어드는 걸 감수해야 한다.")]
        [Range(0f, 1f)] public float clusterAmount = 0f;
        [Tooltip("군락 하나의 대략적인 크기(월드 단위). 크게 잡으면 넓은 숲/초지 덩어리, " +
            "작게 잡으면 잔 얼룩이 된다.")]
        [Min(1f)] public float clusterScale = 40f;
        [Tooltip("그룹마다 다른 값을 주면 풀 군락과 나무 군락이 서로 다른 자리에 생긴다. " +
            "같은 값이면 같은 자리에 겹쳐서 난다.")]
        public int clusterSeed = 0;

        [Header("변형")]
        public bool alignToNormal = true;
        public Vector2 uniformScaleRange = new Vector2(0.85f, 1.15f);
        [Tooltip("높이(Y)만 추가로 ±이 비율만큼 더 흔든다. 0이면 균일 스케일 그대로. " +
            "같은 프리팹이어도 키가 달라 보여서 실루엣이 다양해진다.")]
        [Range(0f, 0.5f)] public float heightScaleVariation = 0f;

        [Header("색 변이")]
        [Tooltip("인스턴스마다 머티리얼 색조를 조금씩 다르게 준다. 0이면 끈다. " +
            "gpuInstanced 그룹에만 적용된다 — GameObject로 심는 그룹은 런타임에 만든 " +
            "머티리얼이 씬 파일에 통째로 저장돼버려서 제외한다.")]
        [Range(0f, 1f)] public float colorVariation = 0f;
        [Tooltip("만들 색 변이 개수. 많을수록 다양하지만 드로 배치가 그만큼 나뉜다.")]
        [Range(1, 8)] public int colorVariantCount = 3;

        [Header("성능")]
        [Tooltip("풀처럼 작고 개수가 많은 그룹은 꺼두면 그림자맵 비용이 크게 줄어든다.")]
        public bool castShadows = true;

        [Header("간격")]
        [Tooltip("같은 그룹의 다른 개체와 이만큼(월드 단위) 떨어지지 않으면 배치하지 않는다. " +
            "0이면 끈다. 다른 그룹과는 겹쳐도 상관없다 — 간격 검사는 그룹 안에서만 적용된다.")]
        [Min(0f)] public float minDistance = 0f;

        [Header("GPU Instancing")]
        [Tooltip("켜두면 GameObject를 만들지 않고 Graphics.DrawMeshInstanced로 그린다 — 수만~수십만 " +
            "개짜리 Grass처럼 콜라이더/개별 상호작용이 필요 없는 장식용 그룹에 적합하다. 실제 " +
            "GameObject/Transform이 없어서 blockPlayer/fadeWhenOccludingPlayer/instanceTag는 " +
            "무시된다(Scatter 시 경고 로그). 에디터 Scene 뷰(Play 안 한 상태)에는 안 보이고 Play " +
            "모드에서만 그려진다.")]
        public bool gpuInstanced = false;
    }

    [RequireComponent(typeof(ProceduralTerrainMesh))]
    public class VegetationScatter : MonoBehaviour
    {
        [Tooltip("비워두면 같은 오브젝트의 ProceduralTerrainMesh를 자동으로 사용한다.")]
        public ProceduralTerrainMesh terrain;

        [Tooltip("배치 규칙 에셋. 비어있으면 Mountains > Create Procedural Terrain 실행 시 자동으로 " +
            "만들어진다. 여기 담긴 값은 다시 배치해도 초기화되지 않는다.")]
        public VegetationScatterSettings settings;

        public bool scatterOnStart = true;

        // settings의 값을 그대로 노출하는 읽기 전용 프로퍼티. ScatterGroupInternal 등 본문
        // 코드는 지금까지처럼 groups/seed/densityMultiplier를 그대로 참조하면 된다.
        public ScatterGroup[] groups => settings != null ? settings.groups : System.Array.Empty<ScatterGroup>();
        public int seed => settings != null ? settings.seed : 0;
        public float densityMultiplier => settings != null ? settings.densityMultiplier : 1f;

        Transform _container;

        // gpuInstanced 그룹의 렌더 데이터. Scatter()가 채우고 Update()가 매 프레임 그린다.
        readonly Dictionary<ScatterGroup, GpuInstancedGroup> _gpuGroups = new Dictionary<ScatterGroup, GpuInstancedGroup>();

        // 색 변이로 만든 복제 머티리얼 캐시(원본 머티리얼 -> 변이들). 원본은 절대 건드리지 않는다.
        readonly Dictionary<Material, Material[]> _colorVariants = new Dictionary<Material, Material[]>();

        // 흔들 색 프로퍼티. 앞 둘은 PT 식생 셰이더의 그라디언트 색이고, 뒤 둘은 URP Lit 등
        // 일반 셰이더용이다 — 없는 프로퍼티는 건너뛰므로 전부 나열해도 안전하다.
        static readonly string[] ColorProperties = { "_TopColor", "_GroundColor", "_BaseColor", "_Color" };
        Camera _mainCamera;

        // 어떤 인스턴스가 어느 그룹에서 나왔는지는 런타임 리스트가 아니라 인스턴스
        // 이름의 접미사([Grass], [Tree] 등)로 기록한다 — 처음엔 List<(label, Transform)>로
        // 들고 있었는데, Play 모드에 들어갈 때 도메인 리로드가 일어나면 직렬화 안 되는
        // 필드라 통째로 비어버려서 TreeViewOcclusionFader가 늘 빈 목록을 받는 버그가
        // 있었다. 이름은 GameObject 자체에 저장되니(씬에 실제로 저장됨) 도메인 리로드와
        // 무관하게 항상 정확하다.
        static string LabelSuffix(string label) => $"[{label}]";

        // 런타임 중 다시 배치(Scatter)/비우기(Clear)하면 기존 인스턴스가 전부 파괴되고
        // 새로 생긴다. TreeViewOcclusionFader처럼 스폰된 Transform을 들고 있는 컴포넌트가
        // 파괴된 참조에 접근해 MissingReferenceException을 내지 않도록, 목록이 바뀔
        // 때마다 알려서 다시 수집(CollectTrees 등)하게 한다.
        public event System.Action InstancesChanged;

        // label과 일치하는(대소문자 무시) 그룹에서 스폰된 인스턴스의 Transform 목록.
        public IReadOnlyList<Transform> GetInstancesByLabel(string label)
        {
            var result = new List<Transform>();
            if (_container == null)
            {
                return result;
            }

            string suffix = LabelSuffix(label);
            foreach (Transform child in _container)
            {
                if (child.name.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(child);
                }
            }
            return result;
        }

        // fadeWhenOccludingPlayer가 켜진 모든 그룹의 인스턴스. 페이드 대상 목록을 그룹
        // 정의에서 직접 끌어오므로, 그룹을 추가/삭제해도 fader 쪽에 따로 등록할 필요가 없다.
        public IReadOnlyList<Transform> GetInstancesToFadeWhenOccluding()
        {
            var result = new List<Transform>();
            foreach (var group in groups)
            {
                if (group == null || !group.fadeWhenOccludingPlayer)
                {
                    continue;
                }
                result.AddRange(GetInstancesByLabel(group.label));
            }
            return result;
        }

        void Awake()
        {
            if (terrain == null)
            {
                terrain = GetComponent<ProceduralTerrainMesh>();
            }
            FindOrCreateContainer();
        }

        void Start()
        {
            if (scatterOnStart)
            {
                Scatter();
            }
            else
            {
                // gpuInstanced 그룹은 GameObject를 하나도 안 만들어서 씬 파일에 저장되는 게
                // 없다 — Tree/Rock처럼 이미 씬에 저장된 GameObject 그룹과 달리, 씬을 새로
                // 열 때마다(Play 재진입 포함) _gpuGroups를 다시 채워야 화면에 나온다.
                // scatterOnStart가 꺼져 있어도(TerrainSceneSetup 기본값 — GameObject 그룹을
                // 매번 다시 Instantiate하지 않으려고 꺼둔 것) 이 부분만은 따로 해준다.
                RebuildGpuInstancedGroups();
            }
        }

        // gpuInstanced 그룹만 골라 다시 스캐터한다(GameObject 그룹은 손대지 않음). 씬을
        // 새로 로드할 때마다(Start) 호출해서, 저장되지 않는 GPU 인스턴싱 렌더 데이터를
        // 다시 만들어준다.
        public void RebuildGpuInstancedGroups()
        {
            if (terrain == null || settings == null)
            {
                return;
            }

            bool hasGpuGroup = false;
            foreach (var group in groups)
            {
                if (group != null && group.gpuInstanced)
                {
                    hasGpuGroup = true;
                    break;
                }
            }
            if (!hasGpuGroup)
            {
                return;
            }

            _gpuGroups.Clear();
            ReleaseColorVariants();

            var rng = new System.Random(seed);
            foreach (var group in groups)
            {
                if (group != null && group.gpuInstanced)
                {
                    ScatterGroupInternal(group, rng);
                }
            }
        }

        // gpuInstanced 그룹이 하나도 없으면(대부분의 씬에서 그렇다) 즉시 리턴이라 오버헤드가
        // 없다. Camera.main은 한 번만 찾아서 캐싱 — 이벤트 카메라로 잠깐 전환되는 동안에도
        // Main Camera 오브젝트 자체는 그대로라 다시 찾을 필요가 없다.
        void Update()
        {
            if (_gpuGroups.Count == 0)
            {
                return;
            }

            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null)
                {
                    return;
                }
            }

            Vector3 camPos = _mainCamera.transform.position;
            float maxDrawDistance = settings != null ? settings.instancingMaxDrawDistance : 150f;

            foreach (var gpuGroup in _gpuGroups.Values)
            {
                gpuGroup.Draw(camPos, maxDrawDistance);
            }
        }

        public void Scatter()
        {
            if (terrain == null)
            {
                Debug.LogWarning("[VegetationScatter] terrain이 지정되지 않아 배치를 건너뜁니다.");
                return;
            }

            if (settings == null)
            {
                Debug.LogWarning("[VegetationScatter] settings가 할당되지 않아 배치를 건너뜁니다. " +
                    "Mountains > Create Procedural Terrain을 실행하면 자동으로 만들어집니다.");
                return;
            }

            FindOrCreateContainer();
            ClearInstances();
            _gpuGroups.Clear();
            ReleaseColorVariants();

#if UNITY_EDITOR
            // 그룹에 적어둔 태그가 아직 프로젝트에 없으면 여기서 등록해둔다. 배치 도중
            // 없는 태그를 대입하면 개체마다 UnityException이 터지기 때문에, 개체를 만들기
            // 전에 한 번에 처리한다. (에디터 배치 기준 — 빌드에서는 이미 등록된 상태다.)
            foreach (var group in groups)
            {
                if (group != null)
                {
                    EnsureTagExists(group.instanceTag);
                }
            }
#endif

            var rng = new System.Random(seed);
            foreach (var group in groups)
            {
                ScatterGroupInternal(group, rng);
            }
        }

#if UNITY_EDITOR
        // TagManager.asset을 직접 편집하면 들여쓰기/빈 항목 형식이 조금만 어긋나도 Unity가
        // 조용히 파싱에 실패하므로, SerializedObject로 정식 API를 통해 추가한다.
        static void EnsureTagExists(string tag)
        {
            if (string.IsNullOrEmpty(tag) || tag == "Untagged")
            {
                return;
            }

            foreach (var existing in UnityEditorInternal.InternalEditorUtility.tags)
            {
                if (existing == tag)
                {
                    return;
                }
            }

            var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning($"[VegetationScatter] TagManager를 열 수 없어 '{tag}' 태그를 등록하지 못했습니다.");
                return;
            }

            var tagManager = new UnityEditor.SerializedObject(assets[0]);
            var tagsProperty = tagManager.FindProperty("tags");
            tagsProperty.InsertArrayElementAtIndex(tagsProperty.arraySize);
            tagsProperty.GetArrayElementAtIndex(tagsProperty.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedProperties();

            Debug.Log($"[VegetationScatter] '{tag}' 태그를 프로젝트에 새로 등록했습니다.");
        }
#endif

        public void ClearInstances()
        {
            if (_container == null)
            {
                FindOrCreateContainer();
            }

            for (int i = _container.childCount - 1; i >= 0; i--)
            {
                var child = _container.GetChild(i).gameObject;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(child);
                    continue;
                }
#endif
                Destroy(child);
            }

            NotifyInstancesChanged();
        }

        // 런타임의 Destroy()는 이번 프레임 끝에야 실제로 처리된다. 지금 당장 알리면
        // 구독자가 다시 목록을 읽을 때 파괴 예약된 옛 인스턴스가 아직 자식 목록에 남아
        // 있어 그대로 섞여 잡힌다 — 한 프레임 미뤄서 실제로 사라진 뒤에 알린다. 에디터에서
        // 재생 중이 아닐 때는 DestroyImmediate라 즉시 알려도 안전하다. Scatter()는
        // 내부에서 ClearInstances()를 먼저 부른 뒤 같은 프레임에 동기적으로 새 인스턴스를
        // 마저 배치하므로, 이 한 번의 지연 알림이 최종(새로 배치된) 상태를 정확히 반영한다.
        void NotifyInstancesChanged()
        {
            if (Application.isPlaying)
            {
                StartCoroutine(NotifyInstancesChangedNextFrame());
            }
            else
            {
                InstancesChanged?.Invoke();
            }
        }

        IEnumerator NotifyInstancesChangedNextFrame()
        {
            yield return null;
            InstancesChanged?.Invoke();
        }

        void ScatterGroupInternal(ScatterGroup group, System.Random rng)
        {
            int targetCount = Mathf.RoundToInt(group.count * densityMultiplier);
            if (group.prefabs == null || group.prefabs.Length == 0 || targetCount <= 0)
            {
                return;
            }

            bool hasIncompatibleFlags = group.blockPlayer || group.fadeWhenOccludingPlayer
                || (!string.IsNullOrEmpty(group.instanceTag) && group.instanceTag != "Untagged");
            if (group.gpuInstanced && hasIncompatibleFlags)
            {
                Debug.LogWarning($"[VegetationScatter] '{group.label}' 그룹: gpuInstanced는 실제 GameObject가 " +
                    "없어 blockPlayer/fadeWhenOccludingPlayer/instanceTag를 지원하지 않습니다 — 무시합니다.");
            }

            if (!group.gpuInstanced && group.colorVariation > 0f)
            {
                Debug.LogWarning($"[VegetationScatter] '{group.label}' 그룹: 색 변이는 gpuInstanced 그룹에만 " +
                    "적용됩니다 — GameObject로 심는 그룹은 런타임에 만든 머티리얼이 씬 파일에 통째로 " +
                    "저장돼버립니다. 색을 다양하게 하려면 프리팹(머티리얼)을 여러 개 넣으세요.");
            }

            GpuInstancedGroup gpuGroup = group.gpuInstanced
                ? new GpuInstancedGroup(group.prefabs, group.castShadows, ResolveVariantCount(group),
                    (material, variantIndex) => ResolveColorVariant(material, variantIndex, group.colorVariation))
                : null;

            // minDistance가 있으면(대부분의 그룹) 격자 칸당 하나씩 심는 방식으로 최소 간격을
            // 구조적으로 보장한다 — "랜덤 위치 찍고 주변 검사, 실패하면 재시도" 방식은 목표
            // 개수가 밀도 한계에 가까워질수록(예: 좁은 지형에 minDistance 촘촘하게) 재시도가
            // 기하급수적으로 늘어나 Play 진입이 몇 초씩 느려졌다(Profiler로 확인 — 간격 검사가
            // 130만 번 넘게 호출됨). minDistance가 없는 그룹은 그럴 걱정이 없으니 기존 방식 그대로 둔다.
            int placed = group.minDistance > 0f
                ? ScatterOnJitteredGrid(group, rng, gpuGroup, targetCount)
                : ScatterByRandomSampling(group, rng, gpuGroup, targetCount);

            if (gpuGroup != null)
            {
                float chunkSize = settings != null ? settings.instancingChunkSize : 25f;
                gpuGroup.BuildChunks(chunkSize);
                _gpuGroups[group] = gpuGroup;
            }

            if (placed < targetCount)
            {
                Debug.LogWarning($"[VegetationScatter] '{group.label}' 그룹: 조건에 맞는 자리를 충분히 찾지 못해 {placed}/{targetCount}개만 배치했습니다. 높이/경사 범위를 넓혀보세요.");
            }
        }

        // 격자 칸 크기를 minDistance로 잡고, 칸을 무작위 순서로 방문하며 칸당 한 번만
        // 시도한다(자리를 못 찾으면 그 칸은 그냥 포기) — 같은 그룹 안에서 min거리 이상
        // 벌어지는 걸 배치 구조 자체로 보장하므로, 근접 검사를 반복할 필요가 없다. 시도
        // 횟수가 셀 개수(≈ 목표 밀도의 역수)에 비례할 뿐, 예전 랜덤 재시도 방식처럼
        // 밀도가 높아질수록 기하급수적으로 늘어나지 않는다.
        int ScatterOnJitteredGrid(ScatterGroup group, System.Random rng, GpuInstancedGroup gpuGroup, int targetCount)
        {
            float cellSizeInGrid = Mathf.Max(group.minDistance / terrain.cellSize, 0.01f);
            int cellsX = Mathf.Max(1, Mathf.CeilToInt(terrain.width / cellSizeInGrid));
            int cellsZ = Mathf.Max(1, Mathf.CeilToInt(terrain.length / cellSizeInGrid));

            // 셀이 지나치게 많아지면(아주 촘촘한 minDistance) 배열이 너무 커지므로 상한을
            // 둔다 — 단, 인덱스 범위 자체를 잘라내면(예전 버그) 격자의 앞쪽 일부 구역만
            // 계속 방문하게 돼서 지형 한쪽에만 배치가 몰린다. 그 대신 셀 크기를 두 축
            // 동일한 비율로 키워서, 격자가 항상 지형 전체를 덮으면서 칸 개수만 줄게 한다.
            long totalCellsLong = (long)cellsX * cellsZ;
            long maxTotalCells = System.Math.Max(targetCount * 20L, 4096L);
            if (totalCellsLong > maxTotalCells)
            {
                float scale = Mathf.Sqrt((float)(totalCellsLong / (double)maxTotalCells));
                cellSizeInGrid *= scale;
                cellsX = Mathf.Max(1, Mathf.CeilToInt(terrain.width / cellSizeInGrid));
                cellsZ = Mathf.Max(1, Mathf.CeilToInt(terrain.length / cellSizeInGrid));
            }

            int totalCells = cellsX * cellsZ;
            var order = new int[totalCells];
            for (int i = 0; i < totalCells; i++)
            {
                order[i] = i;
            }

            int placed = 0;
            for (int i = 0; i < totalCells && placed < targetCount; i++)
            {
                // 부분 Fisher-Yates — targetCount만큼 채워지면 멈추므로 전체를 다 섞을
                // 필요가 없다.
                int swapIndex = i + rng.Next(0, totalCells - i);
                (order[i], order[swapIndex]) = (order[swapIndex], order[i]);

                int cellIndex = order[i];
                int cx = cellIndex % cellsX;
                int cz = cellIndex / cellsX;

                float gx = Mathf.Clamp((cx + (float)rng.NextDouble()) * cellSizeInGrid, 0f, terrain.width - 1);
                float gz = Mathf.Clamp((cz + (float)rng.NextDouble()) * cellSizeInGrid, 0f, terrain.length - 1);

                if (TryPlaceInstance(group, gpuGroup, gx, gz, rng))
                {
                    placed++;
                }
            }
            return placed;
        }

        // minDistance가 없는(간격 제한이 필요 없는) 그룹은 예전처럼 순수 랜덤 시도로도
        // 충분히 빠르다 — 재시도가 늘어나는 원인(근접 검사)이 애초에 없기 때문.
        int ScatterByRandomSampling(ScatterGroup group, System.Random rng, GpuInstancedGroup gpuGroup, int targetCount)
        {
            int maxAttempts = targetCount * 20;
            int placed = 0;
            int attempts = 0;

            while (placed < targetCount && attempts < maxAttempts)
            {
                attempts++;

                int x = rng.Next(0, terrain.width);
                int z = rng.Next(0, terrain.length);

                // 정점 위치에 딱 붙이면 10m 격자 위에 정렬된 것처럼 규칙적으로 보이므로,
                // 격자 한 칸(±0.5) 안에서 흔들어서 심는다.
                float gx = x + (float)(rng.NextDouble() - 0.5);
                float gz = z + (float)(rng.NextDouble() - 0.5);

                if (TryPlaceInstance(group, gpuGroup, gx, gz, rng))
                {
                    placed++;
                }
            }
            return placed;
        }

        // 높이/경사/길/물 조건을 검사하고 통과하면 실제로 인스턴스를 심는다(GameObject 또는
        // GPU 인스턴싱 매트릭스). 그리드 방식과 랜덤 방식이 공유하는 판정 로직.
        bool TryPlaceInstance(ScatterGroup group, GpuInstancedGroup gpuGroup, float gx, float gz, System.Random rng)
        {
            int ix = Mathf.Clamp(Mathf.RoundToInt(gx), 0, terrain.width - 1);
            int iz = Mathf.Clamp(Mathf.RoundToInt(gz), 0, terrain.length - 1);

            float normalizedHeight = terrain.GetNormalizedHeightAt(ix, iz);
            if (normalizedHeight < group.minHeight || normalizedHeight > group.maxHeight)
            {
                return false;
            }

            float slope = terrain.GetSlopeAt(ix, iz);
            if (slope < group.minSlope || slope > group.maxSlope)
            {
                return false;
            }

            // 0.2 임계값: 길 경계의 부드러운 블렌드 구간까지 전부 막으면 식생이 길에서
            // 너무 멀찍이 떨어져 보이므로, 길 중심에 가까운 자리만 확실히 피한다.
            if (group.avoidPath && terrain.GetPathMaskAt(gx, gz) > 0.2f)
            {
                return false;
            }

            // 길과 달리 "깊이" 마스크가 아니라 순수 안/밖 판정을 쓴다 — 호수가
            // waterShoreBlendWidth보다 작으면 내부에서도 마스크 값이 거의 0에 머물러
            // 문턱값(threshold) 기반 회피가 걸리지 않는 경우가 있었다(작은 호수 위에도
            // 나무가 생기던 버그). 폴리곤 안쪽이면 크기와 무관하게 항상 피한다.
            if (group.avoidWater && terrain.IsInsideWaterArea(gx, gz))
            {
                return false;
            }

            if (!PassesClusterMask(group, gx, gz, rng))
            {
                return false;
            }

            if (gpuGroup != null)
            {
                SpawnGpuInstance(group, gpuGroup, gx, gz, rng);
            }
            else
            {
                SpawnInstance(group, gx, gz, rng);
            }
            return true;
        }

        // 군락 마스크 — 후보 자리가 노이즈로 정해진 "군락 안"인지 확률적으로 판정한다.
        // 격자 배치(ScatterOnJitteredGrid)는 칸당 하나라 간격이 구조적으로 균일해서, 그대로
        // 두면 지형 어디를 봐도 같은 밀도가 된다. 여기서 확률을 흔들어 군락과 빈터를 만든다.
        bool PassesClusterMask(ScatterGroup group, float gx, float gz, System.Random rng)
        {
            if (group.clusterAmount <= 0f)
            {
                return true;
            }

            // 격자 좌표가 아니라 월드 좌표로 노이즈를 뽑는다 — 지형 해상도(cellSize)를 바꿔도
            // 군락 크기가 그대로 유지되게 하기 위해서다. clusterSeed는 그룹마다 노이즈 위상을
            // 어긋나게 해서 풀 군락과 나무 군락이 같은 자리에 겹치지 않게 한다.
            float scale = Mathf.Max(1f, group.clusterScale);
            float nx = (gx * terrain.cellSize + group.clusterSeed * 131.7f) / scale;
            float nz = (gz * terrain.cellSize + group.clusterSeed * 317.3f) / scale;

            // SmoothStep으로 대비를 올려서 "확실한 군락 / 확실한 빈터"가 생기게 한다.
            // 평균은 0.5 근처라, clusterAmount=1이면 후보의 절반 정도가 살아남는다.
            float mask = Mathf.SmoothStep(0f, 1f, Mathf.PerlinNoise(nx, nz));
            float density = Mathf.Lerp(1f, mask, group.clusterAmount);
            return rng.NextDouble() < density;
        }

        // 위치(지형 높이)/회전(경사 정렬 + 랜덤 요)/스케일(균일 랜덤 배율)을 계산한다.
        // GameObject로 심든(SpawnInstance) GPU 인스턴싱 매트릭스로 쌓든(SpawnGpuInstance)
        // 같은 규칙을 써야 하므로 공용 헬퍼로 뺐다.
        (Vector3 position, Quaternion rotation, Vector3 scale) ComputeInstanceTransform(
            GameObject prefab, ScatterGroup group, float gx, float gz, System.Random rng)
        {
            Vector3 position = terrain.GetWorldPositionAt(gx, gz);
            Vector3 normal = terrain.GetNormalAt(gx, gz);

            Quaternion slopeRotation = group.alignToNormal ? Quaternion.FromToRotation(Vector3.up, normal) : Quaternion.identity;
            float yaw = (float)(rng.NextDouble() * 360.0);
            Quaternion rotation = slopeRotation * Quaternion.Euler(0f, yaw, 0f);

            // prefab 자체에 이미 들어있는 비율(예: 바위의 찌그러진 모양)은 유지한 채
            // 개체별로 균일한 스케일만 곱한다.
            float scale = Mathf.Lerp(group.uniformScaleRange.x, group.uniformScaleRange.y, (float)rng.NextDouble());
            Vector3 finalScale = prefab.transform.localScale * scale;

            // 높이만 따로 한 번 더 흔든다 — 균일 배율만 쓰면 큰 개체는 그냥 "확대된 작은 개체"라
            // 실루엣이 전부 닮아 보인다. 키 비율이 달라지면 같은 프리팹도 다른 개체처럼 읽힌다.
            if (group.heightScaleVariation > 0f)
            {
                finalScale.y *= 1f + ((float)rng.NextDouble() * 2f - 1f) * group.heightScaleVariation;
            }

            return (position, rotation, finalScale);
        }

        // gx/gz는 이미 지터링까지 끝난 실수 격자 좌표다(ScatterGroupInternal에서 avoidPath
        // 판정에 쓴 것과 동일한 위치) — 여기서 다시 흔들면 검사한 위치와 실제로 심는 위치가
        // 어긋나버린다.
        void SpawnInstance(ScatterGroup group, float gx, float gz, System.Random rng)
        {
            GameObject prefab = group.prefabs[rng.Next(group.prefabs.Length)];
            if (prefab == null)
            {
                return;
            }

            var t = ComputeInstanceTransform(prefab, group, gx, gz, rng);

            GameObject instance = InstantiatePrefab(prefab);
            instance.transform.SetPositionAndRotation(t.position, t.rotation);
            instance.transform.localScale = t.scale;
            instance.name += LabelSuffix(group.label);

            if (!string.IsNullOrEmpty(group.instanceTag) && group.instanceTag != "Untagged")
            {
                instance.tag = group.instanceTag;
            }

            if (!group.castShadows)
            {
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }

            if (group.blockPlayer)
            {
                AddBlockingCollider(instance, group);
            }
        }

        // GameObject를 전혀 만들지 않고 매트릭스만 GpuInstancedGroup에 쌓아둔다 — 실제
        // 그리기는 VegetationScatter.Update()가 매 프레임 Graphics.DrawMeshInstanced로 한다.
        void SpawnGpuInstance(ScatterGroup group, GpuInstancedGroup gpuGroup, float gx, float gz, System.Random rng)
        {
            int prefabIndex = rng.Next(group.prefabs.Length);
            GameObject prefab = group.prefabs[prefabIndex];
            if (prefab == null)
            {
                return;
            }

            var t = ComputeInstanceTransform(prefab, group, gx, gz, rng);
            Matrix4x4 matrix = Matrix4x4.TRS(t.position, t.rotation, t.scale);

            int variantIndex = gpuGroup.VariantCount > 1 ? rng.Next(gpuGroup.VariantCount) : 0;
            gpuGroup.AddInstance(gpuGroup.DrawIndex(prefabIndex, variantIndex), t.position, matrix);
        }

        static int ResolveVariantCount(ScatterGroup group)
        {
            return group.colorVariation > 0f ? Mathf.Max(1, group.colorVariantCount) : 1;
        }

        // 변이 0번은 원본 머티리얼 그대로다 — 최소한 한 벌은 에셋과 똑같이 보이게 두고,
        // 나머지만 색조를 흔든 복제본을 만든다. 같은 머티리얼을 여러 프리팹이 공유해도
        // 복제본은 한 벌만 만들어 재사용한다.
        Material ResolveColorVariant(Material source, int variantIndex, float variation)
        {
            if (source == null || variantIndex <= 0 || variation <= 0f)
            {
                return source;
            }

            if (!_colorVariants.TryGetValue(source, out var variants) || variants.Length <= variantIndex)
            {
                var resized = new Material[variantIndex + 1];
                if (variants != null)
                {
                    variants.CopyTo(resized, 0);
                }
                variants = resized;
                _colorVariants[source] = variants;
            }

            if (variants[variantIndex] == null)
            {
                variants[variantIndex] = CreateColorVariant(source, variantIndex, variation);
            }
            return variants[variantIndex];
        }

        static Material CreateColorVariant(Material source, int variantIndex, float variation)
        {
            // 시드를 머티리얼 이름 + 변이 번호로 고정해서, 다시 배치해도 같은 색이 나오게 한다.
            var rng = new System.Random(source.name.GetHashCode() * 397 + variantIndex);
            var variant = new Material(source)
            {
                name = $"{source.name} (Variant {variantIndex})",
                // 런타임 전용 오브젝트다 — 씬/에셋에 저장되지 않게 명시한다.
                hideFlags = HideFlags.HideAndDontSave
            };

            foreach (var property in ColorProperties)
            {
                if (variant.HasProperty(property))
                {
                    variant.SetColor(property, JitterColor(variant.GetColor(property), rng, variation));
                }
            }

            return variant;
        }

        static Color JitterColor(Color source, System.Random rng, float amount)
        {
            Color.RGBToHSV(source, out float h, out float s, out float v);

            h = Mathf.Repeat(h + NextSigned(rng) * 0.04f * amount, 1f);
            s = Mathf.Clamp01(s * (1f + NextSigned(rng) * 0.3f * amount));
            // v는 Clamp01하지 않는다 — PT 식생 머티리얼의 색은 [HDR]이라 1을 넘을 수 있고,
            // 잘라내면 원래보다 어두워져 버린다.
            v *= 1f + NextSigned(rng) * 0.3f * amount;

            var result = Color.HSVToRGB(h, s, v, true);
            result.a = source.a;
            return result;
        }

        static float NextSigned(System.Random rng)
        {
            return (float)(rng.NextDouble() * 2.0 - 1.0);
        }

        // 런타임에 만든 변이 머티리얼은 GC가 정리해주지 않는다 — 다시 배치할 때마다
        // 쌓이지 않게 직접 파괴한다.
        void ReleaseColorVariants()
        {
            foreach (var variants in _colorVariants.Values)
            {
                foreach (var variant in variants)
                {
                    if (variant == null)
                    {
                        continue;
                    }
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        DestroyImmediate(variant);
                        continue;
                    }
#endif
                    Destroy(variant);
                }
            }

            _colorVariants.Clear();
        }

        // 프리팹(PT_Pine_Tree 등)엔 Collider가 아예 없어서 플레이어가 나무를 그냥 통과한다.
        // 메시 콜라이더는 잎까지 전부 막아 답답하고 비용도 크므로, 몸통만 덮는 캡슐 하나만
        // 붙인다. 개체별 uniformScaleRange는 Transform 스케일에 이미 반영돼 있어서 캡슐도
        // 같이 스케일된다 — 여기서 따로 곱하지 않는다.
        //
        // 나중에 길찾기를 붙일 때: 예전 방식인 StaticEditorFlags.NavigationStatic은
        // deprecated라 쓰지 않는다. 새 AI Navigation 패키지의 NavMeshSurface는 정적 플래그가
        // 아니라 "Use Geometry: Physics Colliders" + Include Layers로 수집하므로, 여기서
        // 붙이는 이 콜라이더만으로 별도 표시 없이 장애물로 잡힌다.
        static void AddBlockingCollider(GameObject instance, ScatterGroup group)
        {
            var capsule = instance.AddComponent<CapsuleCollider>();
            capsule.radius = group.colliderRadius;
            capsule.height = group.colliderHeight;
            // 캡슐 원점은 중심이므로, 밑동(y=0)에서 위로 서게 하려면 절반만큼 올린다.
            capsule.center = new Vector3(0f, group.colliderHeight * 0.5f, 0f);
        }

        GameObject InstantiatePrefab(GameObject prefab)
        {
#if UNITY_EDITOR
            // 실제 프리팹 에셋이면 에디터에서 프리팹 연결(파란 링크)이 유지되도록 인스턴스화한다.
            // 저장되지 않은 임시 오브젝트가 들어오면(과거 호환) 일반 Instantiate로 대체한다.
            if (!Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabAsset(prefab))
            {
                return (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, _container);
            }
#endif
            return Instantiate(prefab, _container);
        }

        void FindOrCreateContainer()
        {
            if (_container != null)
            {
                return;
            }

            var existing = transform.Find("ScatteredInstances");
            if (existing == null)
            {
                var go = new GameObject("ScatteredInstances");
                go.transform.SetParent(transform, false);
                existing = go.transform;
            }
            _container = existing;
        }
    }
}
