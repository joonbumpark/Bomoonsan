using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mountains
{
    // [NpcKey] string 필드를 NpcKeyStore 기반 드롭다운으로 그린다. Unity의 Tag 필드처럼
    // 목록에서 고르고, 맨 아래 "키 관리..."로 저장소를 열어 키를 추가한다.
    [CustomPropertyDrawer(typeof(NpcKeyAttribute))]
    public class NpcKeyDrawer : PropertyDrawer
    {
        const string NoneLabel = "(없음)";
        const string ManageLabel = "키 관리...";
        const string DefaultStorePath = "Assets/Settings/NpcKeyStore.asset";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            position = EditorGUI.PrefixLabel(position, label);

            var store = FindStore();
            string current = property.stringValue;

            // 저장소에 없는 값은 이름을 바꿨거나 저장소가 지워진 경우다 — 조용히 비우면
            // 연결이 끊긴 걸 눈치채지 못하므로 눈에 띄게 표시한다.
            bool missing = !string.IsNullOrEmpty(current) && (store == null || !store.Contains(current));
            var content = new GUIContent(string.IsNullOrEmpty(current) ? NoneLabel
                : missing ? $"{current}  (목록에 없음)" : current);

            var previousColor = GUI.color;
            if (missing)
            {
                GUI.color = new Color(1f, 0.6f, 0.6f);
            }

            if (EditorGUI.DropdownButton(position, content, FocusType.Keyboard))
            {
                ShowMenu(property, store, current);
            }

            GUI.color = previousColor;
        }

        void ShowMenu(SerializedProperty property, NpcKeyStore store, string current)
        {
            var menu = new GenericMenu();
            var serializedObject = property.serializedObject;
            string path = property.propertyPath;

            menu.AddItem(new GUIContent(NoneLabel), string.IsNullOrEmpty(current),
                () => Assign(serializedObject, path, string.Empty));

            if (store != null)
            {
                menu.AddSeparator(string.Empty);
                foreach (var key in store.keys.Where(k => !string.IsNullOrEmpty(k)))
                {
                    string captured = key;
                    menu.AddItem(new GUIContent(key), current == key,
                        () => Assign(serializedObject, path, captured));
                }
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent(ManageLabel), false, () => SelectStore(store));
            menu.ShowAsContext();
        }

        static void Assign(SerializedObject serializedObject, string propertyPath, string value)
        {
            serializedObject.Update();
            var property = serializedObject.FindProperty(propertyPath);
            if (property == null)
            {
                return;
            }

            property.stringValue = value;
            serializedObject.ApplyModifiedProperties();
        }

        // 저장소가 없으면 만들어준다 — 키를 처음 쓰는 사람이 에셋부터 찾아 만들 필요가 없게.
        static void SelectStore(NpcKeyStore store)
        {
            if (store == null)
            {
                store = CreateStore();
            }

            Selection.activeObject = store;
            EditorGUIUtility.PingObject(store);
        }

        public static NpcKeyStore FindStore()
        {
            var guids = AssetDatabase.FindAssets("t:NpcKeyStore");
            if (guids.Length == 0)
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<NpcKeyStore>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        public static NpcKeyStore FindOrCreateStore()
        {
            return FindStore() ?? CreateStore();
        }

        static NpcKeyStore CreateStore()
        {
            string folder = Path.GetDirectoryName(DefaultStorePath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'),
                    Path.GetFileName(folder));
            }

            var store = ScriptableObject.CreateInstance<NpcKeyStore>();
            AssetDatabase.CreateAsset(store, DefaultStorePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[NpcKeyDrawer] 키 저장소를 만들었습니다: {DefaultStorePath}");
            return store;
        }
    }
}
