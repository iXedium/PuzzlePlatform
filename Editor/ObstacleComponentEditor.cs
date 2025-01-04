using UnityEngine;
using UnityEditor;
using PuzzlePlatform.Core;

namespace PuzzlePlatform.Editor
{
    [CustomEditor(typeof(ObstacleComponent))]
    public class ObstacleComponentEditor : UnityEditor.Editor
    {
        private SerializedProperty isStaticProp;
        private SerializedProperty gizmoColorProp;
        private SerializedProperty showGizmosProp;

        private void OnEnable()
        {
            isStaticProp = serializedObject.FindProperty("isStatic");
            gizmoColorProp = serializedObject.FindProperty("gizmoColor");
            showGizmosProp = serializedObject.FindProperty("showGizmos");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(isStaticProp, new GUIContent("Is Static", "If true, obstacle cells are only calculated once"));
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Visualization", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            EditorGUILayout.PropertyField(showGizmosProp, new GUIContent("Show Gizmos", "Display occupied cells in the Scene view"));
            if (showGizmosProp.boolValue)
            {
                EditorGUILayout.PropertyField(gizmoColorProp, new GUIContent("Gizmo Color", "Color of the occupied cell visualization"));
            }
            
            EditorGUI.indentLevel--;

            if (serializedObject.hasModifiedProperties)
            {
                serializedObject.ApplyModifiedProperties();
                
                // If static status changed, recalculate cells
                var obstacle = (ObstacleComponent)target;
                if (obstacle != null)
                {
                    obstacle.CalculateOccupiedCells();
                    // Force a scene view repaint to update visualizations
                    SceneView.RepaintAll();
                }
            }
        }
    }
} 