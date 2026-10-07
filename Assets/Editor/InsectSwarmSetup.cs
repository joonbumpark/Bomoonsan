using System.Collections.Generic;
using Mountains.Env;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mountains
{
    // 식생 옆에서 날아다니는 곤충 떼(나비/파리) 프리팹을 만든다. 곤충 에셋이 없어서 날개
    // 메시는 코드로 만들고(InsectWingMesh), 움직임은 전부 ParticleSystem 모듈에 맡긴다 —
    // 보이드처럼 정교하게 무리 짓진 않지만 장식용으로는 이쪽이 가볍고 손이 덜 간다.
    // 종류마다 다른 건 메시와 수치뿐이라 SwarmPreset 하나로 묶고, 머티리얼은 공유한다
    // (색은 파티클 시작 색이 정한다).
    //
    // 만들어진 프리팹은 식생 군락 옆에 손으로 몇 개 배치해서 쓴다. 개체 하나가 파티클
    // 여러 개를 뿜는 발생기라 VegetationScatter로 대량 배치하기엔 맞지 않는다.
    static class InsectSwarmSetup
    {
        // 나비 경로는 InsectSwarmDiagnostics도 쓴다.
        internal const string PrefabPath = "Assets/Prefabs/InsectSwarm.prefab";
        internal const string MeshPath = "Assets/Prefabs/InsectWing.asset";
        const string FlyPrefabPath = "Assets/Prefabs/FlySwarm.prefab";
        const string FlyMeshPath = "Assets/Prefabs/InsectFly.asset";
        const string MaterialPath = "Assets/Materials/InsectWing.mat";

        // URP가 직접 설정해 둔 기본 파티클 머티리얼. 반투명 키워드/렌더 큐/태그가 이미
        // 맞춰져 있어서 _Surface/_Blend 같은 값을 스크립트로 추측해 넣을 필요가 없다.
        const string StockParticleMaterialPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

        // 종류별로 다른 값만 모은 것. 나머지 모듈 구성(궤도 회전 + 노이즈 + 페이드 등)은
        // ConfigureParticleSystem에서 공통으로 한다.
        sealed class SwarmPreset
        {
            public string PrefabPath;
            public string MeshPath;
            public System.Func<Mesh> BuildMesh;

            public Vector2 Lifetime;
            // 메시가 약 1유닛 폭이라 이 값이 거의 그대로 월드 크기다.
            public Vector2 Size;
            public Color ColorA;
            public Color ColorB;
            public int MaxParticles;
            public float EmissionRate;
            public float SpawnRadius;
            // 궤도 회전 속도(Y축, ±). 클수록 한 지점 주위를 빠르게 맴돈다.
            public float OrbitalSpeed;
            public float NoiseStrength;
            public float NoiseFrequency;
            public float NoiseScrollSpeed;
            // 수명 동안 날개가 접혔다 펴지는 횟수와, 접혔을 때의 크기 비율.
            public int FlapCount;
            public float FlapFoldedSize;
            // 축별 회전 속도(도/초, ±).
            public Vector3 RotationSpeed;
        }

        // 느긋하게 팔랑이며 넓게 맴도는 나비. 초록/갈색 위주인 식생 배경과 대비되는
        // 주황~분홍.
        static readonly SwarmPreset Butterfly = new SwarmPreset
        {
            PrefabPath = PrefabPath,
            MeshPath = MeshPath,
            BuildMesh = InsectWingMesh.BuildButterfly,
            Lifetime = new Vector2(2.5f, 4.5f),
            Size = new Vector2(0.35f, 0.55f),
            ColorA = new Color(1f, 0.75f, 0.1f),
            ColorB = new Color(1f, 0.3f, 0.55f),
            MaxParticles = 24,
            EmissionRate = 4f,
            SpawnRadius = 1.5f,
            OrbitalSpeed = 0.4f,
            NoiseStrength = 0.5f,
            NoiseFrequency = 0.4f,
            NoiseScrollSpeed = 0.3f,
            FlapCount = 6,
            FlapFoldedSize = 0.55f,
            RotationSpeed = new Vector3(40f, 40f, 90f),
        };

        // 작고 어두운 점이 좁은 범위 안에서 빠르고 불규칙하게 윙윙 도는 파리. 메시에 날개가
        // 없으니 날갯짓(크기 오르내림)도 끈다(FlapCount 0 = 크기 고정).
        static readonly SwarmPreset Fly = new SwarmPreset
        {
            PrefabPath = FlyPrefabPath,
            MeshPath = FlyMeshPath,
            BuildMesh = InsectWingMesh.BuildFly,
            Lifetime = new Vector2(3f, 6f),
            Size = new Vector2(0.12f, 0.18f),
            ColorA = new Color(0.05f, 0.05f, 0.05f),
            ColorB = new Color(0.2f, 0.17f, 0.1f),
            MaxParticles = 30,
            EmissionRate = 6f,
            SpawnRadius = 0.8f,
            OrbitalSpeed = 2.5f,
            NoiseStrength = 1.2f,
            NoiseFrequency = 1.5f,
            NoiseScrollSpeed = 1.5f,
            FlapCount = 0,
            FlapFoldedSize = 1f,
            RotationSpeed = new Vector3(180f, 180f, 360f),
        };

        [MenuItem("Mountains/나비 떼 프리팹 생성")]
        static void CreateButterflyPrefab()
        {
            CreatePrefab(Butterfly);
        }

        [MenuItem("Mountains/파리 떼 프리팹 생성")]
        static void CreateFlyPrefab()
        {
            CreatePrefab(Fly);
        }

        static void CreatePrefab(SwarmPreset preset)
        {
            var mesh = SaveMeshAsset(preset.BuildMesh(), preset.MeshPath);
            var material = CreateOrUpdateMaterial();

            var go = new GameObject(System.IO.Path.GetFileNameWithoutExtension(preset.PrefabPath));
            var ps = go.AddComponent<ParticleSystem>();
            ConfigureParticleSystem(ps, preset);
            ConfigureRenderer(go.GetComponent<ParticleSystemRenderer>(), mesh, material);

            bool alreadyExists = AssetDatabase.LoadAssetAtPath<GameObject>(preset.PrefabPath) != null;
            var saved = PrefabUtility.SaveAsPrefabAsset(go, preset.PrefabPath);
            Object.DestroyImmediate(go);

            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(saved);
            Debug.Log(alreadyExists
                ? $"[InsectSwarmSetup] 기존 프리팹을 갱신했습니다: {preset.PrefabPath}"
                : $"[InsectSwarmSetup] 프리팹을 만들었습니다: {preset.PrefabPath} — 식생 군락 옆에 갖다 놓고 쓰세요.");
        }

        static void ConfigureRenderer(ParticleSystemRenderer renderer, Mesh mesh, Material material)
        {
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = mesh;
            renderer.sharedMaterial = material;
            // View(빌보드)면 항상 카메라를 보는 평면이 돼서 날개 모양이 안 보인다. World여야
            // Rotation over Lifetime에 따라 메시가 실제로 돌아가며 펄럭인다.
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            // URP 파티클 셰이더가 요구하는 정점 스트림과 정확히 맞춘다(URP ParticleGUI의
            // 규칙 그대로: 언릿 + Mesh 모드 + GPU 인스턴싱이면 Position/Color/UV/AnimFrame).
            // 기본 스트림(Position/Normal/Color/UV)으로 두면 머티리얼 인스펙터에
            // "incorrect Vertex Streams" 에러가 뜬다.
            renderer.enableGPUInstancing = true;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.AnimFrame
            });
        }

        // 디스크에 실제로 저장된(에셋 GUID를 가진) Mesh를 돌려준다. 렌더러에는 반드시
        // 이 반환값을 물려야 한다 — 기존 에셋이 있을 때 넘겨받은 mesh를 그대로 쓰면
        // 에셋이 아닌 임시 객체라 프리팹 저장 시 참조가 비어 곤충이 안 보인다.
        static Mesh SaveMeshAsset(Mesh mesh, string meshPath)
        {
            EnsureFolder("Assets/Prefabs");

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, meshPath);
                return mesh;
            }

            // 에셋은 그대로 두고 내용만 갱신한다 — 프리팹의 메시 참조가 끊기지 않게.
            existing.Clear();
            existing.SetVertices(new List<Vector3>(mesh.vertices));
            existing.SetTriangles(mesh.triangles, 0);
            existing.SetColors(mesh.colors);
            existing.RecalculateNormals();
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        // URP 기본 파티클 머티리얼을 복사해서 쓰고, 날개에 맞게 두 가지만 바꾼다.
        static Material CreateOrUpdateMaterial()
        {
            var stock = AssetDatabase.LoadAssetAtPath<Material>(StockParticleMaterialPath);
            if (stock == null)
            {
                Debug.LogWarning($"[InsectSwarmSetup] URP 기본 파티클 머티리얼을 찾지 못했습니다: {StockParticleMaterialPath}");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                EnsureFolder("Assets/Materials");
                material = new Material(stock) { name = "InsectWing" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                // 에셋을 새로 만들지 않고 내용만 덮어써서 GUID를 유지한다 — 이미 이
                // 머티리얼을 참조하는 오브젝트의 연결이 끊기지 않게.
                material.shader = stock.shader;
                material.CopyPropertiesFromMaterial(stock);
            }

            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = stock.renderQueue;
            material.SetColor("_BaseColor", Color.white);
            // 양면(_Cull 0)이면 URP 머티리얼 인스펙터가 이 값을 켜서 저장한다 — 미리 맞춰둬야
            // 인스펙터를 열 때마다 변경 사항이 생기지 않는다(파티클은 라이트맵과 무관).
            material.doubleSidedGI = true;

            // 날개가 두께 없는 평면이라 양면을 그린다. _Cull은 셰이더의 Cull[_Cull]을
            // 직접 모는 값이라 키워드 없이 바꿔도 된다.
            material.SetFloat("_Cull", 0f);

            // 기본 머티리얼의 _BaseMap은 가장자리 알파가 0인 원형 텍스처(Default-Particle)다.
            // 날개 메시엔 UV가 없어서 전부 UV (0,0) = 투명한 모서리를 읽으므로, 텍스처를
            // 비워 셰이더 기본값(흰색)을 쓰게 한다.
            material.SetTexture("_BaseMap", null);
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", null);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        static void ConfigureParticleSystem(ParticleSystem ps, SwarmPreset preset)
        {
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(preset.Lifetime.x, preset.Lifetime.y);
            main.startSpeed = 0f; // 이동은 Velocity over Lifetime/Noise가 전부 맡는다.
            main.startSize = new ParticleSystem.MinMaxCurve(preset.Size.x, preset.Size.y);
            main.startRotation3D = true;
            main.startColor = new ParticleSystem.MinMaxGradient(preset.ColorA, preset.ColorB);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = preset.MaxParticles;

            var emission = ps.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(preset.EmissionRate);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = preset.SpawnRadius;

            // 궤도 회전으로 꽃/덤불 주위를 맴돌게 한다. orbitalX/Y/Z와 orbitalOffsetX/Y/Z는
            // 모두 같은 모드의 MinMaxCurve여야 해서(다르면 "Orbital Velocity curves must
            // all be in the same mode" 에러) 여섯 개 전부 TwoConstants로 명시한다.
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalY = SymmetricRange(preset.OrbitalSpeed);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalOffsetX = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalOffsetY = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalOffsetZ = new ParticleSystem.MinMaxCurve(0f, 0f);

            // 궤도 회전만 있으면 너무 규칙적인 원운동이라 노이즈로 흔든다.
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(preset.NoiseStrength);
            noise.frequency = preset.NoiseFrequency;
            noise.scrollSpeed = new ParticleSystem.MinMaxCurve(preset.NoiseScrollSpeed);
            noise.damping = true;

            // 크기를 주기적으로 오르내려 날개가 접혔다 펴지는 것처럼 보이게 한다. 날개가 없는
            // 종류(FlapCount 0)는 모듈을 꺼서 크기를 고정한다.
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = preset.FlapCount > 0;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                BuildFlapCurve(preset.FlapCount, preset.FlapFoldedSize));

            var rotationOverLifetime = ps.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.x = SymmetricRange(preset.RotationSpeed.x);
            rotationOverLifetime.y = SymmetricRange(preset.RotationSpeed.y);
            rotationOverLifetime.z = SymmetricRange(preset.RotationSpeed.z);

            // 갑자기 생기고 사라지지 않도록 앞뒤로 페이드한다.
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        // 수명(0~1) 동안 몇 차례 오르내리는 곡선. AddKey 직후엔 접선이 날카로워서
        // SmoothTangents로 다듬어 사인파에 가깝게 만든다.
        static AnimationCurve BuildFlapCurve(int flapCount, float foldedSize)
        {
            var curve = new AnimationCurve();
            for (int i = 0; i <= flapCount * 2; i++)
            {
                float t = i / (float)(flapCount * 2);
                curve.AddKey(new Keyframe(t, i % 2 == 0 ? 1f : foldedSize));
            }
            for (int i = 0; i < curve.length; i++)
            {
                curve.SmoothTangents(i, 0f);
            }
            return curve;
        }

        // -값~+값 사이에서 개체마다 무작위로 뽑는 범위.
        static ParticleSystem.MinMaxCurve SymmetricRange(float magnitude)
        {
            return new ParticleSystem.MinMaxCurve(-magnitude, magnitude);
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            int slash = folder.LastIndexOf('/');
            AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
        }
    }
}
