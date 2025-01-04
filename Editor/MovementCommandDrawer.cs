using UnityEngine;
using UnityEditor;
using PuzzlePlatform.Core;

namespace PuzzlePlatform.Editor
{
    [CustomPropertyDrawer(typeof(PuzzleInstance.MovementCommand))]
    public class MovementCommandDrawer : PropertyDrawer
    {
        private static bool needsRepaint = false;

        static MovementCommandDrawer()
        {
            EditorApplication.update += () =>
            {
                if (needsRepaint)
                {
                    needsRepaint = false;
                    foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                    {
                        window.Repaint();
                    }
                }
            };
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var puzzle = property.serializedObject.targetObject as PuzzleInstance;
            if (puzzle == null)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            if (Application.isPlaying)
            {
                if (puzzle.CurrentState == PlatformState.ExecutingCommands)
                {
                    needsRepaint = true;
                }
            }

            var path = property.propertyPath;
            int index = -1;
            if (path.Contains("commandList.Array.data["))
            {
                var startIndex = path.IndexOf("[") + 1;
                var endIndex = path.IndexOf("]");
                if (int.TryParse(path.Substring(startIndex, endIndex - startIndex), out index))
                {
                    if (Application.isPlaying && index == puzzle.CurrentCommandIndex && puzzle.CurrentCommandIndex >= 0)
                    {
                        var highlightColor = new Color(0.8f, 0.8f, 0.2f, 0.3f);
                        EditorGUI.DrawRect(position, highlightColor);
                    }
                }
            }

            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(position, property, label);
            if (EditorGUI.EndChangeCheck())
            {
                property.serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(puzzle);
            }
        }
    }
}