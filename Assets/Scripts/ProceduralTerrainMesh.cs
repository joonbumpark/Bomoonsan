using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Mountains
{
    // 서로 안 이어지는 길을 여러 개 둘 수 있도록 "길 하나"를 감싸는 단위. Vector2[][](배열의
    // 배열)는 Unity 직렬화가 중첩 컬렉션을 지원하지 않아 못 쓰므로, ScatterGroup과 같은
    // [Serializable] 래퍼 클래스 배열 패턴을 쓴다.
    [System.Serializable]
    public class TerrainPath
    {
        public string name = "Path";
        [Tooltip("길 중심선의 격자 좌표(0..width-1, 0..length-1). 2개 이상이어야 길이 생성된다.")]
        public Vector2[] waypoints = new Vector2[0];
    }

    // 길(TerrainPath)과 같은 이유로 래퍼 클래스 배열을 쓴다. 길은 열린 곡선이지만 호수는
    // "안/밖"이 있는 닫힌 폴리곤이라는 점이 유일한 차이다.
    [System.Serializable]
    public class WaterArea
    {
        public string name = "Lake";
        [Tooltip("호수 경계의 격자 좌표(0..width-1, 0..length-1). 3개 이상이어야 영역이 생긴다. " +
            "닫힌 폴리곤으로 취급된다(마지막 점과 첫 점이 자동으로 이어짐).")]
        public Vector2[] waypoints = new Vector2[0];
    }

    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class ProceduralTerrainMesh : MonoBehaviour
    {
        [Tooltip("생성 파라미터 에셋. 비어있으면 Mountains > Create Procedural Terrain 실행 시 자동으로 만들어진다. " +
            "여기 담긴 값은 지형을 재생성해도 초기화되지 않는다.")]
        public TerrainGenerationSettings settings;

        [Header("Path")]
        public TerrainPath[] paths = new TerrainPath[0];

        [Header("Water")]
        public WaterArea[] waterAreas = new WaterArea[0];

        [Header("Asset Overrides")]
        [Tooltip("비워두면 이 지형(오브젝트 이름 기준)만을 위한 새 텍스처/머티리얼을 " +
            "Assets/Materials 밑에 자동으로 만든다 — 한 번 만들어지면 여기 저장되어 계속 " +
            "이 지형 전용으로만 쓰인다. 다른 지형/씬과 파일을 공유하지 않으므로, 씬을 복사해도 " +
            "서로 영향을 주지 않는다. 보통 직접 건드릴 필요 없다.")]
        public Texture2D gradientTexture;
        public Texture2D pathMaskTexture;
        public Material waterMaterial;
        public Texture2D[] waterMaskTextures = new Texture2D[0];

        // settings의 각 값을 그대로 노출하는 읽기 전용 프로퍼티. Generate() 등 본문 코드는
        // 지금까지처럼 width/cellSize/noiseScale 등을 그대로 참조하면 되고, settings가 아직
        // 할당되지 않은 경우에만 안전한 기본값을 대신 돌려준다.
        public int width => settings != null ? settings.width : 100;
        public int length => settings != null ? settings.length : 100;
        public float cellSize => settings != null ? settings.cellSize : 10f;
        public float noiseScale => settings != null ? settings.noiseScale : 28f;
        public int octaves => settings != null ? settings.octaves : 3;
        public float persistence => settings != null ? settings.persistence : 0.45f;
        public float lacunarity => settings != null ? settings.lacunarity : 2f;
        public float heightMultiplier => settings != null ? settings.heightMultiplier : 60f;
        public int seed => settings != null ? settings.seed : 0;
        public float noiseOffsetScale => settings != null ? settings.noiseOffsetScale : 1f;
        public int smoothingIterations => settings != null ? settings.smoothingIterations : 2;
        public Gradient heightGradient => settings != null ? settings.heightGradient : null;
        public float pathWidth => settings != null ? settings.pathWidth : 4f;
        public float pathBlendWidth => settings != null ? settings.pathBlendWidth : 3f;
        public float pathFlattenStrength => settings != null ? settings.pathFlattenStrength : 1f;
        public int pathCurveSamplesPerSegment => settings != null ? settings.pathCurveSamplesPerSegment : 8;
        public float pathEdgeJitter => settings != null ? settings.pathEdgeJitter : 5f;
        public int pathMaskResolution => settings != null ? settings.pathMaskResolution : 512;
        public float waterDepth => settings != null ? settings.waterDepth : 8f;
        public float waterShoreBlendWidth => settings != null ? settings.waterShoreBlendWidth : 12f;
        public float waterEdgeJitter => settings != null ? settings.waterEdgeJitter : 6f;
        public int waterCurveSamplesPerSegment => settings != null ? settings.waterCurveSamplesPerSegment : 8;
        public int waterMaskResolution => settings != null ? settings.waterMaskResolution : 256;
        public float waterCliffSharpness => settings != null ? settings.waterCliffSharpness : 4f;
        public bool buildEdgeWalls => settings != null ? settings.buildEdgeWalls : true;
        public float edgeWallHeight => settings != null ? settings.edgeWallHeight : 50f;
        public float edgeWallThickness => settings != null ? settings.edgeWallThickness : 5f;

        // 이 지형이 만드는 파생 에셋(그라디언트/마스크 텍스처, 물 머티리얼)의 파일명 접두사.
        // 이 에셋들은 사실상 "그 씬의 지형" 소유라서 씬 이름을 쓴다 — 씬을 복제하면 지형
        // 오브젝트 이름은 양쪽이 똑같아서, 오브젝트 이름을 쓰면 어느 씬 것인지 구분이 안 된다.
        // 아직 저장 안 된 씬은 이름이 비어있으니 그때만 오브젝트 이름으로 대신한다.
        public string AssetBaseName => !string.IsNullOrEmpty(gameObject.scene.name) ? gameObject.scene.name : gameObject.name;

        Mesh _mesh;
        // Mesh.vertices/normals는 프로퍼티라 호출할 때마다 네이티브 메모리에서 배열을
        // 통째로 새로 복사한다 — 식생 배치처럼 정점/노멀을 대량으로 조회하는 코드가 이걸
        // 직접 부르면(예전엔 GetWorldPositionAt/GetNormalAt이 호출마다 4번씩) 인스턴스
        // 수십만 개 기준으로 GC 할당이 수백만 번 발생해 Play 진입이 몇 초씩 느려진다
        // (Profiler의 "CopyChannels" 마커로 확인). Generate()가 끝날 때 한 번만 복사해두고
        // 이후 조회는 전부 이 캐시를 쓴다.
        Vector3[] _cachedVertices;
        Vector3[] _cachedNormals;

        // 메시(정점)만으로는 되돌려 계산할 수 없는 "카빙 전" 기준 값들. 빌드나 도메인
        // 리로드 후에 Generate()를 통째로 다시 돌리지 않고 상태를 복원하려면 이 둘이
        // 필요해서 씬에 같이 저장한다(Inspector에 노출할 값은 아니라 HideInInspector).
        [SerializeField, HideInInspector] float _serializedHeightRange;
        [SerializeField, HideInInspector] float[] _serializedWaterRimHeights;

        float[] _pathMaskTexels;
        int _pathMaskTexelsResolution;
        float _heightRange = 1f;
        float _sizeX = 1f;
        float _sizeZ = 1f;
        Vector2 _warpOffsetA;
        Vector2 _warpOffsetB;
        Vector2 _waterWarpOffsetA;
        Vector2 _waterWarpOffsetB;
        List<WaterAreaCache> _waterAreaCaches = new List<WaterAreaCache>();
        Transform _waterContainer;
        Transform _waterNavContainer;

        void Start()
        {
            // [ExecuteAlways]라 Domain/Scene Reload를 꺼둔 상태로 Play에 들어가도 Start()가
            // 다시 불린다 — 이미 만들어진 메시가 있으면(_mesh는 비직렬화 필드라 Reload를
            // 껐을 땐 이전 값이 그대로 남아있다) 건너뛴다. 그냥 매번 Generate()를 부르면
            // 노이즈 샘플링부터 길/물 마스크 텍스처 굽기까지 통째로 반복돼서 Play 진입에만
            // 몇 초가 걸린다(프로파일러로 확인, Start() 자체 시간 약 6.8초). settings를
            // 바꿔서 강제로 다시 구워야 할 때는 Inspector 등에서 Generate()를 직접 호출한다.
            if (_mesh != null && _mesh.vertexCount == width * length)
            {
                return;
            }

            // 빌드(그리고 도메인 리로드를 켜둔 에디터)에서는 _mesh 같은 비직렬화 필드가
            // 전부 비어 있어서 예전엔 여기서 Generate()가 통째로 다시 돌았다 — 노이즈
            // 샘플링부터 길/물 마스크 굽기, 물 표면·가장자리벽·NavMesh 볼륨 재생성까지.
            // 정작 메시도 물 표면 오브젝트도 이미 씬에 저장돼 있으므로, 저장된 것들을
            // 그대로 채택하고 런타임에 실제로 필요한 캐시만 복원하면 그 비용이 통째로 없어진다.
            if (TryRestoreRuntimeState())
            {
                return;
            }

            Generate();
        }

        // 씬에 저장된 메시/텍스처/설정으로 런타임 조회용 상태만 복원한다. 복원할 수 없는
        // 상태(메시가 없거나 설정과 안 맞거나, Generate()를 거치지 않아 직렬화 값이 없는
        // 경우)면 false를 돌려서 호출 측이 Generate()로 넘어가게 한다.
        bool TryRestoreRuntimeState()
        {
            var filter = GetComponent<MeshFilter>();
            var savedMesh = filter != null ? filter.sharedMesh : null;
            if (savedMesh == null || savedMesh.vertexCount != width * length)
            {
                return false;
            }

            // 이 값이 비어 있으면 이 스크립트의 저장 로직이 생기기 전에 만들어진 지형이라
            // 복원에 필요한 정보가 없다 — 한 번 Generate()를 돌려서 채워 넣게 한다.
            if (_serializedHeightRange <= 0f)
            {
                return false;
            }

            if (!TryRestorePathMaskTexels())
            {
                return false;
            }

            _mesh = savedMesh;
            _cachedVertices = savedMesh.vertices;
            _cachedNormals = savedMesh.normals;
            _heightRange = _serializedHeightRange;
            _sizeX = Mathf.Max(0.0001f, (width - 1) * cellSize);
            _sizeZ = Mathf.Max(0.0001f, (length - 1) * cellSize);
            RefreshWarpOffsets();
            RestoreWaterAreaCaches();
            RestoreWaterSurfaceMaskBlocks();
            return true;
        }

        // 물 표면 오브젝트(Water_i)는 씬에 저장돼 있지만, 호수별 마스크 텍스처는
        // MaterialPropertyBlock으로 넘기고 있고 이건 직렬화되지 않는다 — 재생성을
        // 건너뛰면 _MaskTex가 셰이더 기본값("black")이 되어 clip()에 전부 잘려서 물이
        // 통째로 안 보인다. 저장해둔 텍스처로 블록만 다시 씌워준다.
        void RestoreWaterSurfaceMaskBlocks()
        {
            if (waterMaskTextures == null || waterMaskTextures.Length == 0)
            {
                return;
            }

            FindOrCreateWaterContainer();
            if (_waterContainer == null)
            {
                return;
            }

            var block = new MaterialPropertyBlock();
            for (int i = 0; i < waterMaskTextures.Length; i++)
            {
                if (waterMaskTextures[i] == null)
                {
                    continue;
                }

                var child = _waterContainer.Find($"Water_{i}");
                var meshRenderer = child != null ? child.GetComponent<MeshRenderer>() : null;
                if (meshRenderer == null)
                {
                    continue;
                }

                block.Clear();
                block.SetTexture("_MaskTex", waterMaskTextures[i]);
                meshRenderer.SetPropertyBlock(block);
            }
        }

        // GetPathMaskAt(식생 avoidPath)이 쓰는 텍셀 배열을 이미 구워둔 텍스처에서 되읽는다.
        bool TryRestorePathMaskTexels()
        {
            _pathMaskTexels = null;
            _pathMaskTexelsResolution = 0;

            // 길이 하나도 없으면 마스크 텍스처도 없는 게 정상이다 — 복원 실패가 아니다.
            if (pathMaskTexture == null)
            {
                return paths == null || paths.Length == 0;
            }

            int resolution = pathMaskTexture.width;
            if (resolution <= 0 || pathMaskTexture.height != resolution || !pathMaskTexture.isReadable)
            {
                return false;
            }

            var pixels = pathMaskTexture.GetPixels();
            if (pixels == null || pixels.Length != resolution * resolution)
            {
                return false;
            }

            _pathMaskTexels = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                _pathMaskTexels[i] = pixels[i].r;
            }
            _pathMaskTexelsResolution = resolution;
            return true;
        }

        // IsInsideWaterArea(식생 avoidWater)가 쓰는 호수 캐시를 복원한다. 곡선은
        // 웨이포인트에서 결정론적으로 다시 만들 수 있고(BuildWaterAreaCaches와 완전히
        // 같은 조건/순서), rimHeight만 카빙 전 값이라 저장해둔 걸 가져다 쓴다.
        void RestoreWaterAreaCaches()
        {
            _waterAreaCaches.Clear();
            if (waterAreas == null || _serializedWaterRimHeights == null)
            {
                return;
            }

            int index = 0;
            foreach (var area in waterAreas)
            {
                var waypoints = area?.waypoints;
                if (waypoints == null || waypoints.Length < 3)
                {
                    continue;
                }

                var curve = BuildSmoothClosedPath(waypoints, waterCurveSamplesPerSegment);
                if (curve.Count < 3)
                {
                    continue;
                }

                if (index >= _serializedWaterRimHeights.Length)
                {
                    break;
                }

                _waterAreaCaches.Add(new WaterAreaCache
                {
                    worldCurve = ToWorldSpace(curve),
                    rimHeight = _serializedWaterRimHeights[index],
                });
                index++;
            }
        }

        public void Generate()
        {
            if (settings == null)
            {
                Debug.LogWarning("[ProceduralTerrainMesh] settings가 할당되지 않아 생성을 건너뜁니다. " +
                    "Mountains > Create Procedural Terrain을 실행하면 자동으로 만들어집니다.");
                return;
            }

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "ProceduralTerrainMesh" };
                GetComponent<MeshFilter>().sharedMesh = _mesh;
            }

            Vector2[] octaveOffsets = BuildOctaveOffsets();

            int vertexCount = width * length;
            var heights = new float[vertexCount];
            for (int z = 0; z < length; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    heights[z * width + x] = SampleHeight(x, z, octaveOffsets);
                }
            }

            // 정점 간격(cellSize)에 비해 고주파 옥타브가 너무 촘촘하면 메시가 그 디테일을
            // 제대로 표현 못 하고 뾰족뾰족하게 나온다. 이웃 정점끼리 평균 내는 박스 블러를
            // 반복해서 봉우리를 둥글게 다듬는다.
            for (int iter = 0; iter < smoothingIterations; iter++)
            {
                heights = SmoothHeights(heights);
            }

            _sizeX = Mathf.Max(0.0001f, (width - 1) * cellSize);
            _sizeZ = Mathf.Max(0.0001f, (length - 1) * cellSize);
            RefreshWarpOffsets();

            // 웨이포인트를 따라 지형(정점 높이)을 평탄화한다. 실제 지오메트리를 움직이는
            // 거라 cellSize 해상도에 묶인다 — 시각적으로 보이는 길 폭은 BakePathMaskTexture가
            // 따로 훨씬 촘촘한 텍스처로 구워서 셰이더에 넘긴다(정점 해상도와 무관하게 얇게 가능).
            ApplyPathFlattening(heights);

            // min/max(그리고 그걸로 정하는 y=0 기준 shift)는 호수 카빙 전(원래 지형 + 길)
            // 기준으로 먼저 계산해둔다 — 카빙 후 기준으로 계산하면 waterDepth를 키울 때마다
            // 호수 바닥이 지형 전체의 새 "최저점"이 되어, 그 값에 맞춰 지형 전체가 위로
            // 밀려 올라가 보이는 문제가 있었다. 카빙으로 파인 부분은 이 기준선보다 아래로
            // (즉 로컬 y가 음수로) 내려가는 것을 그대로 허용한다.
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;
            for (int i = 0; i < vertexCount; i++)
            {
                minHeight = Mathf.Min(minHeight, heights[i]);
                maxHeight = Mathf.Max(maxHeight, heights[i]);
            }

            // 길 평탄화 다음, 최종 정점을 만들기 전에 호수 영역을 파낸다. 림 높이(rimHeight)
            // 캐시는 아직 위에서 구한 minHeight만큼 shift되기 전 값이라, 아래 shift 루프
            // 직후에 같이 보정해줘야 한다.
            ApplyWaterCarving(heights);

            var vertices = new Vector3[vertexCount];
            for (int z = 0; z < length; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = z * width + x;
                    vertices[i] = new Vector3(x * cellSize, heights[i], z * cellSize);
                }
            }

            _heightRange = Mathf.Max(0.0001f, maxHeight - minHeight);
            for (int i = 0; i < vertexCount; i++)
            {
                vertices[i].y -= minHeight;
            }

            foreach (var cache in _waterAreaCaches)
            {
                cache.rimHeight -= minHeight;
            }

            int quadCountX = width - 1;
            int quadCountZ = length - 1;
            var triangles = new int[quadCountX * quadCountZ * 6];
            int t = 0;
            for (int z = 0; z < quadCountZ; z++)
            {
                for (int x = 0; x < quadCountX; x++)
                {
                    int i = z * width + x;

                    triangles[t++] = i;
                    triangles[t++] = i + width;
                    triangles[t++] = i + 1;

                    triangles[t++] = i + 1;
                    triangles[t++] = i + width;
                    triangles[t++] = i + width + 1;
                }
            }

            // TerrainBlend 셰이더는 높이/경사 색상을 메시 UV가 아니라 월드 Position/Normal로
            // 직접 계산하므로, 여기서는 향후 다른 셰이더(타일링 텍스처 등)를 위한 표준 평면
            // UV만 채워두면 된다.
            var uvs = new Vector2[vertexCount];
            for (int z = 0; z < length; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    uvs[z * width + x] = new Vector2((float)x / (width - 1), (float)z / (length - 1));
                }
            }

            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
            _mesh.uv = uvs;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();

            // Tangent는 UV/노멀이 확정된 뒤에 계산해야 한다 — 노멀맵 라이팅(TBN 공간)에 필요.
            // 이게 없으면 셰이더에서 노멀맵을 입혀도 탄젠트 공간을 못 만들어 라이팅이 깨진다.
            _mesh.RecalculateTangents();

            // vertices는 위에서 이미 만든 로컬 배열을 그대로 재사용(추가 복사 없음), normals는
            // RecalculateNormals()가 만든 결과라 한 번만 읽어서 캐시해둔다.
            _cachedVertices = vertices;
            _cachedNormals = _mesh.normals;

            var meshCollider = GetComponent<MeshCollider>();
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = _mesh;

            ApplyGradientTexture();
            BakePathMaskTexture();
            BuildWaterSurfaces(heights, minHeight);
            BuildEdgeWalls();
            BuildWaterNavObstacles(heights, minHeight);

            // 빌드/도메인 리로드 후에는 Generate()를 다시 돌리지 않고 씬에 저장된 메시를
            // 그대로 채택하는데(TryRestoreRuntimeState), 그때 메시만으로는 복원할 수 없는
            // 두 값을 여기서 같이 저장해둔다 — 둘 다 "카빙 전" 지형 기준이라 카빙이 끝난
            // 정점에서는 되돌려 계산할 수 없다.
            _serializedHeightRange = _heightRange;
            _serializedWaterRimHeights = new float[_waterAreaCaches.Count];
            for (int i = 0; i < _waterAreaCaches.Count; i++)
            {
                _serializedWaterRimHeights[i] = _waterAreaCaches[i].rimHeight;
            }
        }

        public void ApplyGradientTexture()
        {
            if (gradientTexture == null)
            {
                gradientTexture = LoadOrCreateGradientTexture();
            }

            int resolution = gradientTexture.height;
            for (int y = 0; y < resolution; y++)
            {
                float t = y / (float)(resolution - 1);
                gradientTexture.SetPixel(0, y, heightGradient.Evaluate(t));
            }
            gradientTexture.Apply();

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(gradientTexture);
#endif

            var material = GetComponent<MeshRenderer>().sharedMaterial;
            if (material != null)
            {
                material.SetTexture("_BaseMap", gradientTexture);
            }
        }

        // gradientTexture 필드가 비어있을 때(이 지형 인스턴스가 처음 생성될 때)만 호출된다.
        // 예전엔 고정된 공유 경로(Assets/Materials/TerrainHeightGradient.asset)를 여러 지형이
        // 같이 재사용했는데, 씬을 복사하면 두 씬의 지형이 같은 파일을 계속 같이 덮어써서
        // 서로 영향을 주는 버그가 있었다 — 그래서 항상 이 오브젝트 이름 기준의 새 파일을
        // 만든다(겹치면 GenerateUniqueAssetPath가 알아서 번호를 붙임). 한 번 만들어지면
        // gradientTexture 필드에 영구히 저장되므로, 이후 재생성/씬 재로드에서는 이 메서드가
        // 다시 불리지 않고 항상 같은(그 지형만의) 파일을 계속 쓴다.
        Texture2D LoadOrCreateGradientTexture()
        {
            var texture = new Texture2D(1, 64, TextureFormat.RGBA32, false)
            {
                name = "TerrainHeightGradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

#if UNITY_EDITOR
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Materials"))
            {
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Materials");
            }
            string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath($"Assets/Materials/{AssetBaseName}_HeightGradient.asset");
            UnityEditor.AssetDatabase.CreateAsset(texture, path);
#endif

            return texture;
        }

        // 길이 정점(cellSize 간격)보다 훨씬 얇게 보이도록, 지형 XZ 전체를 덮는 고해상도
        // 마스크 텍스처를 따로 굽는다. heightGradient를 _BaseMap으로 굽는 것과 같은 패턴이며,
        // 메시가 이미 갖고 있는 평면 UV0으로 셰이더에서 픽셀 단위로 샘플링한다.
        void BakePathMaskTexture()
        {
            // 에셋을 새로 만드는 건 참조가 아예 없을 때뿐이다. 예전엔 해상도가 안 맞을 때도
            // 새로 만들었는데, _pathMaskTexelsResolution이 직렬화 안 되는 private 필드라
            // 도메인 리로드/씬 로드마다 0으로 초기화된다 — 그래서 씬을 열 때마다 매번
            // "해상도 불일치"로 판정되어 이미 있는 에셋을 버리고 새 파일을 만들었고,
            // Materials 폴더에 PathMask 사본이 계속 쌓였다. 해상도가 바뀐 경우엔 기존
            // 텍스처를 Reinitialize로 크기만 바꿔서 같은 에셋을 계속 쓴다.
            if (pathMaskTexture == null)
            {
                pathMaskTexture = LoadOrCreatePathMaskTexture();
            }
            else if (pathMaskTexture.width != pathMaskResolution || pathMaskTexture.height != pathMaskResolution)
            {
                pathMaskTexture.Reinitialize(pathMaskResolution, pathMaskResolution);
            }
            _pathMaskTexelsResolution = pathMaskResolution;

            int resolution = pathMaskResolution;
            _pathMaskTexels = new float[resolution * resolution];

            var worldCurves = BuildWorldCurves();
            var pixels = new Color[resolution * resolution];

            if (worldCurves.Count > 0)
            {
                // 텍셀 간격이 정점 간격보다 훨씬 촘촘하므로, 끊김 방지용 최소폭도 그만큼
                // 작게 잡아도 된다 — pathWidth=1처럼 작은 값도 실제로 얇게 나온다.
                float texelWorldSize = _sizeX / resolution;
                float effectiveWidth = Mathf.Max(pathWidth, texelWorldSize * 0.5f);
                float falloff = Mathf.Max(0.0001f, pathBlendWidth);

                for (int ty = 0; ty < resolution; ty++)
                {
                    for (int tx = 0; tx < resolution; tx++)
                    {
                        float u = (tx + 0.5f) / resolution;
                        float v = (ty + 0.5f) / resolution;
                        Vector2 worldPos = new Vector2(u * _sizeX, v * _sizeZ);
                        Vector2 warpedPos = WarpForMask(worldPos);

                        float bestMask = 0f;
                        foreach (var worldCurve in worldCurves)
                        {
                            float distance = DistanceToCurve(warpedPos, worldCurve, out _, out _);
                            float t01 = Mathf.Clamp01((distance - effectiveWidth) / falloff);
                            float m = 1f - Mathf.SmoothStep(0f, 1f, t01);
                            if (m > bestMask)
                            {
                                bestMask = m;
                            }
                        }

                        int texelIndex = ty * resolution + tx;
                        _pathMaskTexels[texelIndex] = bestMask;
                        pixels[texelIndex] = new Color(bestMask, bestMask, bestMask, 1f);
                    }
                }
            }
            else
            {
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = Color.black;
                }
            }

            pathMaskTexture.SetPixels(pixels);
            pathMaskTexture.Apply();

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(pathMaskTexture);
            // SetPixels/Apply는 메모리 상의 텍스처만 갱신한다 — 에셋 파일에 실제로 저장하고
            // Project 창 썸네일도 다시 그리게 하려면 저장까지 명시적으로 해줘야 한다.
            // (안 하면 씬/게임 뷰의 실제 렌더링은 이미 바뀐 대로 보이는데, 에셋 미리보기만
            // 예전 상태로 남아있는 것처럼 보일 수 있다.)
            UnityEditor.AssetDatabase.SaveAssets();
#endif

            var material = GetComponent<MeshRenderer>().sharedMaterial;
            if (material != null)
            {
                material.SetTexture("_PathMask", pathMaskTexture);
            }
        }

        // LoadOrCreateGradientTexture와 같은 이유로 고정 공유 경로 대신 항상 이 오브젝트
        // 이름 기준의 새 파일을 만든다. pathMaskTexture 필드가 비어있을 때만 호출된다.
        Texture2D LoadOrCreatePathMaskTexture()
        {
            var texture = new Texture2D(pathMaskResolution, pathMaskResolution, TextureFormat.RGBA32, false)
            {
                name = "TerrainPathMask",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

#if UNITY_EDITOR
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Materials"))
            {
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Materials");
            }
            string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath($"Assets/Materials/{AssetBaseName}_PathMask.asset");
            UnityEditor.AssetDatabase.CreateAsset(texture, path);
#endif

            return texture;
        }

        // 길 마스크와 같은 원리(별도 고해상도 텍스처로 경계 모양을 표현)를 호수 하나하나에
        // 적용한다. 각 호수는 자기 바운딩 박스만 덮는 평평한 쿼드 하나 + 그 안에서 호안
        // 모양을 그려내는 마스크 텍스처로 표현되므로, 폴리곤이 오목해도 삼각분할 같은 새
        // 기하 로직 없이 안전하게 동작한다.
        void BuildWaterSurfaces(float[] heights, float minHeight)
        {
            FindOrCreateWaterContainer();

            // 개수/모양이 바뀌었을 수 있으니 항상 자식을 지우고 다시 만든다
            // (VegetationScatter.ClearInstances와 동일한 파괴 패턴).
            for (int i = _waterContainer.childCount - 1; i >= 0; i--)
            {
                var child = _waterContainer.GetChild(i).gameObject;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(child);
                    continue;
                }
#endif
                Destroy(child);
            }

            if (_waterAreaCaches == null || _waterAreaCaches.Count == 0)
            {
                return;
            }

            var material = GetOrCreateWaterMaterial();
            if (material == null)
            {
                return;
            }

            // waterMaskTextures는 호수 개수만큼(호수마다 자기 bbox 모양의 마스크 텍스처
            // 하나씩) 채워진다. 개수가 바뀌었으면 기존에 만들어둔 텍스처는 인덱스가 맞는
            // 만큼 재사용하고 나머지만 새로 만든다.
            if (waterMaskTextures == null || waterMaskTextures.Length != _waterAreaCaches.Count)
            {
                var resized = new Texture2D[_waterAreaCaches.Count];
                for (int i = 0; i < resized.Length; i++)
                {
                    if (waterMaskTextures != null && i < waterMaskTextures.Length)
                    {
                        resized[i] = waterMaskTextures[i];
                    }
                }
                waterMaskTextures = resized;
            }

            for (int i = 0; i < _waterAreaCaches.Count; i++)
            {
                BuildOneWaterSurface(i, _waterAreaCaches[i], material, heights, minHeight);
            }
        }

        // 지형 네 변 바깥에 보이지 않는 벽(BoxCollider)을 세워서 플레이어가 지형 밖으로
        // 떨어지지 않게 한다. 지형 크기/높이는 Generate()마다 바뀔 수 있으므로 매번 다시
        // 계산한다. 콜라이더 4개를 GameObject 하나에 몰아 붙여서 계층을 어지럽히지 않는다.
        void BuildEdgeWalls()
        {
            var walls = transform.Find("EdgeWalls");
            if (walls == null)
            {
                if (!buildEdgeWalls)
                {
                    return;
                }
                var go = new GameObject("EdgeWalls");
                go.transform.SetParent(transform, false);
                walls = go.transform;
            }

            foreach (var existing in walls.GetComponents<BoxCollider>())
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(existing);
                    continue;
                }
#endif
                Destroy(existing);
            }

            if (!buildEdgeWalls)
            {
                return;
            }

            // 지형 로컬 y는 min-shift 때문에 0에서 시작하지만, 호수 카빙은 그 아래(음수)까지
            // 파고들 수 있다. 아래로도 넉넉히 내려서 파인 지점에서도 벽이 끊기지 않게 한다.
            float bottom = -_heightRange;
            float top = _heightRange + edgeWallHeight;
            float height = top - bottom;
            float centerY = (top + bottom) * 0.5f;
            float t = edgeWallThickness;
            float halfT = t * 0.5f;

            // 벽은 경계 "바깥쪽"에 붙인다 — 안쪽으로 들어오면 걸어다닐 수 있는 범위가 줄어든다.
            AddEdgeWall(walls, new Vector3(-halfT, centerY, _sizeZ * 0.5f), new Vector3(t, height, _sizeZ + t * 2f));
            AddEdgeWall(walls, new Vector3(_sizeX + halfT, centerY, _sizeZ * 0.5f), new Vector3(t, height, _sizeZ + t * 2f));
            AddEdgeWall(walls, new Vector3(_sizeX * 0.5f, centerY, -halfT), new Vector3(_sizeX + t * 2f, height, t));
            AddEdgeWall(walls, new Vector3(_sizeX * 0.5f, centerY, _sizeZ + halfT), new Vector3(_sizeX + t * 2f, height, t));
        }

        static void AddEdgeWall(Transform walls, Vector3 center, Vector3 size)
        {
            var box = walls.gameObject.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        // 호수는 실제 콜라이더가 없어서 NavMeshSurface를 Physics Colliders 기준으로 구우면
        // 호수 바닥도 걸어다닐 수 있는 땅으로 잡힌다 — 막아야 한다.
        //
        // 처음엔 IsInsideWaterArea(폴리곤+도메인워프 판정, 플레이어 물 회피와 같은 기준)로
        // 격자를 걸렀는데도 실제 물보다 넓게 막혔다. 원인은 판정 기준 자체가 달랐던 것 —
        // 화면에 실제로 보이는 물 경계는 폴리곤이 아니라 "카빙된 지형 높이가 물 표면보다
        // 낮은가"로 정해진다(BuildOneWaterSurface 주석 참고: 물 쿼드를 bbox 전체에 깔아두고
        // 불투명한 지형이 깊이 버퍼로 그 위를 가리는 방식이라, 호안 블렌드로 파인 정도가
        // 폴리곤 판정과 정확히 일치하지 않는다). 그래서 여기서도 폴리곤 판정 대신 카빙된
        // 실제 높이(heights, Generate()가 넘겨줌)를 물 표면 높이와 직접 비교한다 — 렌더링과
        // 정확히 같은 기준이라 시각적 물 경계와 어긋날 수가 없다.
        //
        // NavMeshModifierVolume은 축 정렬 박스뿐이라 물 모양을 그대로 표현할 수 없으므로,
        // cellSize/4 격자로 잘게 쪼개 칸마다 판정하고(굽은 강도 실제 폭만큼만 막히도록),
        // 같은 Z줄에서 연속으로 물인 칸은 박스 하나로 합쳐 오브젝트 수를 줄인다.
        void BuildWaterNavObstacles(float[] heights, float minHeight)
        {
            var container = FindOrCreateWaterNavContainer();

            foreach (var existing in container.GetComponentsInChildren<NavMeshModifierVolume>(true))
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(existing.gameObject);
                    continue;
                }
#endif
                Destroy(existing.gameObject);
            }

            if (_waterAreaCaches.Count == 0)
            {
                return;
            }

            // 지형 정점 간격(cellSize, 보통 10)을 그대로 쓰면 좁은 강 폭 정도의 굵기라
            // 격자 한 칸만 걸쳐도 실제 폭보다 훨씬 넓게 막힌다 — 훨씬 잘게 쪼갠다. 행
            // 단위로 합치므로 가늘어져도 오브젝트 수가 크게 늘지는 않는다.
            float cell = Mathf.Max(0.5f, cellSize * 0.25f);
            int notWalkableArea = NavMesh.GetAreaFromName("Not Walkable");
            int boxIndex = 0;

            for (int li = 0; li < _waterAreaCaches.Count; li++)
            {
                var cache = _waterAreaCaches[li];

                // BuildOneWaterSurface와 같은 이유로 호안 블렌드 폭만큼 여유를 둔다 — 카빙이
                // 원본 폴리곤 경계 바로 바깥까지 살짝 파고들 수 있어서, 딱 폴리곤 bbox만
                // 스캔하면 그 가장자리에서 물에 잠긴 칸을 놓칠 수 있다(구멍처럼 걸어다닐
                // 수 있는 틈이 남는 문제). 실제로 몇 칸이 막힐지는 아래 높이 비교가 알아서
                // 정확하게 가리므로, 여기서는 스캔 범위만 넉넉히 잡는다.
                float pad = Mathf.Max(0f, waterShoreBlendWidth);
                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                foreach (var p in cache.worldCurve)
                {
                    minX = Mathf.Min(minX, p.x);
                    maxX = Mathf.Max(maxX, p.x);
                    minZ = Mathf.Min(minZ, p.y);
                    maxZ = Mathf.Max(maxZ, p.y);
                }
                minX = Mathf.Clamp(minX - pad, 0f, _sizeX);
                maxX = Mathf.Clamp(maxX + pad, 0f, _sizeX);
                minZ = Mathf.Clamp(minZ - pad, 0f, _sizeZ);
                maxZ = Mathf.Clamp(maxZ + pad, 0f, _sizeZ);
                if (maxX <= minX || maxZ <= minZ)
                {
                    continue;
                }

                // BuildOneWaterSurface와 정확히 같은 물 표면 높이 — 이보다 낮게 카빙된
                // 지형만 "실제로 물에 잠긴 자리"다.
                float waterY = cache.rimHeight - Mathf.Max(0.01f, waterDepth * 0.005f);

                // 세로 범위는 지형 전체 높이(_heightRange)가 아니라 이 호수 하나가 실제로
                // 파인 깊이(rimHeight 기준 waterDepth)만큼만 잡는다 — EdgeWalls처럼 지형
                // 전체를 덮을 필요가 없다(막는 대상이 벽이 아니라 국소적인 호수 바닥이라).
                float margin = Mathf.Max(2f, waterDepth * 0.5f);
                float bottom = cache.rimHeight - waterDepth - margin;
                float top = cache.rimHeight + margin;
                float centerY = (bottom + top) * 0.5f;
                float height = top - bottom;

                int xCells = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / cell));
                int zCells = Mathf.Max(1, Mathf.CeilToInt((maxZ - minZ) / cell));

                for (int zi = 0; zi < zCells; zi++)
                {
                    float zLocal0 = minZ + zi * cell;
                    float zLocal1 = Mathf.Min(zLocal0 + cell, maxZ);
                    float zCenter = (zLocal0 + zLocal1) * 0.5f;

                    int runStartXi = -1;
                    for (int xi = 0; xi <= xCells; xi++)
                    {
                        bool inside = false;
                        if (xi < xCells)
                        {
                            float xLocal0 = minX + xi * cell;
                            float xLocal1 = Mathf.Min(xLocal0 + cell, maxX);
                            float xCenter = (xLocal0 + xLocal1) * 0.5f;
                            float terrainHeight = SampleHeightsBilinear(heights, xCenter / cellSize, zCenter / cellSize) - minHeight;
                            inside = terrainHeight < waterY;
                        }

                        if (inside && runStartXi < 0)
                        {
                            runStartXi = xi;
                        }
                        else if (!inside && runStartXi >= 0)
                        {
                            float runX0 = minX + runStartXi * cell;
                            float runX1 = Mathf.Min(minX + xi * cell, maxX);
                            CreateWaterNavBox(container, boxIndex++, runX0, runX1, zLocal0, zLocal1, centerY, height, notWalkableArea);
                            runStartXi = -1;
                        }
                    }
                }
            }
        }

        void CreateWaterNavBox(Transform container, int index, float x0, float x1, float z0, float z1,
            float centerY, float height, int notWalkableArea)
        {
            var go = new GameObject($"WaterNav_{index}", typeof(NavMeshModifierVolume));
            go.transform.SetParent(container, false);
            go.transform.localPosition = new Vector3((x0 + x1) * 0.5f, centerY, (z0 + z1) * 0.5f);

            var modifier = go.GetComponent<NavMeshModifierVolume>();
            modifier.size = new Vector3(x1 - x0, height, z1 - z0);
            modifier.area = notWalkableArea;
        }

        Transform FindOrCreateWaterNavContainer()
        {
            if (_waterNavContainer != null)
            {
                return _waterNavContainer;
            }

            var existing = transform.Find("WaterNavObstacles");
            if (existing == null)
            {
                var go = new GameObject("WaterNavObstacles");
                go.transform.SetParent(transform, false);
                existing = go.transform;
            }
            _waterNavContainer = existing;
            return _waterNavContainer;
        }

        // 월드 좌표를 격자 좌표(0..width-1, 0..length-1)로 바꾼다. GetWorldPositionAt의
        // 역변환 — 지형 격자는 XZ가 뒤틀리지 않은 균일 그리드라 나눗셈 한 번이면 된다.
        // IsInsideWaterArea처럼 격자 좌표를 받는 API를 월드 좌표에서 호출할 때 쓴다.
        public Vector2 WorldToGrid(Vector3 worldPos)
        {
            Vector3 local = transform.InverseTransformPoint(worldPos);
            float gx = Mathf.Clamp(local.x / cellSize, 0f, width - 1);
            float gz = Mathf.Clamp(local.z / cellSize, 0f, length - 1);
            return new Vector2(gx, gz);
        }

        void FindOrCreateWaterContainer()
        {
            if (_waterContainer != null)
            {
                return;
            }

            var existing = transform.Find("WaterAreas");
            if (existing == null)
            {
                var go = new GameObject("WaterAreas");
                go.transform.SetParent(transform, false);
                existing = go.transform;
            }
            _waterContainer = existing;
        }

        void BuildOneWaterSurface(int index, WaterAreaCache cache, Material material, float[] heights, float minHeight)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in cache.worldCurve)
            {
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.y);
                maxZ = Mathf.Max(maxZ, p.y);
            }

            // 마스크의 호안 페이드가 쿼드 가장자리에서 잘리지 않도록 블렌드 폭만큼 여유를
            // 두고, 지형 범위 밖으로 나가지 않게 clamp한다.
            float pad = Mathf.Max(0f, waterShoreBlendWidth);
            minX = Mathf.Clamp(minX - pad, 0f, _sizeX);
            maxX = Mathf.Clamp(maxX + pad, 0f, _sizeX);
            minZ = Mathf.Clamp(minZ - pad, 0f, _sizeZ);
            maxZ = Mathf.Clamp(maxZ + pad, 0f, _sizeZ);
            if (maxX <= minX || maxZ <= minZ)
            {
                return;
            }

            var maskTexture = LoadOrCreateWaterMaskTexture(index);
            waterMaskTextures[index] = maskTexture;
            BakeWaterMaskTexture(maskTexture, cache, minX, minZ, maxX, maxZ, heights, minHeight);

            var go = new GameObject($"Water_{index}");
            go.transform.SetParent(_waterContainer, false);

            // ApplyWaterCarving의 ceiling 클램프는 호안(mask=0)에서 지형을 정확히
            // rimHeight로 깎는다. 물 평면을 딱 그 높이에 두면 두 면이 같은 평면이 되어
            // 물가를 따라 z-fighting(지글거림)이 생기므로, 눈에 안 띌 만큼만 아래로
            // 내려서 겹침을 없앤다 — 수심 0인 자리는 어차피 물이 안 보여야 하는 곳이라
            // 이만큼 물가가 안쪽으로 당겨지는 건 시각적으로 무의미하다.
            float y = cache.rimHeight - Mathf.Max(0.01f, waterDepth * 0.005f);
            var mesh = new Mesh { name = $"WaterSurface_{index}" };
            mesh.vertices = new[]
            {
                new Vector3(minX, y, minZ),
                new Vector3(minX, y, maxZ),
                new Vector3(maxX, y, maxZ),
                new Vector3(maxX, y, minZ),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f),
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            AssignReflectionProbeAnchor(meshRenderer, new Vector3((minX + maxX) * 0.5f, y, (minZ + maxZ) * 0.5f));

            // 호수마다 모양이 다른 마스크 텍스처만 개체별로 오버라이드한다(TreeViewOcclusionFader가
            // 알파를 오버라이드하는 것과 같은 기법) — 머티리얼 인스턴스를 호수 개수만큼 만들지 않는다.
            var block = new MaterialPropertyBlock();
            block.SetTexture("_MaskTex", maskTexture);
            meshRenderer.SetPropertyBlock(block);
        }

        // 유니티는 앵커가 없으면 "렌더러 bounds 중심"이 프로브 박스 안에 들어와야 그 반사
        // 프로브를 할당한다 — 물 쿼드는 호수 bbox 전체를 덮는 큰 평면이라 중심이 박스 밖으로
        // 벗어나기 쉽고, 그러면 프로브를 놔둬도 스카이박스 폴백만 비친다. 가장 가까운 프로브의
        // Transform을 앵커로 물려주면 그 프로브가 항상 선택된다. 인스펙터에서 손으로 넣는
        // 방법도 있지만, 물 오브젝트는 Generate()마다 파괴 후 재생성되므로 매번 날아간다.
        void AssignReflectionProbeAnchor(MeshRenderer meshRenderer, Vector3 localCenter)
        {
            Vector3 worldCenter = transform.TransformPoint(localCenter);

            Transform nearest = null;
            float nearestSqr = float.MaxValue;
            foreach (var probe in FindObjectsOfType<ReflectionProbe>())
            {
                if (!probe.isActiveAndEnabled)
                {
                    continue;
                }

                float sqr = (probe.transform.position - worldCenter).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = probe.transform;
                }
            }

            if (nearest != null)
            {
                meshRenderer.probeAnchor = nearest;
            }
        }

        // BakePathMaskTexture와 같은 굽기 방식이지만, 전체 지형이 아니라 호수 하나의
        // 바운딩 박스([minX,maxX] x [minZ,maxZ])만 덮는다.
        //
        // 중요: 이 마스크는 더 이상 "물가(shoreline)를 그려내는" 용도가 아니다. 물가는
        // 물 쿼드를 bbox 전체에 그대로 깔아두고 깊이 버퍼가 자르게 한다 — 지형(불투명)이
        // 먼저 그려지며 깊이를 쓰므로, 수면보다 높은 지형은 물을 픽셀 단위로 정확히 가린다.
        // 마스크로 물가를 그리던 방식은 텍스처 해상도와 정점 격자 보간 오차 때문에 실제
        // 지형 삼각형과 완벽히 일치할 수 없어 미세한 틈이 계속 남았는데, 깊이 버퍼는 실제
        // 렌더링된 지오메트리 그 자체라 틈이 원천적으로 생길 수 없다.
        //
        // 그래서 이 텍스처가 지금 하는 일은 둘뿐이다:
        //   R 채널 = 사용자가 그린 폴리곤 안쪽인지 — bbox 구석의 경사면처럼 수면보다 낮되
        //            호수는 아닌 자리로 물이 번지는 것만 막는다(물가 자체는 안 그린다).
        //   G 채널 = 정규화된 수심(얕은 색 ↔ 깊은 색 보간용, 순수 색상 용도).
        void BakeWaterMaskTexture(Texture2D texture, WaterAreaCache cache, float minX, float minZ, float maxX, float maxZ,
            float[] heights, float minHeight)
        {
            int resolution = waterMaskResolution;
            var pixels = new Color[resolution * resolution];
            float bboxSizeX = Mathf.Max(0.0001f, maxX - minX);
            float bboxSizeZ = Mathf.Max(0.0001f, maxZ - minZ);
            float depthRange = Mathf.Max(0.0001f, waterDepth);

            // BuildOneWaterSurface가 물 쿼드를 놓는 높이와 정확히 같아야 한다 — 이 높이보다
            // 낮게 파인 지형이 곧 "물에 잠긴 자리"다.
            float waterY = cache.rimHeight - Mathf.Max(0.01f, waterDepth * 0.005f);
            // 정점 격자(cellSize) 해상도로 카빙된 지형과 텍셀 해상도 마스크 사이의 보간
            // 오차만큼 여유를 둬서, 경계에서 한 텍셀씩 모자라 구멍이 나지 않게 한다.
            float tolerance = Mathf.Max(0.01f, waterDepth * 0.01f);

            var coverage = new bool[resolution * resolution];
            var belowWater = new bool[resolution * resolution];
            var depths = new float[resolution * resolution];
            var queue = new Queue<int>();

            for (int ty = 0; ty < resolution; ty++)
            {
                for (int tx = 0; tx < resolution; tx++)
                {
                    int i = ty * resolution + tx;
                    float u = (tx + 0.5f) / resolution;
                    float v = (ty + 0.5f) / resolution;
                    Vector2 worldPos = new Vector2(minX + u * bboxSizeX, minZ + v * bboxSizeZ);

                    // 깊이는 워핑하지 않은 실좌표에서 읽는다 — ApplyWaterCarving이
                    // heights[i]를 갱신한 것도 정점의 실좌표 기준이었다.
                    float carvedHeight = SampleHeightsBilinear(heights, worldPos.x / cellSize, worldPos.y / cellSize) - minHeight;
                    depths[i] = Mathf.Clamp01((cache.rimHeight - carvedHeight) / depthRange);
                    belowWater[i] = carvedHeight < waterY + tolerance;

                    // 폴리곤 안쪽(카빙과 동일하게 워핑된 좌표로 판정)은 무조건 덮는다 —
                    // 그 자리 지형이 물 위로 솟아 있으면 어차피 깊이 버퍼가 가려준다.
                    if (IsPointInPolygon(WarpForWaterMask(worldPos), cache.worldCurve))
                    {
                        coverage[i] = true;
                        queue.Enqueue(i);
                    }
                }
            }

            // 폴리곤 바깥이라도 실제로 물 평면 아래까지 파인 자리는 물로 덮어야 한다 —
            // 카빙은 정점 격자(cellSize) 해상도라 매끄러운 폴리곤 곡선 바깥으로 삐져나오는데,
            // 마스크가 곡선에서 딱 잘리면 "파였는데 물이 없는 빈 구멍"이 보인다(마스크는
            // 실제 보이는 물의 상위집합이어야 하고, 정밀한 물가는 깊이 버퍼가 만든다).
            // 반대로 호수와 이어지지 않은 저지대(bbox 구석의 골짜기 등)로 번지지는 않도록,
            // 폴리곤에서 출발해 물에 잠긴 텍셀로만 이어붙인다(연결 성분 확장).
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int tx = i % resolution;
                int ty = i / resolution;

                ExpandWaterCoverage(tx - 1, ty, resolution, coverage, belowWater, queue);
                ExpandWaterCoverage(tx + 1, ty, resolution, coverage, belowWater, queue);
                ExpandWaterCoverage(tx, ty - 1, resolution, coverage, belowWater, queue);
                ExpandWaterCoverage(tx, ty + 1, resolution, coverage, belowWater, queue);
            }

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(coverage[i] ? 1f : 0f, depths[i], 0f, 1f);
            }

            texture.SetPixels(pixels);
            texture.Apply();

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(texture);
            UnityEditor.AssetDatabase.SaveAssets();
#endif
        }

        // 물에 잠긴(belowWater) 이웃 텍셀로만 coverage를 넓힌다 — BakeWaterMaskTexture의
        // 연결 성분 확장에서만 쓴다.
        static void ExpandWaterCoverage(int tx, int ty, int resolution, bool[] coverage, bool[] belowWater, Queue<int> queue)
        {
            if (tx < 0 || ty < 0 || tx >= resolution || ty >= resolution)
            {
                return;
            }

            int i = ty * resolution + tx;
            if (coverage[i] || !belowWater[i])
            {
                return;
            }

            coverage[i] = true;
            queue.Enqueue(i);
        }

        Texture2D LoadOrCreateWaterMaskTexture(int index)
        {
            var alreadyAssigned = (waterMaskTextures != null && index < waterMaskTextures.Length) ? waterMaskTextures[index] : null;
            if (alreadyAssigned != null)
            {
                if (alreadyAssigned.width != waterMaskResolution || alreadyAssigned.height != waterMaskResolution)
                {
                    alreadyAssigned.Reinitialize(waterMaskResolution, waterMaskResolution);
                }
                alreadyAssigned.wrapMode = TextureWrapMode.Clamp;
                alreadyAssigned.filterMode = FilterMode.Bilinear;
                return alreadyAssigned;
            }

            var texture = new Texture2D(waterMaskResolution, waterMaskResolution, TextureFormat.RGBA32, false)
            {
                name = $"TerrainWaterMask_{index}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

#if UNITY_EDITOR
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Materials"))
            {
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Materials");
            }
            string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath($"Assets/Materials/{AssetBaseName}_WaterMask_{index}.asset");
            UnityEditor.AssetDatabase.CreateAsset(texture, path);
#endif

            return texture;
        }

        Material GetOrCreateWaterMaterial()
        {
            if (waterMaterial != null)
            {
                return waterMaterial;
            }

            var shader = Shader.Find("Mountains/WaterSurface");
            if (shader == null)
            {
                Debug.LogWarning("[ProceduralTerrainMesh] Mountains/WaterSurface 셰이더를 찾을 수 없어 물 표면 머티리얼을 만들지 못했습니다.");
                return null;
            }

            var material = new Material(shader) { name = "WaterSurface" };

#if UNITY_EDITOR
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Materials"))
            {
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Materials");
            }
            string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath($"Assets/Materials/{AssetBaseName}_WaterSurface.mat");
            UnityEditor.AssetDatabase.CreateAsset(material, path);
#endif

            waterMaterial = material;
            return waterMaterial;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying || settings == null) return;

            ApplyGradientTexture();

            var material = GetComponent<MeshRenderer>().sharedMaterial;
            if (material != null)
            {
                UnityEditor.EditorUtility.SetDirty(material);
            }
        }
#endif

        // 지형의 실제 최저~최고 높이 차이(월드 단위). TerrainBlend 셰이더의
        // _MaxHeight 프로퍼티를 지형 실측값에 맞춰 자동으로 세팅할 때 쓴다.
        public float HeightRange => _heightRange;

        public Vector3 GetVertexWorldPosition(int x, int z)
        {
            if (!TryGetVertexIndex(x, z, out int i))
            {
                return transform.position;
            }
            return transform.TransformPoint(_cachedVertices[i]);
        }

        // 해당 정점의 월드 공간 노멀. 식생/오브젝트를 지형 경사에 맞춰 세우거나
        // 경사도를 판정할 때 사용한다.
        public Vector3 GetVertexNormal(int x, int z)
        {
            if (!TryGetVertexIndex(x, z, out int i))
            {
                return Vector3.up;
            }
            return transform.TransformDirection(_cachedNormals[i]);
        }

        // gx/gz는 0..width-1 / 0..length-1 범위의 "실수" 격자 좌표(정점 사이 값도 허용).
        // 정점 격자에 딱 붙은 위치만 나오면(예: 식생 배치) 부자연스럽게 규칙적으로 보이므로,
        // 정점 사이 임의 지점을 양선형 보간으로 구할 때 쓴다.
        public Vector3 GetWorldPositionAt(float gx, float gz)
        {
            if (!TryGetBilinearCorners(gx, gz, out int x0, out int z0, out int x1, out int z1, out float tx, out float tz))
            {
                return transform.position;
            }

            Vector3 v00 = _cachedVertices[z0 * width + x0];
            Vector3 v10 = _cachedVertices[z0 * width + x1];
            Vector3 v01 = _cachedVertices[z1 * width + x0];
            Vector3 v11 = _cachedVertices[z1 * width + x1];
            Vector3 local = Vector3.Lerp(Vector3.Lerp(v00, v10, tx), Vector3.Lerp(v01, v11, tx), tz);
            return transform.TransformPoint(local);
        }

        // GetWorldPositionAt과 짝을 이루는 노멀 버전.
        public Vector3 GetNormalAt(float gx, float gz)
        {
            if (!TryGetBilinearCorners(gx, gz, out int x0, out int z0, out int x1, out int z1, out float tx, out float tz))
            {
                return Vector3.up;
            }

            Vector3 n00 = _cachedNormals[z0 * width + x0];
            Vector3 n10 = _cachedNormals[z0 * width + x1];
            Vector3 n01 = _cachedNormals[z1 * width + x0];
            Vector3 n11 = _cachedNormals[z1 * width + x1];
            Vector3 local = Vector3.Lerp(Vector3.Lerp(n00, n10, tx), Vector3.Lerp(n01, n11, tx), tz).normalized;
            return transform.TransformDirection(local);
        }

        bool TryGetBilinearCorners(float gx, float gz, out int x0, out int z0, out int x1, out int z1, out float tx, out float tz)
        {
            x0 = z0 = x1 = z1 = 0;
            tx = tz = 0f;

            if (_mesh == null || _mesh.vertexCount != width * length)
            {
                return false;
            }

            gx = Mathf.Clamp(gx, 0f, width - 1);
            gz = Mathf.Clamp(gz, 0f, length - 1);

            x0 = Mathf.FloorToInt(gx);
            z0 = Mathf.FloorToInt(gz);
            x1 = Mathf.Min(x0 + 1, width - 1);
            z1 = Mathf.Min(z0 + 1, length - 1);
            tx = gx - x0;
            tz = gz - z0;
            return true;
        }

        // 0(최저점) ~ 1(최고점)로 정규화된 높이. 색상 그라디언트에 쓰는 것과 같은 값이라
        // 식생 배치 등 다른 시스템에서도 동일한 기준으로 높이를 판정할 수 있다.
        public float GetNormalizedHeightAt(int x, int z)
        {
            if (!TryGetVertexIndex(x, z, out int i))
            {
                return 0f;
            }
            return _cachedVertices[i].y / _heightRange;
        }

        // 경사도. 0=평지, 1=수직절벽. TerrainBlend 셰이더의 슬로프 계산과
        // 동일한 정의(1 - normal.y)를 써서 다른 시스템과 기준을 맞춘다.
        public float GetSlopeAt(int x, int z)
        {
            if (!TryGetVertexIndex(x, z, out int i))
            {
                return 0f;
            }
            return 1f - Mathf.Clamp01(_cachedNormals[i].y);
        }

        // 길 마스크(0=길 아님, 1=길 중심). BakePathMaskTexture가 구운 고해상도 텍셀
        // 배열을 양선형 보간해서 읽는다 — 식생 배치 등에서 길 위를 피하고 싶을 때 쓴다.
        public float GetPathMaskAt(int x, int z) => GetPathMaskAt((float)x, (float)z);

        // gx/gz는 GetWorldPositionAt처럼 정점 사이 값도 허용하는 실수 격자 좌표. 식생
        // 배치가 정수 격자점이 아니라 지터링된(격자 한 칸 안에서 흔든) 위치에 실제로
        // 심으므로, avoidPath 판정도 그 흔든 위치 기준으로 해야 정확하다 — 정수 격자점만
        // 검사하면 흔든 위치가 길 안쪽으로 들어가 있어도 못 걸러낸다.
        public float GetPathMaskAt(float gx, float gz)
        {
            if (_pathMaskTexels == null || _pathMaskTexelsResolution <= 0)
            {
                return 0f;
            }

            float u = _sizeX > 0f ? (gx * cellSize) / _sizeX : 0f;
            float v = _sizeZ > 0f ? (gz * cellSize) / _sizeZ : 0f;
            return SamplePathMaskTexelsBilinear(u, v);
        }

        // 호수 영역 안/밖 판정(폴리곤 내부면 true). 길 마스크(GetPathMaskAt)와 달리
        // 미리 구운 텍셀 배열이 아니라 카빙과 완전히 같은 폴리곤 판정을 그 자리에서 다시
        // 계산한다 — 호수는 개수가 적고 식생 배치 시에만 호출되므로 캐시할 필요가 없다.
        //
        // 깊이(수심) 값이 아니라 boolean인 이유: 예전엔 "거리 기반 깊이 마스크 > 문턱값"으로
        // 회피 판정을 했는데, 호수가 작거나 폴리곤이 좁아지는 곳에서는 내부인데도 그 값이
        // 거의 0에 머물러 회피가 안 걸렸다(물 위에 나무가 생기던 버그). 식생 입장에선
        // "물속인가"만 알면 되므로 깊이를 아예 안 쓰는 판정으로 단순화했다.
        public bool IsInsideWaterArea(float gx, float gz)
        {
            if (_waterAreaCaches == null || _waterAreaCaches.Count == 0)
            {
                return false;
            }

            Vector2 worldPos = new Vector2(gx * cellSize, gz * cellSize);
            Vector2 warpedPos = WarpForWaterMask(worldPos);

            foreach (var cache in _waterAreaCaches)
            {
                if (IsPointInPolygon(warpedPos, cache.worldCurve))
                {
                    return true;
                }
            }
            return false;
        }

        float SamplePathMaskTexelsBilinear(float u, float v)
        {
            int resolution = _pathMaskTexelsResolution;
            float fx = Mathf.Clamp01(u) * resolution - 0.5f;
            float fz = Mathf.Clamp01(v) * resolution - 0.5f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, resolution - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(fz), 0, resolution - 1);
            int x1 = Mathf.Min(x0 + 1, resolution - 1);
            int z1 = Mathf.Min(z0 + 1, resolution - 1);
            float tx = Mathf.Clamp01(fx - x0);
            float tz = Mathf.Clamp01(fz - z0);

            float v00 = _pathMaskTexels[z0 * resolution + x0];
            float v10 = _pathMaskTexels[z0 * resolution + x1];
            float v01 = _pathMaskTexels[z1 * resolution + x0];
            float v11 = _pathMaskTexels[z1 * resolution + x1];
            return Mathf.Lerp(Mathf.Lerp(v00, v10, tx), Mathf.Lerp(v01, v11, tx), tz);
        }

        bool TryGetVertexIndex(int x, int z, out int index)
        {
            index = -1;
            if (_mesh == null || _mesh.vertexCount != width * length)
            {
                return false;
            }

            x = Mathf.Clamp(x, 0, width - 1);
            z = Mathf.Clamp(z, 0, length - 1);
            index = z * width + x;
            return true;
        }

        // 3x3 박스 블러(자기 자신 포함, 가장자리는 존재하는 이웃만으로 평균). 격자 경계는
        // 이웃 수가 줄어드는 만큼만 평균에 반영해서 가장자리가 찌그러지지 않게 한다.
        float[] SmoothHeights(float[] src)
        {
            var dst = new float[src.Length];
            for (int z = 0; z < length; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0f;
                    int count = 0;
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        int nz = z + dz;
                        if (nz < 0 || nz >= length) continue;

                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx;
                            if (nx < 0 || nx >= width) continue;

                            sum += src[nz * width + nx];
                            count++;
                        }
                    }
                    dst[z * width + x] = sum / count;
                }
            }
            return dst;
        }

        // 곡선 하나(월드 좌표) + 그 위 각 점의 목표 고도를 함께 들고 있는 캐시. 여러 길을
        // 동시에 순회할 때 길마다 한 번씩만 계산해두고 재사용하려고 묶었다.
        class PathCurveCache
        {
            public List<Vector2> worldCurve;
            public float[] elevations;
        }

        // 모든 길(paths)에 대해 곡선 + 곡선 위 고도값을 미리 계산해둔다. 고도가 필요한
        // ApplyPathFlattening 전용 — BakePathMaskTexture는 고도가 필요 없어서 더 가벼운
        // BuildWorldCurves를 따로 쓴다. 웨이포인트가 2개 미만인 길은 건너뛴다.
        List<PathCurveCache> BuildPathCurveCaches(float[] heights)
        {
            var caches = new List<PathCurveCache>();
            if (paths == null)
            {
                return caches;
            }

            foreach (var path in paths)
            {
                var curve = BuildSmoothPath(path?.waypoints, pathCurveSamplesPerSegment);
                if (curve.Count < 2)
                {
                    continue;
                }

                var elevations = new float[curve.Count];
                for (int i = 0; i < curve.Count; i++)
                {
                    elevations[i] = SampleHeightsBilinear(heights, curve[i].x, curve[i].y);
                }

                caches.Add(new PathCurveCache { worldCurve = ToWorldSpace(curve), elevations = elevations });
            }

            return caches;
        }

        // 고도 없이 곡선(월드 좌표)만 필요한 곳(BakePathMaskTexture)을 위한 가벼운 버전.
        List<List<Vector2>> BuildWorldCurves()
        {
            var result = new List<List<Vector2>>();
            if (paths == null)
            {
                return result;
            }

            foreach (var path in paths)
            {
                var curve = BuildSmoothPath(path?.waypoints, pathCurveSamplesPerSegment);
                if (curve.Count < 2)
                {
                    continue;
                }
                result.Add(ToWorldSpace(curve));
            }

            return result;
        }

        // 웨이포인트를 따라 heights(정점 높이)를 평탄화한다. 길이 하나도 없으면(또는 전부
        // 웨이포인트 2개 미만이면) 아무것도 하지 않는다. 시각적으로 보이는 길 폭은 이 정점
        // 기반 평탄화와는 별도로 BakePathMaskTexture가 훨씬 촘촘한 텍스처로 굽는다(정점
        // 해상도 한계를 안 받음).
        void ApplyPathFlattening(float[] heights)
        {
            var caches = BuildPathCurveCaches(heights);
            if (caches.Count == 0)
            {
                return;
            }

            // 정점은 cellSize 간격(기본 10)으로만 존재하므로, pathWidth를 그보다 훨씬 작게
            // 주면(예: 1) 어느 정점도 그 반경 안에 들지 못하는 구간이 생겨 길이 끊겨 보인다.
            // 격자 절반 간격을 최소값으로 못박아 항상 최소 한 줄은 걸리도록 보장한다.
            float effectivePathWidth = Mathf.Max(pathWidth, cellSize * 0.5f);
            float falloff = Mathf.Max(0.0001f, pathBlendWidth);

            for (int z = 0; z < length; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = z * width + x;
                    Vector2 worldPos = new Vector2(x * cellSize, z * cellSize);
                    // WarpForMask는 길과 무관하게 좌표 자체를 뒤트는 함수라 정점당 한 번만 계산한다.
                    Vector2 warpedPos = WarpForMask(worldPos);

                    float bestMask = 0f;
                    float bestElevation = heights[i];

                    foreach (var cache in caches)
                    {
                        float distance = DistanceToCurve(worldPos, cache.worldCurve, out float t, out int segment);
                        float elevation = Mathf.Lerp(cache.elevations[segment], cache.elevations[segment + 1], t);

                        // 마스크는 실제 좌표가 아니라 노이즈로 뒤튼 좌표 기준 거리로 계산해서
                        // 경계가 출렁이며 서로 파고들게 한다. 고도는 뒤틀리지 않은 실제 위치
                        // 기준을 그대로 써서 지형 자체의 경사는 왜곡되지 않게 한다.
                        float warpedDistance = DistanceToCurve(warpedPos, cache.worldCurve, out _, out _);
                        float t01 = Mathf.Clamp01((warpedDistance - effectivePathWidth) / falloff);
                        float m = 1f - Mathf.SmoothStep(0f, 1f, t01);

                        if (m > bestMask)
                        {
                            bestMask = m;
                            bestElevation = elevation;
                        }
                    }

                    if (bestMask > 0f)
                    {
                        heights[i] = Mathf.Lerp(heights[i], bestElevation, bestMask * pathFlattenStrength);
                    }
                }
            }
        }

        // 호수 하나(닫힌 폴리곤 곡선 + 그 곡선의 림 높이)를 들고 있는 캐시. rimHeight는
        // ApplyWaterCarving이 heights를 건드리기 전에 원래 지형에서 샘플링한 값이라, 아직
        // min-shift(최저점을 y=0으로 맞추는 것) 적용 전이다 — Generate()가 min-shift를 끝낸
        // 직후 별도로 보정해서 BuildWaterSurfaces가 최종 로컬 Y로 쓴다.
        class WaterAreaCache
        {
            public List<Vector2> worldCurve; // 닫힘 세그먼트 포함
            public float rimHeight;
        }

        List<WaterAreaCache> BuildWaterAreaCaches(float[] heights)
        {
            var caches = new List<WaterAreaCache>();
            if (waterAreas == null)
            {
                return caches;
            }

            foreach (var area in waterAreas)
            {
                var waypoints = area?.waypoints;
                if (waypoints == null || waypoints.Length < 3)
                {
                    continue;
                }

                var curve = BuildSmoothClosedPath(waypoints, waterCurveSamplesPerSegment);
                if (curve.Count < 3)
                {
                    continue;
                }

                // 수면 높이는 경계 높이의 "평균"이 아니라 "최저점"이다 — 실제 호수가 림에서
                // 가장 낮은 지점까지만 차오르는 것과 같은 이치. 평균을 쓰면 경사면에 걸친
                // 호수의 낮은 쪽 경계에서 수면이 지형보다 높아져, 물이 벽처럼 공중에 떠
                // 보였다. 최저점을 쓰면 수면이 모든 경계점 이하라는 게 보장되어 그런 일이
                // 구조적으로 생길 수 없다. 웨이포인트만이 아니라 실제로 쓰이는 스플라인
                // 곡선 전체를 샘플링해서, 웨이포인트 사이가 움푹 꺼진 경우도 놓치지 않는다.
                float rimHeight = float.MaxValue;
                foreach (var p in curve)
                {
                    rimHeight = Mathf.Min(rimHeight, SampleHeightsBilinear(heights, p.x, p.y));
                }

                caches.Add(new WaterAreaCache { worldCurve = ToWorldSpace(curve), rimHeight = rimHeight });
            }

            return caches;
        }

        // 웨이포인트를 따라 heights(정점 높이)를 파낸다. 길 평탄화(ApplyPathFlattening)와
        // 같은 거리-마스크 패턴이지만, 마스크를 적용하기 전에 반드시 폴리곤 안쪽인지부터
        // 판정한다 — 그래서 경계 밖 지형은 구조적으로 절대 영향을 받지 않는다.
        void ApplyWaterCarving(float[] heights)
        {
            var caches = BuildWaterAreaCaches(heights);
            _waterAreaCaches = caches;
            if (caches.Count == 0)
            {
                return;
            }

            float falloff = Mathf.Max(0.0001f, waterShoreBlendWidth);

            for (int z = 0; z < length; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = z * width + x;
                    Vector2 worldPos = new Vector2(x * cellSize, z * cellSize);
                    Vector2 warpedPos = WarpForWaterMask(worldPos);

                    float bestMask = 0f;
                    float bestFloorHeight = 0f;
                    bool anyInside = false;

                    foreach (var cache in caches)
                    {
                        if (!IsPointInPolygon(warpedPos, cache.worldCurve))
                        {
                            continue;
                        }

                        float distance = DistanceToCurve(warpedPos, cache.worldCurve, out _, out _);
                        float t01 = Mathf.Clamp01(distance / falloff);
                        // 호안=0, 깊은 안쪽=1이되 waterCliffSharpness로 급격히 끌어올려서,
                        // 호안 근처 좁은 폭에서만 가파르게 깊어지고 나머지는 평평한 바닥으로
                        // 남는 절벽형 프로파일을 만든다.
                        float mask = Mathf.SmoothStep(0f, 1f, SharpenMask01(t01, waterCliffSharpness));

                        if (!anyInside || mask > bestMask)
                        {
                            anyInside = true;
                            bestMask = mask;
                            bestFloorHeight = cache.rimHeight - waterDepth;
                        }
                    }

                    if (anyInside)
                    {
                        // Lerp(원래 높이, floorHeight, mask)를 쓰면, 폴리곤 안쪽이 원래
                        // 언덕/경사처럼 rimHeight보다 훨씬 높았을 때 mask가 아직 1에 못
                        // 미친 구간(호안 근처)에서는 못 다 파여서 물 표면(rimHeight)보다
                        // 높게 남는다 — 그러면 물이 그 지형에 파묻히기는커녕 오히려 위에
                        // 붕 뜬 것처럼 보인다. 대신 "이 mask에서 허용되는 최고 높이(ceiling)"
                        // 위로는 절대 못 넘도록 Min으로 깎는다 — ceiling은 호안(mask=0)에서
                        // rimHeight, 깊은 안쪽(mask=1)에서 floorHeight로 선형 보간되므로
                        // 항상 rimHeight 이하이고, 그 결과 카빙된 지형은 어떤 mask 값에서도
                        // 절대 물 표면보다 높아질 수 없다는 게 수학적으로 보장된다. 원래
                        // 지형이 이미 ceiling보다 낮았던 자리(자연스러운 저지대)는 그대로 둔다.
                        float ceiling = bestFloorHeight + waterDepth * (1f - bestMask);
                        heights[i] = Mathf.Min(heights[i], ceiling);
                    }
                }
            }

            // 정점은 cellSize(기본 10) 간격으로만 존재해서, 호수가 그보다 작으면 폴리곤
            // 안쪽에 정점이 단 하나도 안 걸릴 수 있다(길의 effectivePathWidth와 같은 문제 —
            // pathWidth가 격자보다 작을 때 길이 끊겨 보이던 것과 동일한 원인). 그러면 지형은
            // 하나도 안 파였는데 물 표면(정밀한 곡선 기준 rimHeight)만 정상적으로 그려져서,
            // 물이 지형 속에 파묻힌 것처럼 보인다. 각 호수의 중심에 가장 가까운 정점 하나는
            // 격자 해상도와 무관하게 항상 바닥 높이까지 확실히 파이도록 보장한다.
            foreach (var cache in caches)
            {
                Vector2 centroid = AverageOfCurvePoints(cache.worldCurve);
                int cx = Mathf.Clamp(Mathf.RoundToInt(centroid.x / cellSize), 0, width - 1);
                int cz = Mathf.Clamp(Mathf.RoundToInt(centroid.y / cellSize), 0, length - 1);
                heights[cz * width + cx] = cache.rimHeight - waterDepth;
            }
        }

        static Vector2 AverageOfCurvePoints(List<Vector2> curve)
        {
            Vector2 sum = Vector2.zero;
            foreach (var p in curve)
            {
                sum += p;
            }
            return sum / Mathf.Max(1, curve.Count);
        }

        // 표준 ray-casting(even-odd) 폴리곤 내부 판정. worldPolygon은 닫힘 세그먼트(마지막
        // 점 == 첫 점)가 포함되어 있어도 없어도 결과가 같다 — 어느 쪽이든 각 변을 한 번씩만 센다.
        static bool IsPointInPolygon(Vector2 point, List<Vector2> worldPolygon)
        {
            bool inside = false;
            int n = worldPolygon.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 pi = worldPolygon[i];
                Vector2 pj = worldPolygon[j];

                bool crosses = (pi.y > point.y) != (pj.y > point.y);
                if (crosses)
                {
                    float xIntersect = (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x;
                    if (point.x < xIntersect)
                    {
                        inside = !inside;
                    }
                }
            }
            return inside;
        }

        List<Vector2> ToWorldSpace(List<Vector2> curveInGridSpace)
        {
            var result = new List<Vector2>(curveInGridSpace.Count);
            for (int i = 0; i < curveInGridSpace.Count; i++)
            {
                result.Add(curveInGridSpace[i] * cellSize);
            }
            return result;
        }

        // curve(월드 좌표 리스트) 위에서 worldPos와 가장 가까운 점까지의 거리를 구한다.
        // ApplyPathFlattening(정점 평탄화)과 BakePathMaskTexture(시각 마스크) 양쪽이
        // 공유하는 "곡선까지 최단거리" 로직 — 세그먼트 인덱스/보간 t도 함께 돌려줘서
        // 호출부가 해당 구간의 목표 고도 등을 이어서 계산할 수 있게 한다.
        static float DistanceToCurve(Vector2 worldPos, List<Vector2> worldCurve, out float bestT, out int bestSegment)
        {
            float bestDistSq = float.MaxValue;
            bestT = 0f;
            bestSegment = 0;

            for (int s = 0; s < worldCurve.Count - 1; s++)
            {
                Vector2 a = worldCurve[s];
                Vector2 b = worldCurve[s + 1];
                Vector2 ab = b - a;
                float abLenSq = ab.sqrMagnitude;
                float t = abLenSq > 1e-6f ? Mathf.Clamp01(Vector2.Dot(worldPos - a, ab) / abLenSq) : 0f;
                Vector2 closest = a + ab * t;
                float distSq = (worldPos - closest).sqrMagnitude;

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestT = t;
                    bestSegment = s;
                }
            }

            return Mathf.Sqrt(bestDistSq);
        }

        // 길 호안용 도메인 워핑(WarpDomain 참고) — pathEdgeJitter/pathBlendWidth 기준.
        Vector2 WarpForMask(Vector2 worldPos)
        {
            return WarpDomain(worldPos, pathEdgeJitter, Mathf.Max(1f, pathBlendWidth), _warpOffsetA, _warpOffsetB);
        }

        // 호수 호안용 도메인 워핑 — WarpForMask와 같은 기법이지만 별도 세기(waterEdgeJitter)와
        // 별도 오프셋(길과 호수가 같은 시드에서도 서로 다르게 흔들리도록)을 쓴다.
        Vector2 WarpForWaterMask(Vector2 worldPos)
        {
            return WarpDomain(worldPos, waterEdgeJitter, Mathf.Max(1f, waterShoreBlendWidth), _waterWarpOffsetA, _waterWarpOffsetB);
        }

        // 마스크 값이나 문턱을 흔드는 대신, 마스크를 계산하기 전에 좌표 자체를 노이즈로
        // 뒤튼다(Domain Warping) — distance 기반 마스크 전체가 출렁이면서 자연스럽게 서로
        // 깊숙이 파고드는 얼룩이 생긴다. jitterAmount는 이 뒤틀림의 세기(월드 단위)다.
        static Vector2 WarpDomain(Vector2 worldPos, float jitterAmount, float warpScale, Vector2 offsetA, Vector2 offsetB)
        {
            if (jitterAmount <= 0f)
            {
                return worldPos;
            }

            float nx = worldPos.x / warpScale;
            float nz = worldPos.y / warpScale;

            float offsetX = Mathf.PerlinNoise(nx + offsetA.x, nz + offsetA.y) * 2f - 1f;
            float offsetZ = Mathf.PerlinNoise(nx + offsetB.x, nz + offsetB.y) * 2f - 1f;
            return worldPos + new Vector2(offsetX, offsetZ) * jitterAmount;
        }

        // t01(0=호안, 1=falloff 거리 이상 안쪽)을 sharpness가 클수록 0 근처에서부터 빠르게
        // 1로 밀어올린다 — 그 결과 falloff 거리 중 좁은 초입 구간에서만 실제로 깊어지고,
        // 나머지 대부분은 이미 마스크=1(평평한 바닥)에 도달해 있게 된다. sharpness=1이면
        // 원래의 선형 진행 그대로.
        static float SharpenMask01(float t01, float sharpness)
        {
            float exponent = 1f / Mathf.Max(1f, sharpness);
            return Mathf.Pow(Mathf.Clamp01(t01), exponent);
        }

        // seed를 노이즈 좌표에 직접 곱해서 더하면(예: seed * 0.137f) "Randomize Seed"로 들어온
        // 큰 seed(예: 1425724677) 때문에 값이 수억 단위가 된다. float32는 그 크기에서 최소
        // 간격(ULP)이 16이나 되므로, 지형 전체의 좌표 변화 폭(수십)이 반올림에 통째로 삼켜져
        // 노이즈가 사실상 상수가 되어버린다 — warp이 맵 전체를 살짝 평행이동시키는 효과만
        // 남아서, jitter를 아무리 키워도 경계가 깨끗한 직선으로 나왔다.
        // 그래서 seed에서 작은 범위의 오프셋만 뽑아 쓴다(BuildOctaveOffsets와 같은 방식).
        void RefreshWarpOffsets()
        {
            var rng = new System.Random(seed);
            _warpOffsetA = new Vector2(rng.Next(-2000, 2000), rng.Next(-2000, 2000));
            _warpOffsetB = new Vector2(rng.Next(-2000, 2000), rng.Next(-2000, 2000));
            // 같은 시드에서 이어서 뽑아 길과 다른(그러나 재현 가능한) 흔들림을 준다.
            _waterWarpOffsetA = new Vector2(rng.Next(-2000, 2000), rng.Next(-2000, 2000));
            _waterWarpOffsetB = new Vector2(rng.Next(-2000, 2000), rng.Next(-2000, 2000));
        }

        // 웨이포인트를 직선이 아니라 Catmull-Rom 스플라인으로 리샘플링해서, 꺾이는 지점이
        // 각지지 않고 부드럽게 휘어지는 경로를 만든다. 제어점(웨이포인트) 자체는 곡선이
        // 항상 그대로 지나가므로 사용자가 찍은 위치가 유지된다.
        static List<Vector2> BuildSmoothPath(Vector2[] waypoints, int samplesPerSegment)
        {
            var result = new List<Vector2>();
            if (waypoints == null || waypoints.Length < 2)
            {
                return result;
            }

            if (waypoints.Length == 2)
            {
                result.Add(waypoints[0]);
                result.Add(waypoints[1]);
                return result;
            }

            int last = waypoints.Length - 1;
            for (int i = 0; i < last; i++)
            {
                Vector2 p1 = waypoints[i];
                Vector2 p2 = waypoints[i + 1];
                Vector2 p0 = i == 0 ? p1 * 2f - p2 : waypoints[i - 1];
                Vector2 p3 = i == last - 1 ? p2 * 2f - p1 : waypoints[i + 2];

                int startJ = i == 0 ? 0 : 1;
                for (int j = startJ; j <= samplesPerSegment; j++)
                {
                    float t = (float)j / samplesPerSegment;
                    result.Add(EvaluateCatmullRom(p0, p1, p2, p3, t));
                }
            }

            return result;
        }

        // BuildSmoothPath의 닫힌 루프 버전. 호수 경계는 끝점이 없는 폐곡선이라, 열린 경로처럼
        // 양 끝을 거울像으로 확장하는 트릭이 필요 없다 — 이웃 인덱스를 그냥 순환(모듈로)시키고,
        // 마지막에 첫 점으로 되돌아오는 닫힘 세그먼트까지 결과에 포함시킨다.
        static List<Vector2> BuildSmoothClosedPath(Vector2[] waypoints, int samplesPerSegment)
        {
            var result = new List<Vector2>();
            int n = waypoints?.Length ?? 0;
            if (n < 3)
            {
                return result;
            }

            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = waypoints[(i - 1 + n) % n];
                Vector2 p1 = waypoints[i];
                Vector2 p2 = waypoints[(i + 1) % n];
                Vector2 p3 = waypoints[(i + 2) % n];

                for (int j = 0; j < samplesPerSegment; j++)
                {
                    float t = (float)j / samplesPerSegment;
                    result.Add(EvaluateCatmullRom(p0, p1, p2, p3, t));
                }
            }

            result.Add(result[0]); // 닫힘 세그먼트: 마지막 점 -> 첫 점
            return result;
        }

        static Vector2 EvaluateCatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        // TryGetBilinearCorners는 이미 만들어진 _mesh 기준이라 Generate() 도중(메시가 아직
        // 없는 시점)에는 못 쓴다 — heights 배열에 바로 양선형 보간하는 버전을 따로 둔다.
        float SampleHeightsBilinear(float[] heights, float gx, float gz)
        {
            gx = Mathf.Clamp(gx, 0f, width - 1);
            gz = Mathf.Clamp(gz, 0f, length - 1);

            int x0 = Mathf.FloorToInt(gx);
            int z0 = Mathf.FloorToInt(gz);
            int x1 = Mathf.Min(x0 + 1, width - 1);
            int z1 = Mathf.Min(z0 + 1, length - 1);
            float tx = gx - x0;
            float tz = gz - z0;

            float h00 = heights[z0 * width + x0];
            float h10 = heights[z0 * width + x1];
            float h01 = heights[z1 * width + x0];
            float h11 = heights[z1 * width + x1];
            return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);
        }

        void OnDrawGizmosSelected()
        {
            if (paths == null)
            {
                return;
            }

            foreach (var path in paths)
            {
                if (path?.waypoints == null || path.waypoints.Length == 0)
                {
                    continue;
                }

                // 제어점(웨이포인트)은 구로 표시하고, 실제 지형에 반영되는 곡선은
                // BuildSmoothPath로 같은 결과를 그려서 프리뷰와 실제 결과가 항상 일치하게 한다.
                Gizmos.color = Color.yellow;
                foreach (var waypoint in path.waypoints)
                {
                    Gizmos.DrawSphere(GetWorldPositionAt(waypoint.x, waypoint.y), Mathf.Max(0.5f, pathWidth * 0.3f));
                }

                Gizmos.color = Color.cyan;
                var curve = BuildSmoothPath(path.waypoints, pathCurveSamplesPerSegment);
                for (int i = 0; i < curve.Count - 1; i++)
                {
                    Vector3 a = GetWorldPositionAt(curve[i].x, curve[i].y);
                    Vector3 b = GetWorldPositionAt(curve[i + 1].x, curve[i + 1].y);
                    Gizmos.DrawLine(a, b);
                }
            }

            if (waterAreas == null)
            {
                return;
            }

            foreach (var area in waterAreas)
            {
                if (area?.waypoints == null || area.waypoints.Length == 0)
                {
                    continue;
                }

                // 길과 같은 원칙: 실제로 카빙/마스크에 쓰이는 것과 똑같은
                // BuildSmoothClosedPath 결과(닫힘 세그먼트 포함)를 그대로 그린다.
                Gizmos.color = Color.blue;
                foreach (var waypoint in area.waypoints)
                {
                    Gizmos.DrawSphere(GetWorldPositionAt(waypoint.x, waypoint.y), Mathf.Max(0.5f, waterShoreBlendWidth * 0.15f));
                }

                if (area.waypoints.Length < 3)
                {
                    continue;
                }

                Gizmos.color = Color.cyan;
                var curve = BuildSmoothClosedPath(area.waypoints, waterCurveSamplesPerSegment);
                for (int i = 0; i < curve.Count - 1; i++)
                {
                    Vector3 a = GetWorldPositionAt(curve[i].x, curve[i].y);
                    Vector3 b = GetWorldPositionAt(curve[i + 1].x, curve[i + 1].y);
                    Gizmos.DrawLine(a, b);
                }
            }
        }

        Vector2[] BuildOctaveOffsets()
        {
            var rng = new System.Random(seed);
            var offsets = new Vector2[Mathf.Max(1, octaves)];
            for (int i = 0; i < offsets.Length; i++)
            {
                offsets[i] = new Vector2(rng.Next(-100000, 100000), rng.Next(-100000, 100000));
            }
            return offsets;
        }

        float SampleHeight(int x, int z, Vector2[] octaveOffsets)
        {
            float amplitude = 1f;
            float frequency = 1f;
            float noiseSum = 0f;
            float amplitudeSum = 0f;
            float safeScale = Mathf.Max(0.0001f, noiseScale);

            for (int o = 0; o < octaveOffsets.Length; o++)
            {
                // 오프셋에 noiseOffsetScale을 곱하는 이유는 TerrainGenerationSettings 쪽 주석 참고 —
                // 해상도를 바꿔도 같은 지형이 나오게 하는 보정값이고, 기본값 1이면 영향이 없다.
                float offsetScale = noiseOffsetScale;
                float sampleX = (x + octaveOffsets[o].x * offsetScale) / safeScale * frequency;
                float sampleZ = (z + octaveOffsets[o].y * offsetScale) / safeScale * frequency;
                float perlin = Mathf.PerlinNoise(sampleX, sampleZ) * 2f - 1f;

                noiseSum += perlin * amplitude;
                amplitudeSum += amplitude;

                amplitude *= persistence;
                frequency *= lacunarity;
            }

            float normalized = amplitudeSum > 0f ? noiseSum / amplitudeSum : 0f;
            return normalized * heightMultiplier;
        }
    }
}
