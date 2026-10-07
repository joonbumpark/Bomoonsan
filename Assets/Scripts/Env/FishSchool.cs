using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mountains.Env
{
    // 물고기 무리. GameObject를 만들지 않고 배열로 상태를 들고 있다가 Burst 잡으로 갱신하고,
    // GPU 인스턴싱으로 종류당 한 번에 그린다.
    //
    // 예전 FishManager는 마리당 GameObject + MonoBehaviour였고 이웃 탐색이 O(n²)였다.
    // 여기서는 격자로 이웃을 좁히고(잡 주석 참고), Transform 컴포넌트도 쓰지 않는다.
    public class FishSchool : MonoBehaviour
    {
        [Tooltip("무리 설정 에셋. 비어 있으면 아무것도 스폰하지 않는다. 컴포넌트 우클릭 > " +
            "설정 에셋 만들기로 씬 이름에 맞춰 생성할 수 있다.")]
        public FishSchoolSettings settings;

        // settings의 값을 그대로 노출하는 읽기 전용 프로퍼티. 본문 코드는 지금까지처럼
        // spawnCount/maxSpeed를 그대로 참조하면 된다(VegetationScatter와 같은 방식).
        public bool spawnFish => settings != null && settings.spawnFish;
        public FishSpecies[] species => settings != null ? settings.species : System.Array.Empty<FishSpecies>();
        public int spawnCount => settings != null ? settings.spawnCount : 0;
        public Vector2 boundsSize => settings != null ? settings.boundsSize : new Vector2(40f, 25f);
        public bool useTransformHeight => settings == null || settings.useTransformHeight;
        public float waterHeight => settings != null ? settings.waterHeight : 0f;
        public float maxSpeed => settings != null ? settings.maxSpeed : 5f;
        public float minSpeed => settings != null ? settings.minSpeed : 1.5f;
        public float maxForce => settings != null ? settings.maxForce : 3f;
        public float turnSpeed => settings != null ? settings.turnSpeed : 6f;
        public float perceptionRadius => settings != null ? settings.perceptionRadius : 6f;
        public float separationRadius => settings != null ? settings.separationRadius : 2.4f;
        public float fovAngle => settings != null ? settings.fovAngle : 270f;
        public float alignWeight => settings != null ? settings.alignWeight : 1f;
        public float cohereWeight => settings != null ? settings.cohereWeight : 1f;
        public float separateWeight => settings != null ? settings.separateWeight : 1.5f;
        public float boundsWeight => settings != null ? settings.boundsWeight : 4f;
        public ShadowCastingMode shadowCastingMode =>
            settings != null ? settings.shadowCastingMode : ShadowCastingMode.Off;
        public bool receiveShadows => settings != null && settings.receiveShadows;

        // 상태 배열. 종류별로 연속 구간을 차지하도록 스폰 때 정렬해두면, 그릴 때 구간을
        // 그대로 넘길 수 있어 매 프레임 종류별로 다시 모을 필요가 없다.
        NativeArray<float3> _positions;
        NativeArray<float3> _velocities;
        NativeArray<float3> _accelerations;
        NativeArray<quaternion> _rotations;
        NativeArray<quaternion> _modelRotations;
        NativeArray<float4x4> _meshToRoot;
        NativeArray<float> _scales;
        NativeArray<float> _speedMultipliers;
        NativeArray<Matrix4x4> _matrices;
        NativeParallelMultiHashMap<int, int> _grid;
        NativeArray<FishScarePoint> _scares;

        FishSpecies[] _usable;
        int[] _speciesStart;
        int[] _speciesCount;
        RenderParams[] _renderParams;
        bool _ready;

        // RenderMeshInstanced 한 번에 넘길 수 있는 최대 인스턴스 수.
        const int MaxInstancesPerDraw = 1023;

        void Start()
        {
            TryInitialize();
        }

        // 설정을 Play 중에 켜고 끌 수 있게, 스폰을 Start에 묶지 않고 필요할 때 만든다.
        bool TryInitialize()
        {
            if (_ready)
            {
                return true;
            }
            if (!spawnFish || spawnCount <= 0)
            {
                return false;
            }

            _usable = System.Array.FindAll(species, s => s != null && s.Resolve() && s.IsUsable);
            if (_usable.Length == 0)
            {
                Debug.LogWarning("[FishSchool] 쓸 수 있는 종류가 없어 스폰하지 않습니다. " +
                    "설정 에셋의 species에 프리팹이 들어 있는지 확인하세요.");
                return false;
            }

            Allocate();
            Spawn();
            _ready = true;
            return true;
        }

        void Allocate()
        {
            _positions = new NativeArray<float3>(spawnCount, Allocator.Persistent);
            _velocities = new NativeArray<float3>(spawnCount, Allocator.Persistent);
            _accelerations = new NativeArray<float3>(spawnCount, Allocator.Persistent);
            _rotations = new NativeArray<quaternion>(spawnCount, Allocator.Persistent);
            _modelRotations = new NativeArray<quaternion>(spawnCount, Allocator.Persistent);
            _meshToRoot = new NativeArray<float4x4>(spawnCount, Allocator.Persistent);
            _scales = new NativeArray<float>(spawnCount, Allocator.Persistent);
            _speedMultipliers = new NativeArray<float>(spawnCount, Allocator.Persistent);
            _matrices = new NativeArray<Matrix4x4>(spawnCount, Allocator.Persistent);
            // 한 셀에 여러 마리가 들어가므로 여유를 두고 잡는다.
            _grid = new NativeParallelMultiHashMap<int, int>(spawnCount * 2, Allocator.Persistent);
            _scares = new NativeArray<FishScarePoint>(FishScare.MaxActive, Allocator.Persistent);
        }

        void Spawn()
        {
            var random = new Unity.Mathematics.Random((uint)UnityEngine.Random.Range(1, int.MaxValue));

            DistributeSpecies();
            _renderParams = new RenderParams[_usable.Length];

            for (int i = 0; i < _usable.Length; i++)
            {
                _renderParams[i] = CreateRenderParams(_usable[i]);
                InitializeInstances(i, ref random);
            }
        }

        // 종류별 마릿수를 weight 비중대로 나눈다. 나머지는 마지막 종류가 받는다.
        // 종류마다 연속 구간을 차지하게 해두면 그릴 때 구간을 그대로 넘길 수 있다.
        void DistributeSpecies()
        {
            _speciesStart = new int[_usable.Length];
            _speciesCount = new int[_usable.Length];

            float totalWeight = 0f;
            foreach (var s in _usable)
            {
                totalWeight += s.weight;
            }

            int assigned = 0;
            for (int i = 0; i < _usable.Length; i++)
            {
                _speciesCount[i] = i == _usable.Length - 1
                    ? spawnCount - assigned
                    : Mathf.FloorToInt(spawnCount * (_usable[i].weight / totalWeight));
                _speciesStart[i] = assigned;
                assigned += _speciesCount[i];
            }
        }

        RenderParams CreateRenderParams(FishSpecies s)
        {
            return new RenderParams(s.Material)
            {
                shadowCastingMode = shadowCastingMode,
                receiveShadows = receiveShadows,
                // 컬링용 범위라 넉넉하게 잡는다 — 좁으면 화면에 있는데도 통째로 사라진다.
                worldBounds = new Bounds(transform.position,
                    new Vector3(boundsSize.x * 2f, 10f, boundsSize.y * 2f))
            };
        }

        void InitializeInstances(int speciesIndex, ref Unity.Mathematics.Random random)
        {
            var s = _usable[speciesIndex];
            var modelRotation = quaternion.Euler(math.radians((float3)s.modelRotationOffset));
            float4x4 meshToRoot = s.MeshToRoot;
            float height = SurfaceHeight;
            int end = _speciesStart[speciesIndex] + _speciesCount[speciesIndex];

            for (int k = _speciesStart[speciesIndex]; k < end; k++)
            {
                float3 offset = new float3(
                    random.NextFloat(-boundsSize.x, boundsSize.x) * 0.4f,
                    0f,
                    random.NextFloat(-boundsSize.y, boundsSize.y) * 0.4f);

                float3 spawnPosition = (float3)transform.position + offset;
                _positions[k] = new float3(spawnPosition.x, height, spawnPosition.z);

                float2 direction = math.normalize(random.NextFloat2Direction());
                _velocities[k] = new float3(direction.x, 0f, direction.y) * random.NextFloat(minSpeed, maxSpeed);
                _rotations[k] = quaternion.LookRotationSafe(_velocities[k], math.up());
                _modelRotations[k] = modelRotation;
                _meshToRoot[k] = meshToRoot;
                // 프리팹 스케일은 meshToRoot에 이미 들어 있으므로 여기서는 개체별 배수만 둔다.
                _scales[k] = random.NextFloat(s.scaleRange.x, s.scaleRange.y);
                _speedMultipliers[k] = s.speedMultiplier;
            }
        }

        // 물고기가 헤엄치는 높이. 스폰과 매 프레임 적분이 같은 값을 써야 한다.
        float SurfaceHeight => useTransformHeight ? transform.position.y : waterHeight;

        // 격자 셀 크기는 인지 반경과 같아야 주변 3x3 셀만 봐도 반경 안 이웃이 전부 들어온다.
        float CellSize => math.max(0.5f, perceptionRadius);

        void Update()
        {
            // 껐으면 이미 만든 것도 정리한다 — 물고기만 남아 떠다니지 않게.
            if (!spawnFish)
            {
                Release();
                return;
            }

            if (!TryInitialize())
            {
                return;
            }

            ScheduleAndComplete();
            Render();
        }

        void ScheduleAndComplete()
        {
            // 살아 있는 놀람 지점만 추려 잡에 넘긴다(만료된 것은 여기서 정리된다).
            int scareCount = FishScare.Collect(_scares);

            _grid.Clear();

            var buildGrid = new BuildGridJob
            {
                Positions = _positions,
                CellSize = CellSize,
                Grid = _grid.AsParallelWriter()
            }.Schedule(spawnCount, 64);

            var flock = new FlockJob
            {
                Positions = _positions,
                Velocities = _velocities,
                SpeedMultipliers = _speedMultipliers,
                Grid = _grid,
                CellSize = CellSize,
                PerceptionRadius = perceptionRadius,
                SeparationRadius = separationRadius,
                CosHalfFov = math.cos(math.radians(fovAngle * 0.5f)),
                MaxSpeed = maxSpeed,
                MaxForce = maxForce,
                AlignWeight = alignWeight,
                CohereWeight = cohereWeight,
                SeparateWeight = separateWeight,
                BoundsWeight = boundsWeight,
                Center = transform.position,
                HalfBounds = new float2(boundsSize.x * 0.5f, boundsSize.y * 0.5f),
                Scares = _scares,
                ScareCount = scareCount,
                Accelerations = _accelerations
            }.Schedule(spawnCount, 32, buildGrid);

            var integrate = new IntegrateJob
            {
                Accelerations = _accelerations,
                Scales = _scales,
                SpeedMultipliers = _speedMultipliers,
                ModelRotations = _modelRotations,
                MeshToRoot = _meshToRoot,
                Positions = _positions,
                Velocities = _velocities,
                Rotations = _rotations,
                Matrices = _matrices,
                DeltaTime = Time.deltaTime,
                MaxSpeed = maxSpeed,
                MinSpeed = minSpeed,
                TurnSpeed = turnSpeed,
                WaterHeight = SurfaceHeight
            }.Schedule(spawnCount, 64, flock);

            // 그리려면 결과가 필요하므로 이 프레임 안에서 끝낸다. 잡 사이에 메인 스레드가
            // 할 일이 없어서 프레임을 넘겨 기다릴 이유도 없다.
            integrate.Complete();
        }

        void Render()
        {
            for (int i = 0; i < _usable.Length; i++)
            {
                int count = _speciesCount[i];
                if (count <= 0)
                {
                    continue;
                }

                // 한 번에 1023개까지만 넘길 수 있어 나눠 그린다.
                int drawn = 0;
                while (drawn < count)
                {
                    int batch = math.min(MaxInstancesPerDraw, count - drawn);
                    Graphics.RenderMeshInstanced(_renderParams[i], _usable[i].Mesh, 0, _matrices,
                        batch, _speciesStart[i] + drawn);
                    drawn += batch;
                }
            }
        }


        // NativeArray는 GC가 치우지 않는다 — 반드시 직접 해제해야 에디터에 누수 경고가 남지 않는다.
        void OnDestroy()
        {
            Release();
        }

        void Release()
        {
            if (!_ready)
            {
                return;
            }

            _positions.Dispose();
            _velocities.Dispose();
            _accelerations.Dispose();
            _rotations.Dispose();
            _modelRotations.Dispose();
            _meshToRoot.Dispose();
            _scales.Dispose();
            _speedMultipliers.Dispose();
            _matrices.Dispose();
            _grid.Dispose();
            _scares.Dispose();

            foreach (var s in _usable)
            {
                s.ReleaseRuntimeMaterial();
            }

            _ready = false;
        }

#if UNITY_EDITOR
        // 식생 설정(<씬이름>_VegetationSettings)과 같은 규칙으로 만들어 둔다 — 씬마다
        // 무리를 따로 튜닝할 때 어느 에셋이 어느 씬 것인지 바로 알 수 있다.
        [ContextMenu("설정 에셋 만들기")]
        void CreateSettingsAsset()
        {
            if (settings != null)
            {
                UnityEditor.EditorGUIUtility.PingObject(settings);
                return;
            }

            const string folder = "Assets/Settings";
            if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
            {
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Settings");
            }

            string sceneName = gameObject.scene.name;
            string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(
                $"{folder}/{sceneName}_FishSettings.asset");

            var created = ScriptableObject.CreateInstance<FishSchoolSettings>();
            UnityEditor.AssetDatabase.CreateAsset(created, path);
            UnityEditor.AssetDatabase.SaveAssets();

            settings = created;
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.EditorGUIUtility.PingObject(created);
            Debug.Log($"[FishSchool] 설정 에셋을 만들었습니다: {path}");
        }
#endif

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, new Vector3(boundsSize.x, 0.1f, boundsSize.y));

            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, perceptionRadius);
        }
    }
}
