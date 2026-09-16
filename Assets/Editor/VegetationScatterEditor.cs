using UnityEditor;
using UnityEngine;

namespace Mountains
{
    [CustomEditor(typeof(VegetationScatter))]
    public class VegetationScatterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var scatter = (VegetationScatter)target;

            EditorGUILayout.Space();

            if (scatter.settings == null)
            {
                EditorGUILayout.HelpBox("settings가 비어있습니다. Mountains > Create Procedural Terrain을 " +
                    "한 번 실행하면 자동으로 만들어집니다.", MessageType.Info);
                return;
            }

            if (GUILayout.Button("Scatter"))
            {
                scatter.Scatter();
                EditorUtility.SetDirty(scatter);
            }

            if (GUILayout.Button("Clear"))
            {
                scatter.ClearInstances();
            }
        }
    }
}
