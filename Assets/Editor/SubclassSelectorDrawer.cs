using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Mountains
{
    // [SerializeReference] 항목 옆에 타입 드롭다운을 그린다. 고르면 그 자리에서 해당 타입의
    // 인스턴스를 만들어 넣는다 — 액션을 추가할 때 자식 GameObject를 만들지 않아도 되는 이유.
    //
    // 배열 필드에 붙은 PropertyAttribute는 Unity가 "각 요소"에 적용하므로, 이 드로어 하나로
    // 목록의 모든 칸이 드롭다운을 갖는다.
    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public class SubclassSelectorDrawer : PropertyDrawer
    {
        const string NoneLabel = "(없음)";
        // Unity의 ManagedReferenceUtility.RefIdNull / RefIdUnknown 값 — 네임스페이스에 의존하지 않으려고 직접 둔다.
        const long RefIdNull = -2;
        const long RefIdUnknown = -1;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            // 배열의 +로 칸을 늘리면 Unity가 마지막 칸을 그대로 복제하는데, [SerializeReference]에서는
            // 복제가 아니라 같은 인스턴스를 가리키게 된다 — 새 칸을 고치면 원본도 같이 바뀐다.
            // 새 칸은 늘 빈 칸((없음))으로 시작하도록 중복 참조를 비운다.
            ClearDuplicatedReference(property);

            // 본체(폴드아웃 + 하위 필드)를 먼저 그리고, 첫 줄 오른쪽에 드롭다운을 겹쳐 올린다.
            EditorGUI.PropertyField(position, property, label, true);

            var dropdownRect = new Rect(
                position.x + EditorGUIUtility.labelWidth + 2f,
                position.y,
                position.width - EditorGUIUtility.labelWidth - 2f,
                EditorGUIUtility.singleLineHeight);

            if (EditorGUI.DropdownButton(dropdownRect, new GUIContent(GetDisplayName(property)), FocusType.Keyboard))
            {
                ShowTypeMenu(property, dropdownRect);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, true);
        }

        // 같은 오브젝트 안에서 이미 앞쪽 속성이 가리키고 있는 인스턴스를 또 가리키는 칸이면
        // 비운다. 의도적으로 한 인스턴스를 여러 칸이 공유하는 경우는 이 시스템에 없다.
        // 그리는 도중에 값을 고치면 인스펙터의 속성 순회가 깨지므로, 감지만 하고 수정은
        // 그리기가 끝난 뒤(delayCall)에 새 SerializedObject로 한다.
        static readonly HashSet<string> PendingClears = new HashSet<string>();

        static void ClearDuplicatedReference(SerializedProperty property)
        {
            if (!IsDuplicatedReference(property))
            {
                return;
            }

            var targets = property.serializedObject.targetObjects;
            string path = property.propertyPath;
            string key = property.serializedObject.targetObject.GetInstanceID() + path;
            if (!PendingClears.Add(key))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                PendingClears.Remove(key);
                if (targets == null || targets.Length == 0 || targets[0] == null)
                {
                    return;
                }

                var serializedObject = new SerializedObject(targets);
                var target = serializedObject.FindProperty(path);
                if (target == null || !IsDuplicatedReference(target))
                {
                    return;
                }

                target.managedReferenceValue = null;
                serializedObject.ApplyModifiedProperties();
                InternalEditorUtility.RepaintAllViews();
            };
        }

        static bool IsDuplicatedReference(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                return false;
            }

            long id = property.managedReferenceId;
            if (id == RefIdNull || id == RefIdUnknown)
            {
                return false;
            }

            string myPath = property.propertyPath;
            var iterator = property.serializedObject.GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ManagedReference ||
                    iterator.managedReferenceId != id)
                {
                    continue;
                }

                // 가장 앞선 소유자는 원본, 그 뒤에 같은 id가 나오면 복제된 칸이다.
                return iterator.propertyPath != myPath;
            }

            return false;
        }

        static string GetDisplayName(SerializedProperty property)
        {
            string full = property.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(full))
            {
                return NoneLabel;
            }

            // "어셈블리 네임스페이스.타입" 형태로 들어오므로 마지막 타입 이름만 쓴다.
            int dot = full.LastIndexOf('.');
            return dot >= 0 ? full.Substring(dot + 1) : full;
        }

        void ShowTypeMenu(SerializedProperty property, Rect rect)
        {
            var baseType = GetElementType(fieldInfo.FieldType);
            var menu = new GenericMenu();

            // 비우는 선택지도 준다 — 잘못 넣은 액션을 지우려고 배열 칸을 통째로 없앨 필요가 없다.
            var propertyPath = property.propertyPath;
            var serializedObject = property.serializedObject;

            menu.AddItem(new GUIContent(NoneLabel), string.IsNullOrEmpty(property.managedReferenceFullTypename),
                () => Assign(serializedObject, propertyPath, null));

            foreach (var type in GetCandidateTypes(baseType))
            {
                var captured = type;
                menu.AddItem(new GUIContent(type.Name),
                    GetDisplayName(property) == type.Name,
                    () => Assign(serializedObject, propertyPath, captured));
            }

            menu.DropDown(rect);
        }

        // 메뉴 콜백은 나중에 실행되므로 SerializedProperty를 그대로 들고 있으면 안 된다
        // (그 사이 인스펙터가 다시 그려지면 무효가 된다) — 경로로 다시 찾는다.
        static void Assign(SerializedObject serializedObject, string propertyPath, Type type)
        {
            serializedObject.Update();
            var property = serializedObject.FindProperty(propertyPath);
            if (property == null)
            {
                return;
            }

            property.managedReferenceValue = type != null ? Activator.CreateInstance(type) : null;
            serializedObject.ApplyModifiedProperties();
        }

        static IEnumerable<Type> GetCandidateTypes(Type baseType)
        {
            var types = new List<Type>();
            foreach (var type in TypeCache.GetTypesDerivedFrom(baseType))
            {
                if (type.IsAbstract || type.IsGenericType || !type.IsClass)
                {
                    continue;
                }
                // 매개변수 없는 생성자가 있어야 만들 수 있다.
                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    continue;
                }
                types.Add(type);
            }

            types.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return types;
        }

        // 배열/리스트 필드에 붙으면 fieldInfo는 배열 타입이다 — 실제 후보는 그 요소 타입이다.
        static Type GetElementType(Type fieldType)
        {
            if (fieldType.IsArray)
            {
                return fieldType.GetElementType();
            }

            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                return fieldType.GetGenericArguments()[0];
            }

            return fieldType;
        }
    }
}
