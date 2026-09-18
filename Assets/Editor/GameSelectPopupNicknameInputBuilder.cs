using UnityEditor;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// GameSelectPopup.prefab(이미 아트가 입혀진 프리팹)에 닉네임 입력 필드를 한 번
    /// 덧붙여준다. 다시 실행하면 기존에 이 툴이 만든 "NicknameInput"을 지우고 새로 만드니
    /// 위치/크기를 프리팹에서 직접 조정했다면 재실행 전에 값을 기억해둘 것.
    ///
    /// 배치 모드 호출: -executeMethod Match3.EditorTools.GameSelectPopupNicknameInputBuilder.Build
    /// </summary>
    public static class GameSelectPopupNicknameInputBuilder
    {
        private const string PrefabPath = "Assets/Resources/Prefabs/GameSelectPopup.prefab";

        [MenuItem("Bomoonsan/Add Nickname Input To Game Select Popup")]
        public static void Build()
        {
            bool success = TryBuild();
            if (Application.isBatchMode)
                EditorApplication.Exit(success ? 0 : 1);
        }

        private static bool TryBuild()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                Debug.LogError($"[GameSelectPopupNicknameInputBuilder] 프리팹을 찾을 수 없음: {PrefabPath}");
                return false;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var popup = root.GetComponent<GameSelectPopup>();
                if (popup == null)
                {
                    Debug.LogError("[GameSelectPopupNicknameInputBuilder] GameSelectPopup 컴포넌트를 찾을 수 없음");
                    return false;
                }

                Transform bg = root.transform.Find("dim/bg");
                if (bg == null)
                {
                    Debug.LogError("[GameSelectPopupNicknameInputBuilder] 'dim/bg'를 찾을 수 없음 - 프리팹 구조가 바뀌었는지 확인할 것.");
                    return false;
                }

                Transform existing = bg.Find("NicknameInput");
                if (existing != null)
                    Object.DestroyImmediate(existing.gameObject);

                // 버튼 3개가 y=-142 높이(120px)에 있으니, 그 위쪽 빈 공간(카드는
                // 929x900, 중심 기준 위로 450까지)에 입력창을 둔다.
                var inputField = UIFactory.CreateInputField(
                    "NicknameInput", bg, "닉네임 (비우면 랜덤)",
                    new Vector2(0, 220), new Vector2(760, 130));

                var so = new SerializedObject(popup);
                SerializedProperty prop = so.FindProperty("nicknameInput");
                if (prop == null)
                {
                    Debug.LogError("[GameSelectPopupNicknameInputBuilder] GameSelectPopup에 'nicknameInput' 필드가 없음 - 스크립트가 바뀌었는지 확인할 것.");
                    return false;
                }
                prop.objectReferenceValue = inputField;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[GameSelectPopupNicknameInputBuilder] 닉네임 입력 필드 추가 완료.");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
