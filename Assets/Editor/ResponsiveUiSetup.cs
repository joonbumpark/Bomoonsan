using Mountains;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.EditorTools
{
    /// <summary>
    /// 기기 화면 비율 대응을 프리팹/텍스트 씬에 미리 세팅한다 (실행 중에는 ResponsiveCanvas가
    /// 같은 규칙을 한 번 더 적용하니, 이 도구는 에디터 미리보기를 실제와 맞추는 용도다).
    ///   1. 모든 CanvasScaler를 Expand 모드로 - 1080x1920 기준 영역이 어떤 비율에서도 다 보이게.
    ///   2. 전체 화면 배경 이미지에 AspectRatioFitter(EnvelopeParent)를 붙여, 남는 공간까지
    ///      그림 비율을 유지한 채 꽉 채우게 한다 (넘치는 부분은 화면 밖으로 잘린다).
    ///   3. 게임 화면 점수바(TopBar)를 기준 폭 가운데 고정으로 - 넓은 화면에서 숫자가 패널 밖으로 안 빠지게.
    ///
    /// Mountain 5 씬은 바이너리라 병합 충돌을 피하려고 건드리지 않는다 - 런타임의
    /// ResponsiveCanvas가 캔버스 쪽은 맞춘다.
    ///
    /// 다시 실행해도 안전하다 (이미 붙은 AspectRatioFitter는 값만 갱신).
    /// 배치 모드 호출: -executeMethod Match3.EditorTools.ResponsiveUiSetup.Run
    /// </summary>
    public static class ResponsiveUiSetup
    {
        private const float ReferenceWidth = 1080f;

        // 프리팹 경로 -> 배경 오브젝트 경로(루트 기준). 배경이 없는 팝업은 Expand만 적용.
        private static readonly (string prefab, string background)[] Prefabs =
        {
            ("Assets/Resources/Prefabs/Match3Canvas.prefab", "Background"),
            ("Assets/Resources/Prefabs/TetrisCanvas.prefab", "Background"),
            ("Assets/Resources/Prefabs/JigsawCanvas.prefab", "Background"),
            ("Assets/Resources/Prefabs/CountryQuizCanvas.prefab", "Background"),
        };

        // logoInArt: 그림 안에 로고/글자가 있어 많이 잘리면 안 되는 배경 (MakeLogoSafeCover).
        private static readonly (string scene, string background, bool logoInArt)[] Scenes =
        {
            ("Assets/Scenes/InGameScene.unity", "AppCanvas/Background", false),
            ("Assets/Scenes/TitleScene.unity", "TitleCanvas/TitleRoot/Background", true),
        };

        [MenuItem("Bomoonsan/Setup Responsive UI")]
        public static void Run()
        {
            bool success = TryRun();
            if (Application.isBatchMode)
                EditorApplication.Exit(success ? 0 : 1);
        }

        private static bool TryRun()
        {
            bool ok = true;

            foreach (var (path, background) in Prefabs)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var scaler in root.GetComponentsInChildren<CanvasScaler>(true))
                        ResponsiveCanvas.Apply(scaler);
                    ok &= MakeCover(root.transform.Find(background), path);
                    FixTopBarWidth(root.transform, path);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            foreach (var (path, background, logoInArt) in Scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var scaler in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    // 프리팹 인스턴스는 위에서 프리팹 원본을 고쳤으니 씬 오버라이드를 만들지 않는다.
                    if (!PrefabUtility.IsPartOfPrefabInstance(scaler))
                        ResponsiveCanvas.Apply(scaler);
                }
                var target = FindInScene(background);
                ok &= logoInArt ? MakeLogoSafeCover(target, path) : MakeCover(target, path);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log("[ResponsiveUiSetup] 완료" + (ok ? "" : " (일부 배경을 못 찾음 - 위 로그 확인)"));
            return ok;
        }

        /// <summary>배경 이미지를 부모(캔버스) 전체를 덮도록 만든다 - 그림 비율 유지, 넘치는 부분은 잘림.</summary>
        private static bool MakeCover(Transform target, string owner)
        {
            var image = target != null ? target.GetComponent<Image>() : null;
            if (image == null || image.sprite == null)
            {
                Debug.LogError($"[ResponsiveUiSetup] {owner}: 배경 Image를 찾을 수 없음");
                return false;
            }

            var rt = (RectTransform)target;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            float aspect = SpriteAspect(image.sprite);
            if (!(aspect > 0f) || float.IsInfinity(aspect))
            {
                Debug.LogError($"[ResponsiveUiSetup] {owner}: 배경 이미지 비율을 읽을 수 없음 ({image.sprite.name})");
                return false;
            }

            var fitter = target.GetComponent<AspectRatioFitter>();
            if (fitter == null)
                fitter = target.gameObject.AddComponent<AspectRatioFitter>();

            // 프로퍼티 setter를 쓰면 그 자리에서 레이아웃을 다시 계산하는데, 배치 모드에선 캔버스
            // 크기가 0으로 잡혀 비율이 NaN으로 덮어써진다 - 직렬화 값만 직접 기록하고, 실제 크기
            // 계산은 실행 중에(화면 크기가 있을 때) 컴포넌트가 알아서 하게 둔다.
            var so = new SerializedObject(fitter);
            so.FindProperty("m_AspectMode").intValue = (int)AspectRatioFitter.AspectMode.EnvelopeParent;
            so.FindProperty("m_AspectRatio").floatValue = aspect;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>
        /// 로고가 그려진 배경용: 그림 전체가 보이는 크기에서 조금만(CoverFitter.maxOverscale) 키우고
        /// 위쪽 기준으로 붙여 로고가 잘리지 않게 한다. 그래도 남는 공간은 바로 뒤에 같은 그림을
        /// 어둡게 화면 가득 깐 "BackgroundFill"이 채운다.
        /// </summary>
        private static bool MakeLogoSafeCover(Transform target, string owner)
        {
            var image = target != null ? target.GetComponent<Image>() : null;
            if (image == null || image.sprite == null)
            {
                Debug.LogError($"[ResponsiveUiSetup] {owner}: 배경 Image를 찾을 수 없음");
                return false;
            }

            float aspect = SpriteAspect(image.sprite);
            if (!(aspect > 0f) || float.IsInfinity(aspect))
            {
                Debug.LogError($"[ResponsiveUiSetup] {owner}: 배경 이미지 비율을 읽을 수 없음 ({image.sprite.name})");
                return false;
            }

            var oldFitter = target.GetComponent<AspectRatioFitter>();
            if (oldFitter != null)
                Object.DestroyImmediate(oldFitter);

            var cover = target.GetComponent<CoverFitter>();
            if (cover == null)
                cover = target.gameObject.AddComponent<CoverFitter>();
            cover.aspectRatio = aspect;
            cover.maxOverscale = 1.15f;
            cover.alignment = new Vector2(0.5f, 1f);

            var parent = target.parent;
            var fillTf = parent.Find("BackgroundFill");
            if (fillTf == null)
            {
                var go = new GameObject("BackgroundFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                fillTf = go.transform;
                fillTf.SetParent(parent, false);
            }
            fillTf.SetSiblingIndex(target.GetSiblingIndex());

            var fill = fillTf.GetComponent<Image>();
            fill.sprite = image.sprite;
            fill.color = new Color(0.45f, 0.45f, 0.45f, 1f);
            fill.raycastTarget = false;

            var fillRt = (RectTransform)fillTf;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.pivot = new Vector2(0.5f, 0.5f);
            fillRt.anchoredPosition = Vector2.zero;
            fillRt.sizeDelta = Vector2.zero;

            var fitter = fillTf.GetComponent<AspectRatioFitter>();
            if (fitter == null)
                fitter = fillTf.gameObject.AddComponent<AspectRatioFitter>();
            var so = new SerializedObject(fitter);
            so.FindProperty("m_AspectMode").intValue = (int)AspectRatioFitter.AspectMode.EnvelopeParent;
            so.FindProperty("m_AspectRatio").floatValue = aspect;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // 스프라이트 rect가 비어 있는 경우(배치 모드에서 텍스처가 아직 안 올라온 씬 등)가 있어서,
        // 그때는 임포터에 기록된 원본 이미지 크기로 계산한다.
        private static float SpriteAspect(Sprite sprite)
        {
            var rect = sprite.rect;
            if (rect.width > 0f && rect.height > 0f)
                return rect.width / rect.height;

            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite)) as TextureImporter;
            if (importer == null)
                return 0f;
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            return height > 0 ? (float)width / height : 0f;
        }

        /// <summary>
        /// 화면 위쪽 점수바는 가로 전체로 늘어나게 돼 있는데, 그 안의 나무 패널 배경은 가운데
        /// 고정 크기라 화면이 넓어지면 점수 숫자만 패널 밖 왼쪽으로 빠져나간다 - 바 자체를
        /// 기준 폭(1080) 가운데 고정으로 바꿔 안의 배치가 항상 같게 한다.
        /// </summary>
        private static void FixTopBarWidth(Transform root, string owner)
        {
            var topBar = root.Find("TopBar") as RectTransform;
            if (topBar == null)
            {
                Debug.LogWarning($"[ResponsiveUiSetup] {owner}: TopBar 없음 - 건너뜀");
                return;
            }

            topBar.anchorMin = new Vector2(0.5f, topBar.anchorMin.y);
            topBar.anchorMax = new Vector2(0.5f, topBar.anchorMax.y);
            topBar.sizeDelta = new Vector2(ReferenceWidth, topBar.sizeDelta.y);
            topBar.anchoredPosition = new Vector2(0f, topBar.anchoredPosition.y);
        }

        private static GameObject FindRoot(string name)
        {
            foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name == name)
                    return go;
            return null;
        }

        private static Transform FindInScene(string path)
        {
            int slash = path.IndexOf('/');
            var root = FindRoot(slash < 0 ? path : path.Substring(0, slash));
            if (root == null)
                return null;
            return slash < 0 ? root.transform : root.transform.Find(path.Substring(slash + 1));
        }
    }
}
