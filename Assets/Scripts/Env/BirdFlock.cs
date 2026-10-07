using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Mountains.Env
{
    // 새 떼. FishSchool과 같은 구조(GameObject 없이 배열 상태 + Burst 잡 + GPU 인스턴싱)를
    // 쓰지만 두 가지가 다르다 — (1) 설정의 birdPrefab 하나에서 메시와 텍스처만 꺼내
    // 그린다. 수백 마리도 가볍게 그리려고 GameObject/Animator를 만들지 않으므로 프리팹의
    // 뼈대 애니메이션은 재생되지 않고, 날갯짓은 BirdDisplacement 셰이더가 흉내 낸다.
    // (2) 물고기는 정해진 높이에 붙어 2D로 헤엄치는 반면 새는 고도 밴드 안에서 자유롭게
    // 3D로 난다(BirdFlockJobs 참고).
    public class BirdFlock : MonoBehaviour
    {
        [Tooltip("무리 설정 에셋. 비어 있으면 아무것도 스폰하지 않는다. 컴포넌트 우클릭 > " +
            "설정 에셋 만들기로 씬 이름에 맞춰 생성할 수 있다.")]
        public BirdFlockSettings settings;

        public bool spawnBirds => settings != null && settings.spawnBirds;
        public int spawnCount => settings != null ? settings.spawnCount : 0;
        public Vector2 scaleRange => settings != null ? settings.scaleRange : new Vector2(0.8f, 1.3f);
        public Vector2 boundsSize => settings != null ? settings.boundsSize : new Vector2(60f, 60f);
        public float minAltitude => settings != null ? settings.minAltitude : 10f;
        public float maxAltitude => settings != null ? settings.maxAltitude : 22f;
        public float maxSpeed => settings != null ? settings.maxSpeed : 6f;
        public float minSpeed => settings != null ? settings.minSpeed : 3f;
        public float maxForce => settings != null ? settings.maxForce : 4f;
        public float turnSpeed => settings != null ? settings.turnSpeed : 3f;
        public float maxPitchAngle => settings != null ? settings.maxPitchAngle : 25f;
        public float perceptionRadius => settings != null ? settings.perceptionRadius : 10f;
        public float separationRadius => settings != null ? settings.separationRadius : 9f;
        public float fovAngle => settings != null ? settings.fovAngle : 300f;
        public float alignWeight => settings != null ? settings.alignWeight : 1f;
        public float cohereWeight => settings != null ? settings.cohereWeight : 0.4f;
        public float separateWeight => settings != null ? settings.separateWeight : 3f;
        public float boundsWeight => settings != null ? settings.boundsWeight : 3f;

        NativeArray<float3> _positions;
        NativeArray<float3> _velocities;
        NativeArray<float3> _accelerations;
        NativeArray<quaternion> _rotations;
        NativeArray<float> _scales;
        NativeArray<Matrix4x4> _matrices;
        NativeParallelMultiHashMap<int, int> _grid;

        // 프리팹의 공유 메시 — 에셋이라 절대 파괴하면 안 된다.
        Mesh _mesh;
        // 런타임 복제 머티리얼 — 직접 만들었으니 직접 파괴한다.
        Material _material;
        RenderParams _renderParams;
        // 모델 축 보정(설정의 modelRotationOffset)과, 프리팹 안에서 메시가 놓인 상태.
        // FishSpecies와 같은 이유로 인스턴스 변환 뒤에 곱해야 모델이 비스듬해지지 않는다.
        quaternion _modelRotation;
        float4x4 _meshToRoot;
        bool _ready;
        bool _warnedSetup;

        const int MaxInstancesPerDraw = 1023;

        void Start()
        {
            TryInitialize();
        }

        bool TryInitialize()
        {
            if (_ready)
            {
                return true;
            }
            if (!spawnBirds || spawnCount <= 0)
            {
                return false;
            }

            if (!BuildMeshAndMaterial())
            {
                return false;
            }

            Allocate();
            Spawn();
            _ready = true;
            return true;
        }

        bool BuildMeshAndMaterial()
        {
            var prefab = settings.birdPrefab;
            if (prefab == null)
            {
                WarnOnce("설정 에셋의 birdPrefab이 비어 있어 새를 스폰하지 않습니다.");
                return false;
            }

            if (!TryResolveMesh(prefab, out var mesh, out var sourceMaterial, out var meshTransform))
            {
                WarnOnce($"{prefab.name}에서 메시를 찾지 못했습니다(SkinnedMeshRenderer 또는 MeshFilter+MeshRenderer 필요).");
                return false;
            }

            // Shader.Find로 새 머티리얼을 만들면 에디터에서만 보인다 — 빌드에는 에셋이 참조하는
            // 셰이더와, 인스턴싱을 켠 머티리얼 에셋이 쓰는 인스턴싱 변형만 들어가기 때문이다.
            // 그래서 설정 에셋의 기반 머티리얼(물고기의 FishSpecies 머티리얼과 같은 역할)을 복제한다.
            if (settings.material == null)
            {
                WarnOnce("설정 에셋의 material이 비어 있어 새를 스폰하지 않습니다(Materials/BirdFlock.mat).");
                return false;
            }

            _mesh = mesh;
            _meshToRoot = prefab.transform.worldToLocalMatrix * meshTransform.localToWorldMatrix;
            _modelRotation = quaternion.Euler(math.radians((float3)settings.modelRotationOffset));

            // 복제본에만 텍스처/날갯짓 값을 쓴다 — 에셋 자체를 고치면 Play를 멈춰도 남는다.
            _material = new Material(settings.material)
            {
                name = "BirdFlock (Generated)",
                enableInstancing = true,
                hideFlags = HideFlags.HideAndDontSave
            };
            CopySurface(sourceMaterial, _material, settings.tint);
            SetFlapParameters(_material, mesh, _meshToRoot);

            _renderParams = new RenderParams(_material)
            {
                shadowCastingMode = settings.shadowCastingMode,
                receiveShadows = settings.receiveShadows,
                // 컬링용 범위라 넉넉하게 잡는다 — 좁으면 화면에 있는데도 통째로 사라진다.
                worldBounds = new Bounds(BoundsCenter, new Vector3(boundsSize.x * 1.5f,
                    (maxAltitude - minAltitude) * 1.5f + 10f, boundsSize.y * 1.5f))
            };
            return true;
        }

        // 스킨드 메시면 기본 자세(바인드 포즈) 메시를 그대로 쓴다 — 뼈대 애니메이션을 안
        // 쓰므로 바인드 포즈가 곧 날개를 편 기본 자세다.
        static bool TryResolveMesh(GameObject prefab, out Mesh mesh, out Material material, out Transform meshTransform)
        {
            var skinned = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skinned != null && skinned.sharedMesh != null)
            {
                mesh = skinned.sharedMesh;
                material = skinned.sharedMaterial;
                meshTransform = skinned.transform;
                return true;
            }

            var filter = prefab.GetComponentInChildren<MeshFilter>(true);
            var renderer = filter != null ? filter.GetComponent<MeshRenderer>() : null;
            if (filter != null && filter.sharedMesh != null && renderer != null)
            {
                mesh = filter.sharedMesh;
                material = renderer.sharedMaterial;
                meshTransform = filter.transform;
                return true;
            }

            mesh = null;
            material = null;
            meshTransform = null;
            return false;
        }

        // 프리팹 머티리얼의 텍스처/색만 옮긴다. 셰이더는 인스턴싱 + 날갯짓 전용이라 따로 쓴다.
        static void CopySurface(Material source, Material target, Color tint)
        {
            Color color = Color.white;
            if (source != null)
            {
                string textureProperty = source.HasProperty("_BaseMap") ? "_BaseMap"
                    : source.HasProperty("_MainTex") ? "_MainTex" : null;
                if (textureProperty != null)
                {
                    target.SetTexture("_BaseMap", source.GetTexture(textureProperty));
                    target.SetTextureScale("_BaseMap", source.GetTextureScale(textureProperty));
                    target.SetTextureOffset("_BaseMap", source.GetTextureOffset(textureProperty));
                }

                if (source.HasProperty("_BaseColor"))
                {
                    color = source.GetColor("_BaseColor");
                }
                else if (source.HasProperty("_Color"))
                {
                    color = source.GetColor("_Color");
                }
            }

            target.SetColor("_BaseColor", color * tint);
        }

        // 날갯짓 축을 메시에 맞춰 정한다. 모델마다 축이 달라서(FBX 축 변환 등) 고정값을
        // 쓰면 날개가 아니라 몸통이 흔들린다.
        // - 펄럭이는 방향: 프리팹 루트의 위쪽을 메시 공간으로 옮긴 것.
        // - 날개 폭 방향: 위쪽 축을 뺀 나머지 중 가장 긴 축 — 날개를 편 새는 몸길이보다 폭이 넓다.
        void SetFlapParameters(Material material, Mesh mesh, Matrix4x4 meshToRoot)
        {
            Vector3 flapAxis = meshToRoot.inverse.MultiplyVector(Vector3.up).normalized;
            int upAxis = LargestComponentIndex(flapAxis);

            Vector3 size = mesh.bounds.size;
            int spanAxis = -1;
            for (int axis = 0; axis < 3; axis++)
            {
                if (axis != upAxis && (spanAxis < 0 || size[axis] > size[spanAxis]))
                {
                    spanAxis = axis;
                }
            }

            var spanVector = Vector3.zero;
            spanVector[spanAxis] = 1f;

            material.SetVector("_SpanAxis", spanVector);
            material.SetVector("_FlapAxis", flapAxis);
            material.SetVector("_BodyCenter", mesh.bounds.center);
            material.SetFloat("_BodyHalfWidth", size[spanAxis] * 0.5f * settings.bodyWidthRatio);
            material.SetFloat("_FlapSpeed", settings.flapSpeed);
            material.SetFloat("_FlapAmplitude", settings.flapAmplitude);
            material.SetFloat("_PhaseSpread", settings.phaseSpread);
        }

        static int LargestComponentIndex(Vector3 v)
        {
            float x = Mathf.Abs(v.x), y = Mathf.Abs(v.y), z = Mathf.Abs(v.z);
            return x >= y && x >= z ? 0 : y >= z ? 1 : 2;
        }

        // Update가 매 프레임 초기화를 다시 시도하므로, 설정 문제는 한 번만 알린다.
        void WarnOnce(string message)
        {
            if (_warnedSetup)
            {
                return;
            }

            _warnedSetup = true;
            Debug.LogWarning($"[BirdFlock] {message}", this);
        }

        void Allocate()
        {
            _positions = new NativeArray<float3>(spawnCount, Allocator.Persistent);
            _velocities = new NativeArray<float3>(spawnCount, Allocator.Persistent);
            _accelerations = new NativeArray<float3>(spawnCount, Allocator.Persistent);
            _rotations = new NativeArray<quaternion>(spawnCount, Allocator.Persistent);
            _scales = new NativeArray<float>(spawnCount, Allocator.Persistent);
            _matrices = new NativeArray<Matrix4x4>(spawnCount, Allocator.Persistent);
            _grid = new NativeParallelMultiHashMap<int, int>(spawnCount * 2, Allocator.Persistent);
        }

        void Spawn()
        {
            var random = new Unity.Mathematics.Random((uint)UnityEngine.Random.Range(1, int.MaxValue));

            for (int i = 0; i < spawnCount; i++)
            {
                // 수평은 영역 안쪽(가장자리 전)에, 높이는 고도 밴드 안에서 뽑는다.
                float3 offset = new float3(
                    random.NextFloat(-boundsSize.x, boundsSize.x) * 0.4f,
                    random.NextFloat(minAltitude, maxAltitude),
                    random.NextFloat(-boundsSize.y, boundsSize.y) * 0.4f);

                _positions[i] = (float3)transform.position + offset;

                float3 direction = math.normalize(new float3(
                    random.NextFloat(-1f, 1f), random.NextFloat(-0.2f, 0.2f), random.NextFloat(-1f, 1f)));
                _velocities[i] = direction * random.NextFloat(minSpeed, maxSpeed);
                _rotations[i] = quaternion.LookRotationSafe(_velocities[i], math.up());
                _scales[i] = random.NextFloat(scaleRange.x, scaleRange.y);
            }
        }

        // 수평 경계 + 고도 밴드를 한 상자로 합친 중심/반폭. BirdFlockJob과 스폰 둘 다 이
        // 값을 써야 하므로 프로퍼티로 묶어둔다.
        Vector3 BoundsCenter => transform.position + new Vector3(0f, (minAltitude + maxAltitude) * 0.5f, 0f);
        Vector3 BoundsHalfExtent => new Vector3(boundsSize.x * 0.5f, (maxAltitude - minAltitude) * 0.5f, boundsSize.y * 0.5f);

        float CellSize => math.max(0.5f, perceptionRadius);

        void Update()
        {
            if (!spawnBirds)
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
            _grid.Clear();

            var buildGrid = new BuildBirdGridJob
            {
                Positions = _positions,
                CellSize = CellSize,
                Grid = _grid.AsParallelWriter()
            }.Schedule(spawnCount, 64);

            var flock = new BirdFlockJob
            {
                Positions = _positions,
                Velocities = _velocities,
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
                Center = BoundsCenter,
                HalfBounds = BoundsHalfExtent,
                Accelerations = _accelerations
            }.Schedule(spawnCount, 32, buildGrid);

            var integrate = new IntegrateBirdJob
            {
                Accelerations = _accelerations,
                Scales = _scales,
                Positions = _positions,
                Velocities = _velocities,
                Rotations = _rotations,
                Matrices = _matrices,
                ModelRotation = _modelRotation,
                MeshToRoot = _meshToRoot,
                DeltaTime = Time.deltaTime,
                MaxSpeed = maxSpeed,
                MinSpeed = minSpeed,
                TurnSpeed = turnSpeed,
                SinMaxPitch = math.sin(math.radians(math.clamp(maxPitchAngle, 0f, 90f)))
            }.Schedule(spawnCount, 64, flock);

            // 그리려면 결과가 필요하므로 이 프레임 안에서 끝낸다.
            integrate.Complete();
        }

        void Render()
        {
            if (_mesh == null || _material == null)
            {
                return;
            }

            int drawn = 0;
            while (drawn < spawnCount)
            {
                int batch = math.min(MaxInstancesPerDraw, spawnCount - drawn);
                Graphics.RenderMeshInstanced(_renderParams, _mesh, 0, _matrices, batch, drawn);
                drawn += batch;
            }
        }

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
            _scales.Dispose();
            _matrices.Dispose();
            _grid.Dispose();

            // _mesh는 프리팹의 공유 에셋이라 참조만 놓는다.
            DestroyRuntimeObject(_material);
            _mesh = null;
            _material = null;

            _ready = false;
        }

        static void DestroyRuntimeObject(Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }

#if UNITY_EDITOR
        // 물고기/식생 설정과 같은 규칙으로 만들어 둔다 — 씬마다 무리를 따로 튜닝할 때
        // 어느 에셋이 어느 씬 것인지 바로 알 수 있다.
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
                $"{folder}/{sceneName}_BirdSettings.asset");

            var created = ScriptableObject.CreateInstance<BirdFlockSettings>();
            UnityEditor.AssetDatabase.CreateAsset(created, path);
            UnityEditor.AssetDatabase.SaveAssets();

            settings = created;
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.EditorGUIUtility.PingObject(created);
            Debug.Log($"[BirdFlock] 설정 에셋을 만들었습니다: {path}");
        }
#endif

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.8f);
            Gizmos.DrawWireCube(BoundsCenter, BoundsHalfExtent * 2f);

            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * (minAltitude + maxAltitude) * 0.5f, perceptionRadius);
        }
    }
}
