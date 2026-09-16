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

        [Header("변형")]
        public bool alignToNormal = true;
        public Vector2 uniformScaleRange = new Vector2(0.85f, 1.15f);

        [Header("성능")]
        [Tooltip("풀처럼 작고 개수가 많은 그룹은 꺼두면 그림자맵 비용이 크게 줄어든다.")]
        public bool castShadows = true;
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

            int placed = 0;
            // 조건(높이/경사)을 만족하는 지점이 드물 수 있으므로 목표 개수보다 넉넉하게 시도한다.
            int maxAttempts = targetCount * 20;
            int attempts = 0;

            while (placed < targetCount && attempts < maxAttempts)
            {
                attempts++;

                int x = rng.Next(0, terrain.width);
                int z = rng.Next(0, terrain.length);

                float normalizedHeight = terrain.GetNormalizedHeightAt(x, z);
                if (normalizedHeight < group.minHeight || normalizedHeight > group.maxHeight)
                {
                    continue;
                }

                float slope = terrain.GetSlopeAt(x, z);
                if (slope < group.minSlope || slope > group.maxSlope)
                {
                    continue;
                }

                // 정점 위치에 딱 붙이면 10m 격자 위에 정렬된 것처럼 규칙적으로 보이므로,
                // 격자 한 칸(±0.5) 안에서 미리 흔들어둔다. avoidPath는 반드시 이 흔든
                // 위치 기준으로 검사해야 한다 — 정수 격자점만 검사하면 흔든 위치가 실제로는
                // 길 안쪽으로 들어가 있어도 못 걸러내서 길 위에 나무가 생기는 문제가 있었다.
                float jitterX = x + (float)(rng.NextDouble() - 0.5);
                float jitterZ = z + (float)(rng.NextDouble() - 0.5);

                // 0.2 임계값: 길 경계의 부드러운 블렌드 구간까지 전부 막으면 식생이 길에서
                // 너무 멀찍이 떨어져 보이므로, 길 중심에 가까운 자리만 확실히 피한다.
                if (group.avoidPath && terrain.GetPathMaskAt(jitterX, jitterZ) > 0.2f)
                {
                    continue;
                }

                // 길과 달리 "깊이" 마스크가 아니라 순수 안/밖 판정을 쓴다 — 호수가
                // waterShoreBlendWidth보다 작으면 내부에서도 마스크 값이 거의 0에 머물러
                // 문턱값(threshold) 기반 회피가 걸리지 않는 경우가 있었다(작은 호수 위에도
                // 나무가 생기던 버그). 폴리곤 안쪽이면 크기와 무관하게 항상 피한다.
                if (group.avoidWater && terrain.IsInsideWaterArea(jitterX, jitterZ))
                {
                    continue;
                }

                SpawnInstance(group, jitterX, jitterZ, rng);
                placed++;
            }

            if (placed < targetCount)
            {
                Debug.LogWarning($"[VegetationScatter] '{group.label}' 그룹: 조건에 맞는 자리를 충분히 찾지 못해 {placed}/{targetCount}개만 배치했습니다. 높이/경사 범위를 넓혀보세요.");
            }
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

            Vector3 position = terrain.GetWorldPositionAt(gx, gz);
            Vector3 normal = terrain.GetNormalAt(gx, gz);

            GameObject instance = InstantiatePrefab(prefab);
            instance.transform.position = position;
            instance.name += LabelSuffix(group.label);

            if (!string.IsNullOrEmpty(group.instanceTag) && group.instanceTag != "Untagged")
            {
                instance.tag = group.instanceTag;
            }

            Quaternion slopeRotation = group.alignToNormal ? Quaternion.FromToRotation(Vector3.up, normal) : Quaternion.identity;
            float yaw = (float)(rng.NextDouble() * 360.0);
            instance.transform.rotation = slopeRotation * Quaternion.Euler(0f, yaw, 0f);

            // prefab 자체에 이미 들어있는 비율(예: 바위의 찌그러진 모양)은 유지한 채
            // 개체별로 균일한 스케일만 곱한다.
            float scale = Mathf.Lerp(group.uniformScaleRange.x, group.uniformScaleRange.y, (float)rng.NextDouble());
            instance.transform.localScale = prefab.transform.localScale * scale;

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
