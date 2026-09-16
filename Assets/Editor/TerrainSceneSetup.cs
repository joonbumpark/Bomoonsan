using Cinemachine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Mountains
{
    public static class TerrainSceneSetup
    {
        const string SkyboxMaterialPath = "Assets/Materials/MountainSkybox.mat";
        const string PolytopeSkyboxMaterialPath = "Assets/Polytope Studio/Lowpoly_Environments/Sources/Materials/PT_Skybox_mat.mat";
        const string SunFlareDataPath = "Assets/Settings/SunLensFlare.asset";

        [MenuItem("Mountains/Create Procedural Terrain")]
        public static void CreateProceduralTerrain()
        {
            var terrain = Object.FindObjectOfType<ProceduralTerrainMesh>();
            var go = terrain != null ? terrain.gameObject : new GameObject("ProceduralTerrain");
            if (terrain == null)
            {
                terrain = go.AddComponent<ProceduralTerrainMesh>();
            }

            var terrainRenderer = go.GetComponent<MeshRenderer>();
            var material = GetOrCreateMaterial(terrainRenderer, terrain.AssetBaseName);
            if (material != null)
            {
                terrainRenderer.sharedMaterial = material;
            }
            terrainRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            terrainRenderer.receiveShadows = true;

            EnsureSettings(terrain);
            EditorUtility.SetDirty(terrain);

            terrain.Generate();

            if (material != null)
            {
                ApplyHeightRangeToMaterial(material, terrain);
            }

            float sizeX = (terrain.width - 1) * terrain.cellSize;
            float sizeZ = (terrain.length - 1) * terrain.cellSize;
            var center = new Vector3(sizeX * 0.5f, 0f, sizeZ * 0.5f);
            float maxExtent = Mathf.Max(sizeX, sizeZ);

            SetupSkybox();
            PositionLight(center, maxExtent);
            var player = SpawnPlayer(terrain);
            player.tag = "Player";
            SetupPlayerCamera(player);
            SetupMobileControls(player);
            ConfigureCameraClipping(maxExtent);

            SetupVegetation(terrain, go);
            SetupTreeOcclusionFader(player, go);

            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        // 씬 파일을 그냥 복사(Save As, Ctrl+D)하면 GameObject는 다 따라오지만, 지형이
        // 참조하는 설정/텍스처/머티리얼 에셋은 복제되지 않고 원본 씬과 같은 파일을 계속
        // 가리킨다 — 그래서 한쪽 씬에서 지형을 튜닝하면 안 건드린 다른 씬 지형까지 같이
        // 바뀐다. 이 메뉴는 씬을 복제한 뒤 그 에셋들까지 자동으로 독립시켜서 두 문제를
        // 한 번에 해결한다.
        [MenuItem("Mountains/Duplicate Terrain Scene")]
        public static void DuplicateTerrainScene()
        {
            var currentScene = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(currentScene.path))
            {
                Debug.LogWarning("[TerrainSceneSetup] 현재 씬이 아직 저장되지 않아 복제할 원본 파일이 없습니다. " +
                    "먼저 씬을 저장한 뒤 다시 실행하세요.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return; // 사용자가 취소함
            }

            string newPath = AssetDatabase.GenerateUniqueAssetPath(currentScene.path);
            if (!AssetDatabase.CopyAsset(currentScene.path, newPath))
            {
                Debug.LogWarning($"[TerrainSceneSetup] 씬 복제에 실패했습니다: {currentScene.path} -> {newPath}");
                return;
            }
            AssetDatabase.Refresh();

            var newScene = EditorSceneManager.OpenScene(newPath, OpenSceneMode.Single);
            MakeTerrainAssetsIndependent(newScene.name);
            EditorSceneManager.SaveScene(newScene);

            Debug.Log($"[TerrainSceneSetup] 씬을 복제하고 지형 에셋을 독립시켰습니다: {newPath}");
        }

        // 복제된 씬의 지형이 아직 원본 씬과 공유하고 있는 에셋들을 이 씬 전용으로 갈아끼운다.
        // 두 종류를 다르게 다룬다: 사용자가 Inspector에서 튜닝한 값이 담긴 에셋은 복제해서
        // 값을 그대로 보존하고, 순수 파생 데이터(높이 그라디언트/마스크 텍스처)는 참조만
        // 비워두면 Generate()가 이 씬 이름으로 알아서 다시 구워준다.
        static void MakeTerrainAssetsIndependent(string baseName)
        {
            var terrain = Object.FindObjectOfType<ProceduralTerrainMesh>();
            if (terrain == null)
            {
                Debug.LogWarning("[TerrainSceneSetup] 복제된 씬에서 ProceduralTerrainMesh를 찾을 수 없어 에셋 독립화를 건너뜁니다.");
                return;
            }

            EnsureFolder("Materials");
            EnsureFolder("Settings");

            if (terrain.settings != null)
            {
                terrain.settings = DuplicateAsset(terrain.settings, $"Assets/Settings/{baseName}_TerrainSettings.asset");
            }
            if (terrain.waterMaterial != null)
            {
                terrain.waterMaterial = DuplicateAsset(terrain.waterMaterial, $"Assets/Materials/{baseName}_WaterSurface.mat");
            }

            var terrainRenderer = terrain.GetComponent<MeshRenderer>();
            if (terrainRenderer != null && terrainRenderer.sharedMaterial != null)
            {
                terrainRenderer.sharedMaterial = DuplicateAsset(terrainRenderer.sharedMaterial, $"Assets/Materials/{baseName}_TerrainMountain.mat");
                EditorUtility.SetDirty(terrainRenderer);
            }

            var vegetation = terrain.GetComponent<VegetationScatter>();
            if (vegetation != null && vegetation.settings != null)
            {
                vegetation.settings = DuplicateAsset(vegetation.settings, $"Assets/Settings/{baseName}_VegetationSettings.asset");
                EditorUtility.SetDirty(vegetation);
            }

            terrain.gradientTexture = null;
            terrain.pathMaskTexture = null;
            terrain.waterMaskTextures = new Texture2D[0];
            EditorUtility.SetDirty(terrain);

            terrain.Generate();

            if (terrainRenderer != null && terrainRenderer.sharedMaterial != null)
            {
                ApplyHeightRangeToMaterial(terrainRenderer.sharedMaterial, terrain);
            }
            if (vegetation != null)
            {
                vegetation.Scatter();
            }

            AssetDatabase.SaveAssets();
        }

        static T DuplicateAsset<T>(T source, string desiredPath) where T : Object
        {
            string path = AssetDatabase.GenerateUniqueAssetPath(desiredPath);
            var copy = Object.Instantiate(source);
            copy.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }

        // 이미 설정 에셋이 할당돼 있으면 절대 건드리지 않는다 — 이게 핵심이다. 예전에는
        // 이 자리에서 매번 필드를 하드코딩된 값으로 강제로 덮어써서, 재생성할 때마다
        // Inspector에서 튜닝한 값이 날아갔다. 씬 하나에 지형을 여러 개 두고 관리할 수도
        // 있으므로, 고정된 파일명 하나가 아니라 오브젝트 이름 기반으로 겹치지 않는 경로에
        // 새 에셋을 만든다.
        static void EnsureSettings(ProceduralTerrainMesh terrain)
        {
            if (terrain.settings != null)
            {
                return;
            }

            EnsureFolder("Settings");

            string desiredPath = $"Assets/Settings/{terrain.AssetBaseName}_TerrainSettings.asset";
            string path = AssetDatabase.GenerateUniqueAssetPath(desiredPath);

            var settings = ScriptableObject.CreateInstance<TerrainGenerationSettings>();
            settings.width = 100;
            settings.length = 100;
            settings.cellSize = 10f;
            settings.heightMultiplier = 60f;
            settings.noiseScale = 28f;
            settings.octaves = 3;
            settings.persistence = 0.45f;
            settings.smoothingIterations = 2;

            AssetDatabase.CreateAsset(settings, path);
            terrain.settings = settings;
        }

        static GameObject SpawnPlayer(ProceduralTerrainMesh terrain)
        {
            var movement = Object.FindObjectOfType<CharacterMovement>();
            var player = movement != null ? movement.gameObject : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";

            var capsuleCollider = player.GetComponent<CapsuleCollider>();
            if (capsuleCollider != null)
            {
                Object.DestroyImmediate(capsuleCollider);
            }

            var controller = player.GetComponent<CharacterController>();
            if (controller == null)
            {
                controller = player.AddComponent<CharacterController>();
            }

            if (movement == null)
            {
                movement = player.AddComponent<CharacterMovement>();
            }

            if (Camera.main != null)
            {
                movement.cameraTransform = Camera.main.transform;
            }

            // 호수 폴리곤 판정으로 물 진입을 막으려면 지형 참조가 필요하다.
            movement.terrain = terrain;

            Vector3 surfacePoint = terrain.GetVertexWorldPosition(terrain.width / 2, terrain.length / 2);
            float groundOffset = controller.height * 0.5f - controller.center.y + controller.skinWidth;

            // CharacterController가 붙은 채로 transform.position을 직접 바꾸면(순간이동),
            // 컨트롤러 내부 상태가 새 위치를 즉시 인식하지 못해 isGrounded가 한동안 false로
            // 나온다 — 특히 재생 중에 지형을 다시 만들면 그 사이 MeshCollider도 리베이크되는
            // 타이밍이라 겹쳐서 플레이어가 훅 떨어지는 것처럼 보인다. enabled를 껐다 켜서
            // 컨트롤러가 새 위치 기준으로 즉시 다시 인식하게 하고, 누적된 낙하 속도도 지운다.
            controller.enabled = false;
            player.transform.position = surfacePoint + Vector3.up * groundOffset;
            controller.enabled = true;

            if (movement != null)
            {
                movement.ResetVelocity();
            }

            EditorUtility.SetDirty(player);
            return player;
        }

        static void SetupPlayerCamera(GameObject player)
        {
            var cam = Camera.main;
            if (cam == null) return;

            // 예전 Cinemachine 리그(CM TaggedGroupCamera)가 씬에 남아있으면 정리한다.
            var leftoverRig = GameObject.Find("CM TaggedGroupCamera");
            if (leftoverRig != null)
            {
                Object.DestroyImmediate(leftoverRig);
            }

            // Cinemachine 패키지를 제거해서 Main Camera에 붙어있던 CinemachineBrain은
            // Missing Script가 됐다 — 타입 자체가 없어져서 더 이상 이름으로는 못 지우니
            // Unity의 범용 정리 API로 없앤다.
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(cam.gameObject);

            var playerCamera = cam.GetComponent<PlayerCamera>();
            if (playerCamera == null)
            {
                playerCamera = cam.gameObject.AddComponent<PlayerCamera>();
            }

            playerCamera.player = player.transform;
            playerCamera.playerMovement = player.GetComponent<CharacterMovement>();
            playerCamera.SetMode(PlayerCamera.Mode.FollowPlayer);

            // 평상시 카메라는 여전히 PlayerCamera가 직접 몬다. CinemachineBrain은 여기서
            // 되살리지만 씬에 활성화된 Virtual Camera가 없는 한 아무 것도 하지 않는다 —
            // DialogTrigger가 이벤트 재생 중에만 전용 vcam을 잠깐 켜서 카메라를 가로챈다.
            if (cam.GetComponent<CinemachineBrain>() == null)
            {
                cam.gameObject.AddComponent<CinemachineBrain>();
            }

            EditorUtility.SetDirty(cam.gameObject);
        }

        // 씬에 미리 배치해둔 Canvas/Fixed Joystick(Joystick Pack)을 찾아서 이동·회전
        // 스크립트에 연결한다. Canvas나 조이스틱이 없으면(모바일 컨트롤을 아직 안 붙인
        // 씬이면) 경고만 남기고 나머지 설정은 그대로 진행한다 — LoadAssetPrefab과 같은
        // "없으면 경고, 계속 진행" 패턴.
        static void SetupMobileControls(GameObject player)
        {
            var canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[TerrainSceneSetup] Canvas를 찾을 수 없어 모바일 컨트롤 연결을 건너뜁니다.");
                return;
            }

            var movement = player.GetComponent<CharacterMovement>();

            var joystickObject = GameObject.Find("Fixed Joystick");
            if (joystickObject != null)
            {
                // 엉뚱한 부모(예: 식생 컨테이너) 밑에 있으면 Canvas 밑으로 옮긴다 — UI라
                // false로 넘겨서 로컬 anchoredPosition을 그대로 유지한다.
                if (!joystickObject.transform.IsChildOf(canvas.transform))
                {
                    joystickObject.transform.SetParent(canvas.transform, false);
                }

                var joystick = joystickObject.GetComponent<Joystick>();
                if (joystick != null && movement != null)
                {
                    movement.moveJoystick = joystick;
                }
                EditorUtility.SetDirty(joystickObject);
            }
            else
            {
                Debug.LogWarning("[TerrainSceneSetup] 'Fixed Joystick'을 찾을 수 없어 조이스틱 이동 연결을 건너뜁니다.");
            }

            var dragCatcher = FindOrCreateDragCatcher(canvas);
            var touchRotate = dragCatcher.GetComponent<TouchRotateInput>();
            touchRotate.player = player.transform;

            EditorUtility.SetDirty(canvas.gameObject);
        }

        // 화면 전체를 덮는 투명 UI Image + TouchRotateInput. 조이스틱보다 하이어라키
        // 앞쪽(= 레이캐스트 우선순위상 뒤쪽)에 둬서, 조이스틱 영역의 드래그는 조이스틱이
        // 먼저 가로채고 나머지 화면만 회전 드래그로 잡히게 한다.
        static GameObject FindOrCreateDragCatcher(Canvas canvas)
        {
            var existing = canvas.transform.Find("RotateDragCatcher");
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
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

        // 대화 UI만 따로 배선하는 메뉴. "Create Procedural Terrain"은 지형을 통째로
        // 다시 굽고 플레이어를 재배치하는 등 기존 씬을 건드리는 부작용이 커서, 대화
        // UI 하나만 추가/갱신하고 싶을 때 쓰기엔 부담스럽다 — 그래서 별도 메뉴로 분리했다.
        [MenuItem("Mountains/Setup Dialog UI")]
        public static void SetupDialogUI()
        {
            EnsureDialogUI();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        // 조이스틱/회전 캐처와 같은 "이름으로 찾고 없으면 만든다" 패턴. 이미 있으면
        // Inspector에서 튜닝한 값(charsPerSecond 등)을 그대로 두고 아무것도 건드리지 않는다.
        static void EnsureDialogUI()
        {
            var canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[TerrainSceneSetup] Canvas를 찾을 수 없어 대화창 설정을 건너뜁니다.");
                return;
            }

            if (canvas.transform.Find("DialogUI") != null)
            {
                return;
            }

            var root = new GameObject("DialogUI", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(DialogUI));
            root.transform.SetParent(canvas.transform, false);
            // 대화 중엔 조이스틱/회전 드래그 캐처보다 위에서 탭을 가로채야 하므로 맨 뒤
            // (= 레이캐스트 우선순위상 맨 앞)에 둔다.
            root.transform.SetAsLastSibling();

            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var rootImage = root.GetComponent<Image>();
            rootImage.color = new Color(0f, 0f, 0f, 0f);
            rootImage.raycastTarget = true; // 화면 전체 탭 감지용 — 실제로 보이지는 않는다.

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0.28f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.75f);
            panelImage.raycastTarget = false; // 탭 감지는 루트 Image가 전담한다.

            var portrait = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portrait.transform.SetParent(panel.transform, false);
            var portraitRect = portrait.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0f, 0f);
            portraitRect.anchorMax = new Vector2(0f, 1f);
            portraitRect.pivot = new Vector2(0f, 0.5f);
            portraitRect.sizeDelta = new Vector2(220f, -40f);
            portraitRect.anchoredPosition = new Vector2(20f, 0f);
            var portraitImage = portrait.GetComponent<Image>();
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;

            var nameText = CreateDialogText(panel.transform, "NameText", 28f, FontStyles.Bold);
            var nameRect = nameText.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.sizeDelta = new Vector2(-280f, 60f);
            nameRect.anchoredPosition = new Vector2(120f, -20f);

            var bodyText = CreateDialogText(panel.transform, "BodyText", 32f, FontStyles.Normal);
            var bodyRect = bodyText.GetComponent<RectTransform>();
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = new Vector2(260f, 20f);
            bodyRect.offsetMax = new Vector2(-20f, -90f);

            var dialogUi = root.GetComponent<DialogUI>();
            dialogUi.canvasGroup = root.GetComponent<CanvasGroup>();
            dialogUi.portraitImage = portraitImage;
            dialogUi.nameText = nameText;
            dialogUi.bodyText = bodyText;

            var canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            EditorUtility.SetDirty(root);
        }

        static TextMeshProUGUI CreateDialogText(Transform parent, string name, float fontSize, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        // 나무 프리팹엔 Collider가 없어 레이캐스트로 카메라-플레이어 사이를 가리는지
        // 감지할 수 없다 — 위치 기반으로 판정해서 가려지면 반투명하게 페이드시키는
        // TreeViewOcclusionFader를 Main Camera에 붙인다. VegetationScatter가 만든
        // Tree 인스턴스 목록을 참조해야 하므로 SetupVegetation 이후에 호출해야 한다.
        static void SetupTreeOcclusionFader(GameObject player, GameObject terrainObject)
        {
            var cam = Camera.main;
            if (cam == null) return;

            var fader = cam.GetComponent<TreeViewOcclusionFader>();
            if (fader == null)
            {
                fader = cam.gameObject.AddComponent<TreeViewOcclusionFader>();
            }

            var scatter = terrainObject.GetComponent<VegetationScatter>();
            fader.player = player.transform;
            fader.vegetationScatter = scatter;

            fader.CollectTrees();

            EditorUtility.SetDirty(cam.gameObject);
        }

        // 기본 Far Clip Plane(1000)은 예전 작은 씬 기준값이라, 지금처럼 넓은 지형(대각선
        // 1000m 이상)에서는 플레이어가 구석에 있을 때 반대편 지형이 클리핑되어 사라져
        // 보일 수 있다. 지형 대각선 길이에 맞춰 여유 있게 늘려준다.
        static void ConfigureCameraClipping(float maxExtent)
        {
            var cam = Camera.main;
            if (cam == null) return;

            float diagonal = maxExtent * Mathf.Sqrt(2f);
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, diagonal * 1.2f);
            cam.nearClipPlane = Mathf.Min(cam.nearClipPlane, 0.3f);

            EditorUtility.SetDirty(cam);
        }

        // Polytope Studio 데모 팩에 들어있는 PT_Skybox_mat(Skybox/Cubemap)을 우선 쓴다 —
        // Skybox 계열 셰이더(Cubemap/6 Sided/Procedural 등)는 라이팅 모델에 안 묶인
        // 범용 셰이더라 URP에서도 그대로 작동한다. 못 찾으면 예전에 자동 생성해두던
        // 파란 톤 Skybox/Procedural 머티리얼로 대체한다.
        static void SetupSkybox()
        {
            var skybox = AssetDatabase.LoadAssetAtPath<Material>(PolytopeSkyboxMaterialPath);
            if (skybox == null)
            {
                skybox = GetOrCreateProceduralSkybox();
            }

            if (skybox == null)
            {
                return;
            }

            RenderSettings.skybox = skybox;
            // 스카이박스를 갈아끼운 뒤에도 앰비언트 프로브가 예전(회색) 스카이박스 값을
            // 그대로 들고 있으므로, 즉시 다시 계산해서 지형/오브젝트의 환경광도 같이 갱신한다.
            DynamicGI.UpdateEnvironment();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        // 유니티 기본 내장 Default-Skybox는 _SkyTint가 중간 회색(0.5,0.5,0.5)이라 하늘이
        // 파랗지 않고 뿌옇게 회색조로 보인다. PT_Skybox_mat을 못 찾을 때만 쓰는 대체용으로,
        // 산 지형에 맞는 파란 하늘 톤의 전용 머티리얼을 만든다.
        static Material GetOrCreateProceduralSkybox()
        {
            var shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
            {
                return null;
            }

            var skybox = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMaterialPath);
            if (skybox == null)
            {
                skybox = new Material(shader) { name = "MountainSkybox" };

                EnsureFolder("Materials");

                AssetDatabase.CreateAsset(skybox, SkyboxMaterialPath);
            }

            skybox.SetColor("_SkyTint", new Color(0.35f, 0.55f, 0.85f));
            skybox.SetColor("_GroundColor", new Color(0.45f, 0.42f, 0.36f));
            skybox.SetFloat("_AtmosphereThickness", 1f);
            skybox.SetFloat("_Exposure", 1.3f);
            skybox.SetFloat("_SunSize", 0.04f);
            skybox.SetFloat("_SunSizeConvergence", 5f);
            EditorUtility.SetDirty(skybox);
            return skybox;
        }

        static void PositionLight(Vector3 center, float maxExtent)
        {
            var light = FindDirectionalLight();
            if (light != null)
            {
                light.transform.position = center + Vector3.up * maxExtent;
                EditorUtility.SetDirty(light.transform);
                SetupSunFlare(light);
            }
        }

        static Light FindDirectionalLight()
        {
            foreach (var light in Object.FindObjectsOfType<Light>())
            {
                if (light.type == LightType.Directional)
                {
                    return light;
                }
            }
            return null;
        }

        // PT_Skybox_mat(Skybox/Cubemap)은 정적 파노라마 텍스처만 그려서 태양이 안 보인다
        // (Skybox/Procedural과 달리 태양 원반을 직접 계산해주지 않음). URP의 SRP Lens Flare
        // (Data-Driven)로 디렉셔널 라이트 방향에 맞춰 태양을 따로 그려준다 — 지형에 가려지면
        // useOcclusion으로 자연스럽게 옅어진다.
        static void SetupSunFlare(Light light)
        {
            var flareData = GetOrCreateSunFlareData();

            var flare = light.GetComponent<LensFlareComponentSRP>();
            if (flare == null)
            {
                flare = light.gameObject.AddComponent<LensFlareComponentSRP>();
            }

            flare.lensFlareData = flareData;
            flare.intensity = 1f;
            flare.scale = 1f;
            flare.useOcclusion = true;
            flare.occlusionRadius = 0.3f;
            flare.allowOffScreen = false;

            EditorUtility.SetDirty(light.gameObject);
        }

        static LensFlareDataSRP GetOrCreateSunFlareData()
        {
            var existing = AssetDatabase.LoadAssetAtPath<LensFlareDataSRP>(SunFlareDataPath);
            if (existing != null)
            {
                return existing;
            }

            // 원반(core)은 1을 넘는 HDR 값으로 오버브라이트시켜 블룸 포스트프로세스가
            // 걸리게 한다 — 그래야 벡터 도형처럼 딱딱하지 않고 자연스럽게 번져 보인다.
            // 광선(아래 루프)은 원반/후광보다 작고 옅게 둬서 중심 글로우를 가리지 않는
            // 보조 요소로만 쓴다.
            var core = new LensFlareDataElementSRP
            {
                flareType = SRPLensFlareType.Circle,
                tint = new Color(2f, 1.85f, 1.4f, 1f),
                localIntensity = 1f,
                uniformScale = 0.6f,
                fallOff = 0.4f,
                edgeOffset = 0.15f,
                modulateByLightColor = true,
                blendMode = SRPLensFlareBlendMode.Additive,
            };
            var glow = new LensFlareDataElementSRP
            {
                flareType = SRPLensFlareType.Circle,
                tint = new Color(1f, 0.85f, 0.55f, 0.4f),
                localIntensity = 0.5f,
                uniformScale = 1.8f,
                fallOff = 1f,
                edgeOffset = 0.4f,
                modulateByLightColor = true,
                blendMode = SRPLensFlareBlendMode.Additive,
            };

            var elements = new System.Collections.Generic.List<LensFlareDataElementSRP> { core, glow };
            const int rayCount = 4;
            for (int i = 0; i < rayCount; i++)
            {
                float angle = i * (180f / rayCount);
                elements.Add(new LensFlareDataElementSRP
                {
                    flareType = SRPLensFlareType.Polygon,
                    sideCount = 4,
                    sdfRoundness = 0f,
                    tint = new Color(1f, 0.9f, 0.7f, 0.22f),
                    localIntensity = 0.3f,
                    uniformScale = 1.3f,
                    sizeXY = new Vector2(0.015f, 1f),
                    rotation = angle,
                    modulateByLightColor = true,
                    blendMode = SRPLensFlareBlendMode.Additive,
                });
            }

            var data = ScriptableObject.CreateInstance<LensFlareDataSRP>();
            data.elements = elements.ToArray();

            EnsureFolder("Settings");
            AssetDatabase.CreateAsset(data, SunFlareDataPath);
            return data;
        }

        // 전부 Polytope Studio의 무료 로우폴리 에셋. 나무/바위는 Lowpoly_Environments의
        // 기본 프리팹, 풀/꽃/버섯은 Environment_Free 데모 씬이 실제로 쓰던 조합(Helpers
        // 폴더)을 그대로 가져왔다 — 그 데모가 이미 검증된 배치 조합이라 참고했다.
        const string PineTreePath = "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Pine_Tree_03_green.prefab";
        const string GenericRockPath = "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Rocks/PT_Generic_Rock_01.prefab";
        const string OreRockPath = "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Rocks/PT_Ore_Rock_01.prefab";
        const string RiverRockPilePath = "Assets/Polytope Studio/Lowpoly_Demos/Environment_Free/Helpers/PT_River_Rock_Pile_02_v1.prefab";
        const string GrassPath1 = "Assets/Polytope Studio/Lowpoly_Demos/Environment_Free/Helpers/PT_Grass_02_v1.prefab";
        const string GrassPath2 = "Assets/Polytope Studio/Lowpoly_Demos/Environment_Free/Helpers/PT_Grass_02_v2.prefab";
        const string HighGrassPath = "Assets/Polytope Studio/Lowpoly_Demos/Environment_Free/Helpers/PT_High_Grass_02_v1.prefab";
        const string PoppyPath = "Assets/Polytope Studio/Lowpoly_Demos/Environment_Free/Helpers/PT_Poppy_02_v1.prefab";
        const string MushroomPath = "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Mushrooms/PT_Caesars_Mushroom_01.prefab";

        static void SetupVegetation(ProceduralTerrainMesh terrain, GameObject terrainObject)
        {
            var scatter = terrainObject.GetComponent<VegetationScatter>();
            if (scatter == null)
            {
                scatter = terrainObject.AddComponent<VegetationScatter>();
            }
            scatter.terrain = terrain;
            scatter.scatterOnStart = false;

            EnsureVegetationSettings(scatter);
            MigrateTreeGroupFlags(scatter);

            scatter.Scatter();
            EditorUtility.SetDirty(scatter);
        }

        // fadeWhenOccludingPlayer / blockPlayer는 나중에 추가된 필드라, 그 전에 만들어진 설정
        // 에셋의 그룹들은 전부 false로 역직렬화된다 — 그대로 두면 페이드도 충돌도 안 걸린다.
        // 나무 그룹에 해당하는 기본값을 대신 채워준다. Scatter()가 콜라이더를 붙이는 시점보다
        // 먼저 실행되어야 하므로 SetupVegetation 안에서 호출한다.
        static void MigrateTreeGroupFlags(VegetationScatter scatter)
        {
            MigrateTreeGroupFlag(scatter, g => g.fadeWhenOccludingPlayer, g => g.fadeWhenOccludingPlayer = true);
            MigrateTreeGroupFlag(scatter, g => g.blockPlayer, g => g.blockPlayer = true);
        }

        // 어느 그룹도 켜져 있지 않을 때만(즉 사용자가 아직 한 번도 설정한 적 없을 때만)
        // 라벨이 "Tree"로 시작하는 그룹들을 켜준다. 하나라도 켜져 있으면 사용자가 의도적으로
        // 고른 상태이므로 절대 건드리지 않는다.
        static void MigrateTreeGroupFlag(VegetationScatter scatter,
            System.Func<ScatterGroup, bool> isEnabled, System.Action<ScatterGroup> enable)
        {
            if (scatter == null || scatter.settings == null || scatter.groups == null)
            {
                return;
            }

            foreach (var group in scatter.groups)
            {
                if (group != null && isEnabled(group))
                {
                    return;
                }
            }

            bool changed = false;
            foreach (var group in scatter.groups)
            {
                if (group != null && !string.IsNullOrEmpty(group.label)
                    && group.label.StartsWith("Tree", System.StringComparison.OrdinalIgnoreCase))
                {
                    enable(group);
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(scatter.settings);
            }
        }

        // terrain settings(EnsureSettings)와 같은 패턴 — 이미 배치 규칙 에셋이 있으면 절대
        // 건드리지 않는다. 없을 때만 지금 검증된 기본 4개 그룹(Grass/Rock/Tree/Decoration)으로
        // 새 에셋을 만든다. 경로는 ProceduralTerrainMesh.AssetBaseName(씬 이름) 기준으로
        // 맞춰서, 씬을 복제했을 때 어느 씬 소유인지 파일명으로 바로 알 수 있게 한다.
        static void EnsureVegetationSettings(VegetationScatter scatter)
        {
            if (scatter.settings != null)
            {
                return;
            }

            var grassPrefabs = new[] { LoadAssetPrefab(GrassPath1), LoadAssetPrefab(GrassPath2), LoadAssetPrefab(HighGrassPath) };
            var rockPrefabs = new[] { LoadAssetPrefab(GenericRockPath), LoadAssetPrefab(OreRockPath), LoadAssetPrefab(RiverRockPilePath) };
            var treePrefab = LoadAssetPrefab(PineTreePath);
            var decorationPrefabs = new[] { LoadAssetPrefab(PoppyPath), LoadAssetPrefab(MushroomPath) };

            var settings = ScriptableObject.CreateInstance<VegetationScatterSettings>();
            settings.groups = new[]
            {
                new ScatterGroup
                {
                    label = "Grass",
                    prefabs = grassPrefabs,
                    count = 4000,
                    minHeight = 0f, maxHeight = 0.45f,
                    minSlope = 0f, maxSlope = 0.25f,
                    alignToNormal = false,
                    uniformScaleRange = new Vector2(0.8f, 1.4f),
                    castShadows = false, // 개수가 많아 그림자맵 비용이 큼 — 안 보이는 만큼 성능에 크게 도움
                },
                new ScatterGroup
                {
                    label = "Rock",
                    prefabs = rockPrefabs,
                    count = 250,
                    minHeight = 0f, maxHeight = 1f,
                    // 스무딩 패스(smoothingIterations) 도입 이후 지형 경사가 전반적으로 완만해져
                    // 0.25(약 41도) 이상인 지점이 사실상 없다 — 실제 지형이 낼 수 있는 범위로 낮춘다.
                    minSlope = 0.02f, maxSlope = 1f,
                    alignToNormal = true,
                    uniformScaleRange = new Vector2(0.7f, 1.6f),
                },
                new ScatterGroup
                {
                    label = "Tree",
                    prefabs = new[] { treePrefab },
                    count = 900,
                    minHeight = 0f, maxHeight = 0.5f,
                    minSlope = 0f, maxSlope = 0.3f,
                    alignToNormal = false,
                    uniformScaleRange = new Vector2(0.8f, 1.3f),
                    fadeWhenOccludingPlayer = true, // 나무는 시야를 가리므로 기본으로 페이드 대상
                    blockPlayer = true,             // 플레이어가 통과하지 못하게 몸통 콜라이더
                    instanceTag = "Tree",           // 없으면 Scatter 시 자동으로 태그 등록됨
                },
                new ScatterGroup
                {
                    label = "Decoration",
                    prefabs = decorationPrefabs,
                    count = 400,
                    minHeight = 0f, maxHeight = 0.4f,
                    minSlope = 0f, maxSlope = 0.2f,
                    alignToNormal = false,
                    uniformScaleRange = new Vector2(0.8f, 1.2f),
                    castShadows = false,
                },
            };

            EnsureFolder("Settings");

            string desiredPath = $"Assets/Settings/{scatter.terrain.AssetBaseName}_VegetationSettings.asset";
            string path = AssetDatabase.GenerateUniqueAssetPath(desiredPath);
            AssetDatabase.CreateAsset(settings, path);
            scatter.settings = settings;
        }

        static GameObject LoadAssetPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[TerrainSceneSetup] {path} 프리팹을 찾을 수 없습니다. 배치를 건너뜁니다.");
            }
            return prefab;
        }

        // "Assets/X" 폴더가 없으면 만든다. 에셋을 새로 생성하기 전에 반복해서 필요한
        // 패턴이라 하나로 뽑았다.
        static void EnsureFolder(string assetsRelativeFolder)
        {
            if (!AssetDatabase.IsValidFolder($"Assets/{assetsRelativeFolder}"))
            {
                AssetDatabase.CreateFolder("Assets", assetsRelativeFolder);
            }
        }

        // 고정된 공유 경로 대신 이 렌더러에 이미 붙어있는 머티리얼을 기준으로 판단한다 —
        // 그래야 씬을 복사해도 두 씬의 지형이 같은 머티리얼 파일을 계속 같이 가리키다가
        // 한쪽을 튜닝하면 다른 쪽도 같이 바뀌는 일이 없다(이전엔 Assets/Materials/
        // TerrainMountain.mat 고정 경로를 모든 씬이 공유했다). sharedMaterial이 실제
        // 프로젝트 에셋(AssetDatabase에 경로가 있음)이면 이미 이 지형용으로 만들어진
        // 것이므로 그대로 쓰고, 아직 Unity가 기본으로 붙여준 내장 머티리얼(에셋 경로가
        // 없음)이거나 비어있으면 이 지형 이름 기준의 새 파일을 만든다.
        static Material GetOrCreateMaterial(MeshRenderer terrainRenderer, string baseName)
        {
            var shader = Shader.Find("Mountains/TerrainBlend");
            if (shader == null)
            {
                Debug.LogWarning("Mountains/TerrainBlend shader not found; leaving the terrain material unassigned.");
                return null;
            }

            var existing = terrainRenderer != null ? terrainRenderer.sharedMaterial : null;
            bool existingIsRealAsset = existing != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(existing));
            if (existingIsRealAsset)
            {
                // 예전 버전(URP/Lit 그라디언트 방식)으로 만들어진 에셋일 수 있으니
                // 셰이더가 다르면 새 셰이더로 갈아끼운다.
                if (existing.shader != shader)
                {
                    existing.shader = shader;
                    EditorUtility.SetDirty(existing);
                }
                return existing;
            }

            var material = new Material(shader) { name = "TerrainMountain" };

            // 머티리얼을 새로 만들 때만 기본 튜닝값을 세팅한다 — 이미 있는 머티리얼의
            // Inspector 값을 매번 덮어쓰면 사용자가 조정한 값이 재생성할 때마다 날아간다.
            ApplyMaterialFloats(material,
                ("_SlopeCutoff", 0.18f),
                ("_SlopeSmoothness", 0.12f),
                ("_ShadowTint", 0.65f),
                ("_AmbientStrength", 1.1f));

            EnsureFolder("Materials");

            string path = AssetDatabase.GenerateUniqueAssetPath($"Assets/Materials/{baseName}_TerrainMountain.mat");
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // TerrainBlend 셰이더는 월드 Y 높이를 직접 비교해서 Grass/Snow를 나누므로,
        // 지형이 실제로 생성한 높이 범위(최저 0 ~ 최고 HeightRange)를 매번 동기화해줘야 한다.
        static void ApplyHeightRangeToMaterial(Material material, ProceduralTerrainMesh terrain)
        {
            if (!material.HasProperty("_MinHeight"))
            {
                return;
            }

            material.SetFloat("_MinHeight", 0f);
            material.SetFloat("_MaxHeight", Mathf.Max(0.01f, terrain.HeightRange));
            EditorUtility.SetDirty(material);
        }

        // 프로퍼티가 실제로 존재할 때만(다른 셰이더로 바뀌어 있을 수도 있으니) float 값들을
        // 세팅한다. GetOrCreateMaterial()이 머티리얼을 새로 만들 때만 호출한다 — 이미 있는
        // 머티리얼에 매번 호출하면 사용자가 Inspector에서 조정한 값이 재생성할 때마다 날아간다.
        static void ApplyMaterialFloats(Material material, params (string property, float value)[] values)
        {
            foreach (var (property, value) in values)
            {
                if (material.HasProperty(property))
                {
                    material.SetFloat(property, value);
                }
            }
            EditorUtility.SetDirty(material);
        }
    }
}
