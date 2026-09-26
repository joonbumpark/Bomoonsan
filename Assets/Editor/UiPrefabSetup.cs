using System.IO;
using UnityEditor;
using UnityEngine;

namespace Mountains
{
    // 코드로 만들어지는 팝업/토스트 기본 모양을 그대로 프리팹으로 저장해주는 도구.
    //
    // 손으로 계층을 다시 만들게 하지 않는 이유: 레이아웃이 VerticalLayoutGroup +
    // ContentSizeFitter 조합으로 맞물려 있어서 눈으로 재현하기 번거롭고, 컴포넌트 참조
    // (confirmButton, messageText 등)를 하나라도 빠뜨리면 런타임에 조용히 동작하지 않는다.
    // 지금 동작하는 그 구성을 그대로 떠서 저장한 뒤, 프리팹에서 색·여백·폰트만 고치면 된다.
    static class UiPrefabSetup
    {
        const string PrefabFolder = "Assets/Prefabs/UI";
        const string ResourcesFolder = "Assets/Resources";

        [MenuItem("Mountains/UI 프리팹 생성 (팝업 + 토스트)")]
        static void CreateUiPrefabs()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(PrefabFolder);
            EnsureFolder(ResourcesFolder);

            // 빌더가 만든 계층을 씬에 남기지 않기 위해, 임시 캔버스 밑에 지었다가 지운다.
            var temp = new GameObject("~UiPrefabTemp", typeof(Canvas));
            try
            {
                var popup = MessagePopupBuilder.Build(temp.transform);
                var toast = ToastUI.Build(temp.transform);

                var popupPrefab = SavePrefab(popup.gameObject, "MessagePopup");
                var toastPrefab = SavePrefab(toast.gameObject, "Toast");

                var catalog = LoadOrCreateCatalog();
                catalog.messagePopupPrefab = popupPrefab != null ? popupPrefab.GetComponent<MessagePopupUI>() : null;
                catalog.toastPrefab = toastPrefab != null ? toastPrefab.GetComponent<ToastUI>() : null;

                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();

                Selection.activeObject = catalog;
                Debug.Log($"[UiPrefabSetup] 프리팹을 만들고 카탈로그에 연결했습니다.\n" +
                    $"  {PrefabFolder}/MessagePopup.prefab\n" +
                    $"  {PrefabFolder}/Toast.prefab\n" +
                    $"  {ResourcesFolder}/{UiPrefabCatalog.ResourcePath}.asset\n" +
                    "이제 프리팹을 수정하면 런타임에 그대로 쓰입니다(코드 생성 경로는 자동으로 비켜납니다).");
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        static GameObject SavePrefab(GameObject source, string fileName)
        {
            string path = $"{PrefabFolder}/{fileName}.prefab";
            // 이미 있으면 덮어쓴다 — 사용자가 수정해둔 프리팹을 날릴 수 있으므로 확인을 받는다.
            if (File.Exists(path) && !EditorUtility.DisplayDialog("UI 프리팹 생성",
                    $"{path} 이(가) 이미 있습니다. 기본 모양으로 덮어쓸까요?\n" +
                    "프리팹에서 수정한 색·여백·폰트는 사라집니다.", "덮어쓰기", "건너뛰기"))
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            return PrefabUtility.SaveAsPrefabAsset(source, path);
        }

        static UiPrefabCatalog LoadOrCreateCatalog()
        {
            string path = $"{ResourcesFolder}/{UiPrefabCatalog.ResourcePath}.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<UiPrefabCatalog>(path);
            if (catalog != null)
            {
                return catalog;
            }

            catalog = ScriptableObject.CreateInstance<UiPrefabCatalog>();
            AssetDatabase.CreateAsset(catalog, path);
            return catalog;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
