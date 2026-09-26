#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Mountains
{
    // [GizmoTarget]이 붙은 필드가 가리키는 씬 오브젝트 위에 액션 이름을 그린다.
    // EventTrigger.OnDrawGizmos에서 부른다.
    //
    // 에디터 전용이라 파일 전체를 #if UNITY_EDITOR로 감쌌다 — Handles는 빌드에 포함되지
    // 않는다. EventTrigger 자체는 런타임 컴포넌트라 Editor 폴더로 옮길 수 없어서, 그리는
    // 코드만 이렇게 분리한다.
    static class TriggerActionGizmos
    {
        static readonly Color LineColor = new Color(1f, 0.55f, 0.1f, 0.9f);
        const float MarkerRadius = 0.3f;

        // 리플렉션은 OnDrawGizmos마다 돌면 낭비다 — 타입당 한 번만 훑고 기억한다.
        static readonly Dictionary<Type, FieldInfo[]> _markedFields = new Dictionary<Type, FieldInfo[]>();

        public static void Draw(Transform origin, EventTrigger.ActionStep[] steps)
        {
            if (origin == null || steps == null)
            {
                return;
            }

            Gizmos.color = LineColor;
            foreach (var step in steps)
            {
                if (step?.newActions == null)
                {
                    continue;
                }

                foreach (var action in step.newActions)
                {
                    if (action != null)
                    {
                        DrawAction(origin, action);
                    }
                }
            }
        }

        static void DrawAction(Transform origin, TriggerAction action)
        {
            var type = action.GetType();
            foreach (var field in GetMarkedFields(type))
            {
                var attribute = field.GetCustomAttribute<GizmoTargetAttribute>();
                string label = !string.IsNullOrEmpty(attribute.Label)
                    ? attribute.Label
                    : $"{type.Name}.{field.Name}";

                foreach (var target in EnumerateTransforms(field.GetValue(action)))
                {
                    Gizmos.DrawLine(origin.position, target.position);
                    Gizmos.DrawWireSphere(target.position, MarkerRadius);
                    Handles.Label(target.position + Vector3.up * (MarkerRadius + 0.2f), label);
                }
            }
        }

        // Transform 하나든 배열이든, GameObject로 잡아뒀든 같은 방식으로 다룬다.
        static IEnumerable<Transform> EnumerateTransforms(object value)
        {
            // 비어 있거나 파괴된 참조는 UnityEngine.Object의 "가짜 null"이다 — 관리 객체는
            // 살아 있어서 (value == null)이나 is 검사로는 걸러지지 않고, position을 읽는
            // 순간에야 NullReferenceException이 난다. UnityEngine.Object로 한 번 더 확인한다.
            if (value == null)
            {
                yield break;
            }

            if (value is Transform transform)
            {
                if (transform != null)
                {
                    yield return transform;
                }
                yield break;
            }

            if (value is GameObject gameObject)
            {
                if (gameObject != null)
                {
                    yield return gameObject.transform;
                }
                yield break;
            }

            if (value is System.Collections.IEnumerable list)
            {
                foreach (var item in list)
                {
                    if (item is Transform t && t != null)
                    {
                        yield return t;
                    }
                    else if (item is GameObject go && go != null)
                    {
                        yield return go.transform;
                    }
                }
            }
        }

        static FieldInfo[] GetMarkedFields(Type type)
        {
            if (_markedFields.TryGetValue(type, out var cached))
            {
                return cached;
            }

            var found = new List<FieldInfo>();
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.GetCustomAttribute<GizmoTargetAttribute>() != null)
                {
                    found.Add(field);
                }
            }

            cached = found.ToArray();
            _markedFields[type] = cached;
            return cached;
        }
    }
}
#endif
