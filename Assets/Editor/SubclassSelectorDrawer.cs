using System;
using System.Collections.Generic;
using UnityEditor;
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

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

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
