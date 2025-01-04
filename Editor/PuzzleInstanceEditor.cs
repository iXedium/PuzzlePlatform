using UnityEditor;
using UnityEngine;
using PuzzlePlatform.Core;
using System.Linq;

namespace PuzzlePlatform.Editor
{
    [CustomEditor(typeof(PuzzleInstance))]
    public class PuzzleInstanceEditor : UnityEditor.Editor
    {
        private PuzzleInstance puzzle;
        private SerializedProperty gridSizeProp;
        private SerializedProperty movementAreaProp;
        private SerializedProperty restingPositionProp;
        private SerializedProperty commandListProp;
        private SerializedProperty numberOfCommandSlotsProp;
        private SerializedProperty puzzleIdProp;
        private SerializedProperty waitTimeProp;
        private bool showGridSettings = true;
        private bool showCommands = true;
        private bool showWaitDurations = true;
        private bool showGrid = true;
        private readonly Color handleColor = new Color(0.2f, 0.8f, 0.2f, 0.8f);
        private readonly Color gridColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);
        private readonly Color cellColor = new Color(0.2f, 0.6f, 1f, 0.3f);
        private const float MIN_CELL_SIZE = 1f;
        private const int MIN_BOUNDARY_SIZE = 1;
        private const int DEFAULT_CELL_SIZE = 3;
        private const int DEFAULT_BOUNDARY_SIZE = 10;
        private Vector3Int gridPosition = Vector3Int.zero;
        private Vector3Int platformPosition = Vector3Int.zero;
        private Vector3 savedRestingPosition;

        [SerializeField]
        private Color gridLineColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);

        [SerializeField]
        private float gridLineOpacity = 0.2f;

        private bool showMovementSettings = true;
        private SerializedProperty movementSettingsProp;

        [SerializeField] private bool showProximityGrid = false;
        [SerializeField] private int proximityRange = 3; // Number of cells to show around platform
        private SerializedProperty platformObjectProp;

        private bool showVisualizationSettings = true;
        private bool showBoundaryBox = true;
        private bool showFaceHandles = true;
        private bool showEdgeHandles = true;
        private bool showCornerHandles = true;
        private bool showPlatformHandle = true;

        [SerializeField] private bool useProximityGrid = false;
        [SerializeField] private float gridProximityRadius = 3f;
        [SerializeField] private float gridFalloffDistance = 2f; // Distance over which grid fades out
        [SerializeField] private float gridFalloffPower = 1f; // Controls how quickly the grid fades (1 = linear, >1 = sharper falloff)

        [SerializeField] private bool showOnlyPlatformLevel = false;

        [SerializeField] private bool syncMovementSettings = true;

        [SerializeField] private bool showObstacles = true;
        [SerializeField] private Color obstacleColor = new Color(1f, 0f, 0f, 0.3f);

        private SerializedProperty persistentVisualizationProp;

        public bool ShowBoundaryBox => showBoundaryBox;

        private void OnEnable()
        {
            puzzle = (PuzzleInstance)target;
            InitializeProperties();
            EnsureValidSizes();
            LoadEditorPrefs();

            EditorApplication.update += OnEditorUpdate;
            platformObjectProp = serializedObject.FindProperty("platformObject");
            LoadVisualizationPrefs();

            // Subscribe to state changes
            if (puzzle != null)
            {
                puzzle.OnStateChanged += OnPlatformStateChanged;
            }

            syncMovementSettings = EditorPrefs.GetBool("PuzzleInstance_SyncMovementSettings", true);
        }

        private void InitializeProperties()
        {
            gridSizeProp = serializedObject.FindProperty("gridSize");
            movementAreaProp = serializedObject.FindProperty("movementArea");
            restingPositionProp = serializedObject.FindProperty("restingPosition");
            commandListProp = serializedObject.FindProperty("commandList");
            numberOfCommandSlotsProp = serializedObject.FindProperty("numberOfCommandSlots");
            puzzleIdProp = serializedObject.FindProperty("puzzleId");
            waitTimeProp = serializedObject.FindProperty("waitTime");
            movementSettingsProp = serializedObject.FindProperty("movementSettings");
            persistentVisualizationProp = serializedObject.FindProperty("persistentVisualization");
        }

        private void EnsureValidSizes()
        {
            if (gridSizeProp.vector3IntValue == Vector3Int.zero)
            {
                gridSizeProp.vector3IntValue = new Vector3Int(
                    DEFAULT_CELL_SIZE,
                    DEFAULT_CELL_SIZE,
                    DEFAULT_CELL_SIZE
                );
            }
            Vector3 currentSize = movementAreaProp.boundsValue.size;
            if (currentSize == Vector3.zero)
            {
                Bounds newBounds = new Bounds(
                    Vector3.zero,
                    new Vector3(
                        DEFAULT_BOUNDARY_SIZE * gridSizeProp.vector3IntValue.x,
                        DEFAULT_BOUNDARY_SIZE * gridSizeProp.vector3IntValue.y,
                        DEFAULT_BOUNDARY_SIZE * gridSizeProp.vector3IntValue.z
                    )
                );
                movementAreaProp.boundsValue = newBounds;
            }
            serializedObject.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI()
        {
            LoadVisualizationPrefs();

            serializedObject.Update();
            DrawIdentitySection();
            DrawGridSettings();
            DrawMovementSettingsSection();
            DrawGridPositionSection();
            DrawCommandSection();
            DrawWaitDurationsSection();
            DrawVisualizationSettings();
            if (serializedObject.hasModifiedProperties)
            {
                serializedObject.ApplyModifiedProperties();
                SceneView.RepaintAll();
            }
        }

        private void DrawVisualizationSettings()
        {
            EditorGUI.BeginChangeCheck();

            showVisualizationSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showVisualizationSettings, "Visualization Settings");
            if (showVisualizationSettings)
            {
                EditorGUI.indentLevel++;

                EditorGUI.BeginChangeCheck();

                EditorGUILayout.PropertyField(
                    persistentVisualizationProp,
                    new GUIContent(
                        "Persistent Visualization",
                        "Keep visualization elements visible even when this object is not selected"
                    )
                );

                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    SceneView.RepaintAll();
                }

                EditorGUILayout.Space(5);

                showGrid = EditorGUILayout.Toggle(new GUIContent("Show Grid", "Display the movement grid"), showGrid);
                if (showGrid)
                {
                    EditorGUI.indentLevel++;
                    showOnlyPlatformLevel = EditorGUILayout.Toggle(new GUIContent("Show Only Platform Level", "Only show grid at platform's height"), showOnlyPlatformLevel);
                    useProximityGrid = EditorGUILayout.Toggle(new GUIContent("Use Proximity Grid", "Only show grid near platform"), useProximityGrid);
                    if (useProximityGrid)
                    {
                        gridProximityRadius = EditorGUILayout.FloatField(new GUIContent("Grid Radius (Cells)", "Number of cells to show around platform"), gridProximityRadius);
                        gridProximityRadius = Mathf.Max(1f, gridProximityRadius);
                        gridFalloffDistance = EditorGUILayout.FloatField(new GUIContent("Falloff Distance (Cells)", "Distance over which grid fades out"), gridFalloffDistance);
                        gridFalloffDistance = Mathf.Max(0.1f, gridFalloffDistance);
                        gridFalloffPower = EditorGUILayout.Slider(new GUIContent("Falloff Power", "Controls how quickly the grid fades (1 = linear, >1 = sharper)"), gridFalloffPower, 0.1f, 5f);
                    }
                    gridLineOpacity = EditorGUILayout.Slider("Grid Opacity", gridLineOpacity, 0f, 1f);
                    gridLineColor = EditorGUILayout.ColorField("Grid Color", gridLineColor);
                    EditorGUI.indentLevel--;
                }

                showObstacles = EditorGUILayout.Toggle(new GUIContent("Show Obstacles", "Display obstacle cells"), showObstacles);
                if (showObstacles)
                {
                    EditorGUI.indentLevel++;
                    obstacleColor = EditorGUILayout.ColorField(new GUIContent("Obstacle Color", "Color of obstacle cells"), obstacleColor);
                    EditorGUI.indentLevel--;
                }

                showBoundaryBox = EditorGUILayout.Toggle(new GUIContent("Show Boundary Box", "Display the movement area boundary"), showBoundaryBox);
                showFaceHandles = EditorGUILayout.Toggle(new GUIContent("Show Face Handles", "Display handles for resizing faces"), showFaceHandles);
                showEdgeHandles = EditorGUILayout.Toggle(new GUIContent("Show Edge Handles", "Display handles for resizing edges"), showEdgeHandles);
                showCornerHandles = EditorGUILayout.Toggle(new GUIContent("Show Corner Handles", "Display handles for resizing corners"), showCornerHandles);
                showPlatformHandle = EditorGUILayout.Toggle(new GUIContent("Show Platform Handle", "Display the platform movement handle"), showPlatformHandle);

                if (EditorGUI.EndChangeCheck())
                {
                    SaveVisualizationPrefs();

                    // Update all PuzzleInstance editors
                    var editors = Resources.FindObjectsOfTypeAll<PuzzleInstanceEditor>();
                    foreach (var editor in editors)
                    {
                        if (editor != this)
                        {
                            editor.LoadVisualizationPrefs();
                            editor.Repaint();
                        }
                    }

                    SceneView.RepaintAll();
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // If any setting changed, save and update all editors
            if (EditorGUI.EndChangeCheck())
            {
                SaveVisualizationPrefs();
            }
        }

        private void DrawGridSettings()
        {
            showGridSettings = EditorGUILayout.Foldout(showGridSettings, "Grid Settings", true);
            if (!showGridSettings)
                return;
            EditorGUI.indentLevel++;
            Vector3Int currentGridSize = gridSizeProp.vector3IntValue;
            Vector3Int newGridSize = EditorGUILayout.Vector3IntField(
                new GUIContent("Cell Size", "Size of each grid cell in world units"),
                currentGridSize
            );
            newGridSize.x = Mathf.Max(newGridSize.x, (int)MIN_CELL_SIZE);
            newGridSize.y = Mathf.Max(newGridSize.y, (int)MIN_CELL_SIZE);
            newGridSize.z = Mathf.Max(newGridSize.z, (int)MIN_CELL_SIZE);
            if (newGridSize != currentGridSize)
            {
                gridSizeProp.vector3IntValue = newGridSize;
                UpdateMovementAreaToFitCells();
            }
            Bounds bounds = movementAreaProp.boundsValue;
            Vector3Int cellCount = GetCellCount();
            Vector3Int newCellCount = EditorGUILayout.Vector3IntField(
                new GUIContent("Boundary Size (Cells)", "Number of cells in each direction"),
                cellCount
            );
            newCellCount.x = Mathf.Max(newCellCount.x, MIN_BOUNDARY_SIZE);
            newCellCount.y = Mathf.Max(newCellCount.y, MIN_BOUNDARY_SIZE);
            newCellCount.z = Mathf.Max(newCellCount.z, MIN_BOUNDARY_SIZE);
            if (newCellCount != cellCount)
            {
                bounds.size = new Vector3(
                    newCellCount.x * newGridSize.x,
                    newCellCount.y * newGridSize.y,
                    newCellCount.z * newGridSize.z
                );
                movementAreaProp.boundsValue = bounds;
            }
            if (!IsRestingPositionValid())
            {
                EditorGUILayout.HelpBox(
                    "Resting position is outside movement area!",
                    MessageType.Warning
                );
            }
            EditorGUILayout.PropertyField(
                restingPositionProp,
                new GUIContent("Resting Position", "Starting position (in grid cells)")
            );
            EditorGUI.indentLevel--;
        }

        private Vector3Int GetCellCount()
        {
            Bounds bounds = movementAreaProp.boundsValue;
            Vector3Int gridSize = gridSizeProp.vector3IntValue;
            return new Vector3Int(
                Mathf.RoundToInt(bounds.size.x / gridSize.x),
                Mathf.RoundToInt(bounds.size.y / gridSize.y),
                Mathf.RoundToInt(bounds.size.z / gridSize.z)
            );
        }

        private void UpdateMovementAreaToFitCells()
        {
            Vector3Int cellCount = GetCellCount();
            Vector3Int gridSize = gridSizeProp.vector3IntValue;
            Bounds bounds = movementAreaProp.boundsValue;
            bounds.size = new Vector3(
                cellCount.x * gridSize.x,
                cellCount.y * gridSize.y,
                cellCount.z * gridSize.z
            );
            movementAreaProp.boundsValue = bounds;
        }

        private void OnSceneGUI()
        {
            if (puzzle == null) return;

            Matrix4x4 oldMatrix = Handles.matrix;
            Color oldColor = Handles.color;

            Handles.matrix = puzzle.transform.localToWorldMatrix;

            if (showGrid)
            {
                DrawGrid();
            }

            if (showObstacles)
            {
                DrawObstacles();
            }

            if (showPlatformHandle)
            {
                DrawPlatform();
            }

            Handles.color = handleColor;

            if (showBoundaryBox || showCornerHandles || showEdgeHandles)
            {
                DrawMovementAreaHandles();
            }

            if (showFaceHandles)
            {
                DrawFaceHandles();
            }

            Handles.color = oldColor;
            Handles.matrix = oldMatrix;
        }

        private void DrawPlatform()
        {
            Vector3Int gridSize = gridSizeProp.vector3IntValue;
            Vector3 localPos;

            if (Application.isPlaying)
            {
                // In play mode, use local position directly
                localPos = puzzle.transform.InverseTransformPoint(puzzle.GetPlatformWorldPosition());
            }
            else
            {
                // In edit mode, calculate local position from resting position
                localPos = new Vector3(
                    restingPositionProp.vector3IntValue.x * gridSize.x,
                    restingPositionProp.vector3IntValue.y * gridSize.y,
                    restingPositionProp.vector3IntValue.z * gridSize.z
                );
            }

            Color fillColor = new Color(cellColor.r, cellColor.g, cellColor.b, 0.2f);
            Color outlineColor = new Color(cellColor.r, cellColor.g, cellColor.b, 0.8f);

            // Draw in local space since matrix is set
            DrawPlatformCell(localPos, gridSize, fillColor, outlineColor);

            if (!Application.isPlaying)
            {
                DrawPlatformHandle(localPos);
                DrawGridCoordinates(localPos);
            }
        }

        private void DrawPlatformCell(
            Vector3 position,
            Vector3Int size,
            Color fillColor,
            Color outlineColor
        )
        {
            DrawCellFaces(position, size, fillColor);
            Handles.color = outlineColor;
            Handles.DrawWireCube(position + (Vector3)size * 0.5f, size);
        }

        private void DrawGrid()
        {
            Bounds bounds = movementAreaProp.boundsValue;
            Vector3Int gridSize = gridSizeProp.vector3IntValue;
            Vector3 platformPosition = Vector3.zero;

            if (Application.isPlaying)
            {
                platformPosition = puzzle.transform.InverseTransformPoint(puzzle.GetPlatformWorldPosition());
            }
            else
            {
                platformPosition = new Vector3(
                    restingPositionProp.vector3IntValue.x * gridSize.x,
                    restingPositionProp.vector3IntValue.y * gridSize.y,
                    restingPositionProp.vector3IntValue.z * gridSize.z
                );
            }

            float maxRadius = gridProximityRadius * Mathf.Max(gridSize.x, Mathf.Max(gridSize.y, gridSize.z));

            Handles.color = new Color(
                gridLineColor.r,
                gridLineColor.g,
                gridLineColor.b,
                gridLineOpacity
            );

            Vector3Int cellCount = new Vector3Int(
                Mathf.RoundToInt(bounds.size.x / gridSize.x),
                Mathf.RoundToInt(bounds.size.y / gridSize.y),
                Mathf.RoundToInt(bounds.size.z / gridSize.z)
            );

            // Calculate platform level in grid coordinates
            float platformLevel = platformPosition.y;

            // Draw grid lines
            for (int y = 0; y <= cellCount.y; y++)
            {
                float yPos = bounds.min.y + y * gridSize.y;

                // Skip this y-level if we're only showing platform level and this isn't it
                if (showOnlyPlatformLevel && !Mathf.Approximately(yPos, platformLevel))
                    continue;

                // Draw X lines at this Y level
                for (int x = 0; x <= cellCount.x; x++)
                {
                    float xPos = bounds.min.x + x * gridSize.x;
                    DrawGridLineWithProximityCheck(
                        new Vector3(xPos, yPos, bounds.min.z),
                        new Vector3(xPos, yPos, bounds.max.z),
                        platformPosition,
                        maxRadius
                    );
                }

                // Draw Z lines at this Y level
                for (int z = 0; z <= cellCount.z; z++)
                {
                    float zPos = bounds.min.z + z * gridSize.z;
                    DrawGridLineWithProximityCheck(
                        new Vector3(bounds.min.x, yPos, zPos),
                        new Vector3(bounds.max.x, yPos, zPos),
                        platformPosition,
                        maxRadius
                    );
                }
            }
        }

        private void DrawGridLineWithProximityCheck(Vector3 start, Vector3 end, Vector3 center, float radius)
        {
            if (!useProximityGrid)
            {
                Handles.DrawLine(start, end);
                return;
            }

            const int segments = 10;
            Vector3 segmentStep = (end - start) / segments;
            float falloffStart = radius - (gridFalloffDistance * Mathf.Max(gridSizeProp.vector3IntValue.x,
                Mathf.Max(gridSizeProp.vector3IntValue.y, gridSizeProp.vector3IntValue.z)));

            for (int i = 0; i < segments; i++)
            {
                Vector3 segStart = start + segmentStep * i;
                Vector3 segEnd = start + segmentStep * (i + 1);

                float distStart = Vector3.Distance(segStart, center);
                float distEnd = Vector3.Distance(segEnd, center);

                if (distStart <= radius || distEnd <= radius)
                {
                    // Calculate opacity based on distance
                    float opacityStart = CalculateFalloff(distStart, falloffStart, radius);
                    float opacityEnd = CalculateFalloff(distEnd, falloffStart, radius);

                    // Use the lower opacity of the two points
                    float opacity = Mathf.Min(opacityStart, opacityEnd) * gridLineOpacity;

                    Color lineColor = new Color(gridLineColor.r, gridLineColor.g, gridLineColor.b, opacity);
                    Handles.color = lineColor;
                    Handles.DrawLine(segStart, segEnd);
                }
            }
        }

        private float CalculateFalloff(float distance, float falloffStart, float maxRadius)
        {
            if (distance <= falloffStart) return 1f;
            if (distance >= maxRadius) return 0f;

            float t = (distance - falloffStart) / (maxRadius - falloffStart);
            return Mathf.Pow(1f - t, gridFalloffPower);
        }

        private void DrawCurrentCell()
        {
            Vector3Int gridSize = gridSizeProp.vector3IntValue;
            Vector3 worldPos = GridToWorldPosition(gridPosition);
            Vector3 cellMin = worldPos;
            Vector3 cellMax = cellMin + new Vector3(gridSize.x, gridSize.y, gridSize.z);
            Vector3 cellCenter = (cellMin + cellMax) * 0.5f;
            Color outlineColor = new Color(cellColor.r, cellColor.g, cellColor.b, 0.8f);
            Color fillColor = new Color(cellColor.r, cellColor.g, cellColor.b, 0.2f);
            Color gridLineColor = new Color(gridColor.r, gridColor.g, gridColor.b, 0.4f);
            DrawPlatformCell(cellMin, gridSize, fillColor, outlineColor);
            DrawPlatformHandle(cellCenter);
        }

        private void DrawPlatformHandle(Vector3 position)
        {
            if (!showPlatformHandle || Application.isPlaying || Event.current.alt)
                return;

            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.PositionHandle(position, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                UpdatePlatformPosition(SnapToGrid(newPos));
            }
        }

        private void DrawGridCoordinates(Vector3 worldPos)
        {
            if (!showGrid)
                return;
            Vector3Int gridSize = gridSizeProp.vector3IntValue;
            Vector3Int gridPos = new Vector3Int(
                Mathf.RoundToInt(worldPos.x / gridSize.x),
                Mathf.RoundToInt(worldPos.y / gridSize.y),
                Mathf.RoundToInt(worldPos.z / gridSize.z)
            );
            Handles.Label(
                worldPos + Vector3.up * 0.5f,
                $"Grid: ({gridPos.x}, {gridPos.y}, {gridPos.z})"
            );
        }

        private void DrawIdentitySection()
        {
            EditorGUILayout.PropertyField(
                puzzleIdProp,
                new GUIContent("Puzzle ID", "Unique identifier for this puzzle instance")
            );
        }

        private void DrawCommandSection()
        {
            showCommands = EditorGUILayout.Foldout(showCommands, "Commands", true);
            if (!showCommands)
                return;

            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(
                commandListProp,
                new GUIContent("Command List", "List of movement commands"),
                true
            );

            EditorGUI.indentLevel--;
        }

        private void DrawWaitDurationsSection()
        {
            showWaitDurations = EditorGUILayout.Foldout(showWaitDurations, "Wait Settings", true);
            if (!showWaitDurations)
                return;

            EditorGUI.indentLevel++;

            // Use the global waitTime property for all wait commands
            EditorGUILayout.PropertyField(
                waitTimeProp,
                new GUIContent("Default Wait Time", "Default duration for Wait commands")
            );

            EditorGUI.indentLevel--;
        }


        private bool IsRestingPositionValid()
        {
            Vector3 restingPos = restingPositionProp.vector3IntValue;
            Bounds bounds = movementAreaProp.boundsValue;
            return bounds.Contains(restingPos);
        }

        private void DrawMovementAreaHandles()
        {
            Bounds bounds = movementAreaProp.boundsValue;

            if (showBoundaryBox)
            {
                Handles.DrawWireCube(bounds.center, bounds.size);
            }

            if (Event.current.alt) return;

            if (showCornerHandles)
            {
                // Draw corner handles
                Vector3[] cornerPoints = GetBoundsCorners(bounds);
                foreach (Vector3 point in cornerPoints)
                {
                    EditorGUI.BeginChangeCheck();
                    Vector3 newPos = Handles.FreeMoveHandle(
                        point,
                        HandleUtility.GetHandleSize(point) * 0.1f,
                        Vector3.zero,
                        Handles.DotHandleCap
                    );
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(puzzle, "Resize Movement Area");
                        UpdateBoundsFromPoint(point, newPos, true);
                    }
                }
            }

            if (showEdgeHandles)
            {
                // Draw edge handles
                Vector3[] edgePoints = GetBoundsEdges(bounds);
                foreach (Vector3 point in edgePoints)
                {
                    EditorGUI.BeginChangeCheck();
                    Vector3 newPos = Handles.FreeMoveHandle(
                        point,
                        HandleUtility.GetHandleSize(point) * 0.08f,
                        Vector3.zero,
                        Handles.CircleHandleCap
                    );
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(puzzle, "Resize Movement Area");
                        UpdateBoundsFromPoint(point, newPos, false);
                    }
                }
            }
        }

        private Vector3[] GetBoundsCorners(Bounds bounds)
        {
            return new Vector3[]
            {
            bounds.min,
            bounds.max,
            new Vector3(bounds.min.x, bounds.min.y, bounds.max.z),
            new Vector3(bounds.min.x, bounds.max.y, bounds.min.z),
            new Vector3(bounds.max.x, bounds.min.y, bounds.min.z),
            new Vector3(bounds.min.x, bounds.max.y, bounds.max.z),
            new Vector3(bounds.max.x, bounds.min.y, bounds.max.z),
            new Vector3(bounds.max.x, bounds.max.y, bounds.min.z),
            };
        }

        private Vector3[] GetBoundsEdges(Bounds bounds)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            return new Vector3[]
            {
            center + new Vector3(0, extents.y, extents.z),
            center + new Vector3(0, extents.y, -extents.z),
            center + new Vector3(0, -extents.y, extents.z),
            center + new Vector3(0, -extents.y, -extents.z),
            center + new Vector3(extents.x, 0, extents.z),
            center + new Vector3(extents.x, 0, -extents.z),
            center + new Vector3(-extents.x, 0, extents.z),
            center + new Vector3(-extents.x, 0, -extents.z),
            center + new Vector3(extents.x, extents.y, 0),
            center + new Vector3(extents.x, -extents.y, 0),
            center + new Vector3(-extents.x, extents.y, 0),
            center + new Vector3(-extents.x, -extents.y, 0),
            };
        }

        private void UpdateBoundsFromPoint(Vector3 oldPos, Vector3 newPos, bool isCorner)
        {
            Bounds bounds = movementAreaProp.boundsValue;
            Vector3Int gridSize = gridSizeProp.vector3IntValue;
            newPos = SnapToGrid(newPos);
            Vector3 delta = newPos - oldPos;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            bool modifyX = Mathf.Approximately(oldPos.x, min.x) || Mathf.Approximately(oldPos.x, max.x);
            bool modifyY = Mathf.Approximately(oldPos.y, min.y) || Mathf.Approximately(oldPos.y, max.y);
            bool modifyZ = Mathf.Approximately(oldPos.z, min.z) || Mathf.Approximately(oldPos.z, max.z);
            if (!isCorner)
            {
                if (!modifyX)
                    delta.x = 0;
                if (!modifyY)
                    delta.y = 0;
                if (!modifyZ)
                    delta.z = 0;
            }
            if (Mathf.Approximately(oldPos.x, min.x))
                min.x += delta.x;
            if (Mathf.Approximately(oldPos.x, max.x))
                max.x += delta.x;
            if (Mathf.Approximately(oldPos.y, min.y))
                min.y += delta.y;
            if (Mathf.Approximately(oldPos.y, max.y))
                max.y += delta.y;
            if (Mathf.Approximately(oldPos.z, min.z))
                min.z += delta.z;
            if (Mathf.Approximately(oldPos.z, max.z))
                max.z += delta.z;
            Vector3 size = max - min;
            size.x = Mathf.Max(size.x, gridSize.x);
            size.y = Mathf.Max(size.y, gridSize.y);
            size.z = Mathf.Max(size.z, gridSize.z);
            bounds.SetMinMax(min, min + size);
            movementAreaProp.boundsValue = bounds;
            serializedObject.ApplyModifiedProperties();
            SceneView.RepaintAll();
            UpdateCellCountDisplay();
        }

        private void UpdateCellCountDisplay()
        {
            Vector3Int cellCount = GetCellCount();
            EditorGUILayout.Vector3IntField(
                new GUIContent("Current Cell Count", "Number of cells in each direction (X, Y, Z)"),
                cellCount
            );
        }

        private Vector3 SnapToGrid(Vector3 position)
        {
            Vector3Int gridSize = gridSizeProp.vector3IntValue;
            return new Vector3(
                Mathf.Round(position.x / gridSize.x) * gridSize.x,
                Mathf.Round(position.y / gridSize.y) * gridSize.y,
                Mathf.Round(position.z / gridSize.z) * gridSize.z
            );
        }

        private Vector3Int WorldToGridPosition(Vector3 worldPos)
        {
            Vector3Int gridSize = gridSizeProp.vector3IntValue;
            Vector3 reference = Application.isPlaying
                ? savedRestingPosition
                : restingPositionProp.vector3IntValue;
            return new Vector3Int(
                Mathf.RoundToInt(worldPos.x / gridSize.x) - Mathf.RoundToInt(reference.x / gridSize.x),
                Mathf.RoundToInt(worldPos.y / gridSize.y) - Mathf.RoundToInt(reference.y / gridSize.y),
                Mathf.RoundToInt(worldPos.z / gridSize.z) - Mathf.RoundToInt(reference.z / gridSize.z)
            );
        }

        private Vector3 GridToWorldPosition(Vector3Int gridPos)
        {
            Vector3Int gridSize = gridSizeProp.vector3IntValue;
            Vector3 reference = Application.isPlaying
                ? savedRestingPosition
                : restingPositionProp.vector3IntValue;
            return new Vector3(
                gridPos.x * gridSize.x + reference.x,
                gridPos.y * gridSize.y + reference.y,
                gridPos.z * gridSize.z + reference.z
            );
        }

        private void DrawGridPositionSection()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Platform Position", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            if (Application.isPlaying)
            {
                EditorGUILayout.Vector3IntField(
                    new GUIContent(
                        "Platform Grid Position",
                        "Current position relative to resting position"
                    ),
                    platformPosition
                );
                EditorGUILayout.Vector3Field(
                    new GUIContent(
                        "Resting Position (Fixed)",
                        "Static reference point (0,0,0) during gameplay"
                    ),
                    savedRestingPosition
                );
            }
            else
            {
                EditorGUILayout.Vector3Field(
                    new GUIContent(
                        "Platform/Resting Position",
                        "Position in world space (defines grid origin)"
                    ),
                    restingPositionProp.vector3IntValue
                );
                EditorGUILayout.HelpBox(
                    "In editor mode, moving the platform updates the resting position. "
                        + "During gameplay, the platform moves relative to the resting position.",
                    MessageType.Info
                );
            }
            EditorGUI.EndDisabledGroup();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    savedRestingPosition = SnapToGrid(restingPositionProp.vector3IntValue);
                    platformPosition = Vector3Int.zero;
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    restingPositionProp.vector3IntValue = Vector3Int.RoundToInt(savedRestingPosition);
                    platformPosition = Vector3Int.zero;
                    serializedObject.ApplyModifiedProperties();
                    break;
            }
        }

        private void DrawCellFaces(Vector3 min, Vector3Int size, Color faceColor)
        {
            // All positions are already in local space, and Handles.matrix is set
            Vector3[] faces = new Vector3[][]
            {
            // Bottom face
            new Vector3[]
            {
                min,
                min + Vector3.right * size.x,
                min + Vector3.right * size.x + Vector3.forward * size.z,
                min + Vector3.forward * size.z,
            },
            // Top face
            new Vector3[]
            {
                min + Vector3.up * size.y,
                min + Vector3.up * size.y + Vector3.right * size.x,
                min + Vector3.up * size.y + Vector3.right * size.x + Vector3.forward * size.z,
                min + Vector3.up * size.y + Vector3.forward * size.z,
            },
                // Front and back faces...
            }
                .SelectMany(x => x)
                .ToArray();

            for (int i = 0; i < faces.Length; i += 4)
            {
                Handles.DrawSolidRectangleWithOutline(
                    faces.Skip(i).Take(4).ToArray(),
                    faceColor,
                    Color.clear
                );
            }
        }

        private void DrawCellGridLines(Vector3 min, Vector3Int size, Color lineColor)
        {
            Handles.color = lineColor;
            float subdivisions = 4f;
            for (int axis = 0; axis < 3; axis++)
            {
                Vector3 axisDir =
                    axis == 0 ? Vector3.right : (axis == 1 ? Vector3.up : Vector3.forward);
                Vector3 perpDir1 =
                    axis == 0 ? Vector3.up : (axis == 1 ? Vector3.forward : Vector3.right);
                Vector3 perpDir2 =
                    axis == 0 ? Vector3.forward : (axis == 1 ? Vector3.right : Vector3.up);
                float axisSize = axis == 0 ? size.x : (axis == 1 ? size.y : size.z);
                float perpSize1 = axis == 0 ? size.y : (axis == 1 ? size.z : size.x);
                float perpSize2 = axis == 0 ? size.z : (axis == 1 ? size.x : size.y);
                for (float t1 = 0; t1 <= perpSize1; t1 += perpSize1 / subdivisions)
                {
                    for (float t2 = 0; t2 <= perpSize2; t2 += perpSize2 / subdivisions)
                    {
                        Vector3 start = min + perpDir1 * t1 + perpDir2 * t2;
                        Vector3 end = start + axisDir * axisSize;
                        Handles.DrawLine(start, end);
                    }
                }
            }
        }

        private void UpdatePlatformPosition(Vector3 newWorldPos)
        {
            if (!Application.isPlaying)
            {
                Undo.RecordObject(puzzle, "Move Platform");
                Vector3Int gridSize = gridSizeProp.vector3IntValue;
                Vector3Int newGridPos = new Vector3Int(
                    Mathf.RoundToInt(newWorldPos.x / gridSize.x),
                    Mathf.RoundToInt(newWorldPos.y / gridSize.y),
                    Mathf.RoundToInt(newWorldPos.z / gridSize.z)
                );
                restingPositionProp.vector3IntValue = newGridPos;
                serializedObject.ApplyModifiedProperties();

                // Use EditorApplication.delayCall instead of SendMessage
                EditorApplication.delayCall += () =>
                {
                    if (puzzle == null) return;
                    puzzle.OnValidate();
                };

                SceneView.RepaintAll();
            }
        }

        private void OnDisable()
        {
            if (puzzle != null)
            {
                puzzle.OnStateChanged -= OnPlatformStateChanged;
            }

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            SaveVisualizationPrefs();
        }

        private void DrawMovementSettingsSection()
        {
            showMovementSettings = EditorGUILayout.Foldout(showMovementSettings, "Movement Settings", true);
            if (!showMovementSettings) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Draw the ScriptableObject field
            EditorGUILayout.PropertyField(
                movementSettingsProp,
                new GUIContent("Movement Settings Asset", "Platform movement configuration asset")
            );

            if (movementSettingsProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "Movement Settings asset is required. Please assign or create one.",
                    MessageType.Warning
                );

                if (GUILayout.Button("Create New Settings Asset"))
                {
                    CreateNewMovementSettingsAsset();
                }
            }
            else
            {
                EditorGUILayout.Space(5);
                var settings = (PlatformMovementSettings)movementSettingsProp.objectReferenceValue;
                var serializedSettings = new SerializedObject(settings);
                bool wasEnabled = GUI.enabled;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUI.BeginChangeCheck();
                syncMovementSettings = EditorGUILayout.ToggleLeft(
                    new GUIContent("Sync with Asset", "Keep these values in sync with the asset"),
                    syncMovementSettings
                );
                if (EditorGUI.EndChangeCheck())
                {
                    if (syncMovementSettings)
                    {
                        // When switching to sync, ask if we want to save independent changes
                        if (EditorUtility.DisplayDialog("Save Changes?",
                            "Do you want to save your independent changes to the asset?",
                            "Save to Asset", "Discard Changes"))
                        {
                            SaveToAsset(settings);
                        }
                    }
                    else
                    {
                        // When switching to independent, copy current values
                        var independentProp = serializedObject.FindProperty("independentSettings");
                        if (independentProp != null)
                        {
                            Undo.RecordObject(target, "Switch to Independent Settings");
                            CopySettingsToIndependent(settings, independentProp);
                            serializedObject.ApplyModifiedProperties();
                        }
                    }
                    EditorPrefs.SetBool("PuzzleInstance_SyncMovementSettings", syncMovementSettings);
                }

                GUI.enabled = !syncMovementSettings;

                // Draw properties from either the asset or independent settings
                if (syncMovementSettings)
                {
                    DrawMovementSettingsProperties(serializedSettings);
                }
                else
                {
                    var independentProp = serializedObject.FindProperty("independentSettings");
                    if (independentProp != null)
                    {
                        DrawMovementSettingsProperties(serializedObject, independentProp);
                    }
                }

                GUI.enabled = wasEnabled;

                // Add buttons for managing settings
                EditorGUILayout.BeginHorizontal();
                if (!syncMovementSettings)
                {
                    if (GUILayout.Button("Save to Asset"))
                    {
                        SaveToAsset(settings);
                    }
                    if (GUILayout.Button("Revert to Asset"))
                    {
                        RevertToAsset(settings);
                    }
                }
                if (GUILayout.Button("Reset to Defaults"))
                {
                    ResetToAssetDefaults(settings);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
        }

        private void DrawMovementSettingsProperties(SerializedObject obj, SerializedProperty rootProp = null)
        {
            SerializedProperty prop = rootProp != null ? rootProp.Copy() : obj.GetIterator();
            bool enterChildren = true;

            // Get the movement settings property directly
            SerializedProperty movementSettingsProp = obj.FindProperty("movementSettings");
            
            if (movementSettingsProp != null && movementSettingsProp.objectReferenceValue is PlatformMovementSettings movementSettings)
            {
                // Create a new SerializedObject for the movement settings
                var movementSettingsSerialized = new SerializedObject(movementSettings);

                // Iterate through the serialized properties of the movement settings
                SerializedProperty movementProp = movementSettingsSerialized.GetIterator();
                while (movementProp.NextVisible(enterChildren))
                {
                    enterChildren = false;

                    // Skip the script reference
                    if (movementProp.name == "m_Script") continue;

                    // Draw the property
                    EditorGUILayout.PropertyField(movementProp, true);
                }

                // Apply modified properties if any changes were made
                if (movementSettingsSerialized.hasModifiedProperties)
                {
                    movementSettingsSerialized.ApplyModifiedProperties();
                }
            }
            else
            {
                Debug.LogError("The movementSettings property is either null or not a valid PlatformMovementSettings reference.");
            }
        }

        private void SaveToAsset(PlatformMovementSettings settings)
        {
            var independentProp = serializedObject.FindProperty("independentSettings");
            if (independentProp != null)
            {
                Undo.RecordObject(settings, "Save Movement Settings to Asset");
                CopyIndependentToAsset(independentProp, settings);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }

        private void RevertToAsset(PlatformMovementSettings settings)
        {
            var independentProp = serializedObject.FindProperty("independentSettings");
            if (independentProp != null)
            {
                Undo.RecordObject(target, "Revert Movement Settings");
                CopySettingsToIndependent(settings, independentProp);
                serializedObject.ApplyModifiedProperties();
            }
        }

        private void CreateNewMovementSettingsAsset()
        {
            var asset = ScriptableObject.CreateInstance<PlatformMovementSettings>();
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Movement Settings",
                "PlatformMovementSettings",
                "asset",
                "Please enter a file name to save the movement settings to"
            );

            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
                movementSettingsProp.objectReferenceValue = asset;
                serializedObject.ApplyModifiedProperties();
            }
        }

        private void ResetToAssetDefaults(PlatformMovementSettings settings)
        {
            Undo.RecordObject(settings, "Reset Movement Settings");
            var defaultSettings = ScriptableObject.CreateInstance<PlatformMovementSettings>();
            settings.moveSpeed = defaultSettings.moveSpeed;
            settings.movementCurve = new AnimationCurve(defaultSettings.movementCurve.keys);
            settings.enableContinuousMovement = defaultSettings.enableContinuousMovement;
            settings.accelerationTime = defaultSettings.accelerationTime;
            settings.decelerationTime = defaultSettings.decelerationTime;
            DestroyImmediate(defaultSettings);
            EditorUtility.SetDirty(settings);
        }

        private void OnEditorUpdate()
        {
            if (puzzle != null && !EditorApplication.isPlaying)
            {
                SceneView.RepaintAll();
            }
        }

        private void SaveEditorPrefs()
        {
            var id = target.GetInstanceID();
            EditorPrefs.SetBool($"PuzzleInstance_{id}_ShowGrid", showGrid);
            EditorPrefs.SetBool($"PuzzleInstance_{id}_ShowProximityGrid", showProximityGrid);
            EditorPrefs.SetInt($"PuzzleInstance_{id}_ProximityRange", proximityRange);
            EditorPrefs.SetFloat($"PuzzleInstance_{id}_GridLineOpacity", gridLineOpacity);

            // Save color components
            EditorPrefs.SetFloat($"PuzzleInstance_{id}_GridColorR", gridLineColor.r);
            EditorPrefs.SetFloat($"PuzzleInstance_{id}_GridColorG", gridLineColor.g);
            EditorPrefs.SetFloat($"PuzzleInstance_{id}_GridColorB", gridLineColor.b);
            EditorPrefs.SetFloat($"PuzzleInstance_{id}_GridColorA", gridLineColor.a);
        }

        private void LoadEditorPrefs()
        {
            var id = target.GetInstanceID();
            showProximityGrid = EditorPrefs.GetBool($"PuzzleInstance_{id}_ShowProximityGrid", false);
            proximityRange = EditorPrefs.GetInt($"PuzzleInstance_{id}_ProximityRange", 3);
            showOnlyPlatformLevel = EditorPrefs.GetBool("PuzzleInstance_ShowOnlyPlatformLevel", false);
        }

        private void DrawFaceHandle(Vector3 position, Vector3 direction, float size, Color color, bool isPositiveDirection)
        {
            Color oldColor = Handles.color;
            Handles.color = new Color(color.r, color.g, color.b, 0.8f);

            // Only draw handle if not navigating camera
            if (!Event.current.alt)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.Slider(position, direction);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(puzzle, "Resize Movement Area");
                    Bounds bounds = movementAreaProp.boundsValue;

                    float movement = Vector3.Distance(newPos, position);
                    if (Vector3.Dot(newPos - position, direction) < 0)
                        movement = -movement;
                    if (!isPositiveDirection)
                        movement = -movement;

                    Vector3Int gridSize = gridSizeProp.vector3IntValue;
                    movement = Mathf.Round(movement / gridSize.x) * gridSize.x;

                    if (Mathf.Abs(direction.x) > 0.1f)
                    {
                        if (isPositiveDirection)
                            bounds.max = new Vector3(position.x + movement, bounds.max.y, bounds.max.z);
                        else
                            bounds.min = new Vector3(position.x + movement, bounds.min.y, bounds.min.z);
                    }
                    else if (Mathf.Abs(direction.y) > 0.1f)
                    {
                        if (isPositiveDirection)
                            bounds.max = new Vector3(bounds.max.x, position.y + movement, bounds.max.z);
                        else
                            bounds.min = new Vector3(bounds.min.x, position.y + movement, bounds.min.z);
                    }
                    else
                    {
                        if (isPositiveDirection)
                            bounds.max = new Vector3(bounds.max.x, bounds.max.y, position.z + movement);
                        else
                            bounds.min = new Vector3(bounds.min.x, bounds.min.y, position.z + movement);
                    }

                    movementAreaProp.boundsValue = bounds;
                    serializedObject.ApplyModifiedProperties();
                    SceneView.RepaintAll();
                }
            }

            Handles.color = oldColor;
        }

        private void DrawFaceHandles()
        {
            if (!showFaceHandles) return;
            Bounds bounds = movementAreaProp.boundsValue;
            Vector3 center = bounds.center;
            Vector3 size = bounds.size;
            float handleSize = HandleUtility.GetHandleSize(center) * 0.15f;

            // Draw face handles
            DrawFaceHandle(center + Vector3.right * (size.x / 2), Vector3.right, handleSize, Color.red, true);    // Right face
            DrawFaceHandle(center - Vector3.right * (size.x / 2), Vector3.left, handleSize, Color.red, false);    // Left face

            DrawFaceHandle(center + Vector3.up * (size.y / 2), Vector3.up, handleSize, Color.green, true);        // Top face
            DrawFaceHandle(center - Vector3.up * (size.y / 2), Vector3.down, handleSize, Color.green, false);     // Bottom face

            DrawFaceHandle(center + Vector3.forward * (size.z / 2), Vector3.forward, handleSize, Color.blue, true);  // Front face
            DrawFaceHandle(center - Vector3.forward * (size.z / 2), Vector3.back, handleSize, Color.blue, false);    // Back face
        }
        private float GetAxisSize(Vector3 direction, Vector3Int gridSize)
        {
            if (direction.x != 0) return gridSize.x;
            if (direction.y != 0) return gridSize.y;
            return gridSize.z;
        }

        private void LoadVisualizationPrefs()
        {
            // Load all shared settings
            showVisualizationSettings = EditorPrefs.GetBool("PuzzleInstance_ShowVisualizationSettings", true);
            showBoundaryBox = EditorPrefs.GetBool("PuzzleInstance_ShowBoundaryBox", true);
            showFaceHandles = EditorPrefs.GetBool("PuzzleInstance_ShowFaceHandles", true);
            showEdgeHandles = EditorPrefs.GetBool("PuzzleInstance_ShowEdgeHandles", true);
            showCornerHandles = EditorPrefs.GetBool("PuzzleInstance_ShowCornerHandles", true);
            showPlatformHandle = EditorPrefs.GetBool("PuzzleInstance_ShowPlatformHandle", true);
            showGrid = EditorPrefs.GetBool("PuzzleInstance_ShowGrid", true);
            showOnlyPlatformLevel = EditorPrefs.GetBool("PuzzleInstance_ShowOnlyPlatformLevel", false);
            useProximityGrid = EditorPrefs.GetBool("PuzzleInstance_UseProximityGrid", false);
            gridProximityRadius = EditorPrefs.GetFloat("PuzzleInstance_GridProximityRadius", 3f);
            gridFalloffDistance = EditorPrefs.GetFloat("PuzzleInstance_GridFalloffDistance", 2f);
            gridFalloffPower = EditorPrefs.GetFloat("PuzzleInstance_GridFalloffPower", 1f);
            gridLineOpacity = EditorPrefs.GetFloat("PuzzleInstance_GridLineOpacity", 0.2f);

            // Load grid color
            gridLineColor = new Color(
                EditorPrefs.GetFloat("PuzzleInstance_GridColorR", 0.5f),
                EditorPrefs.GetFloat("PuzzleInstance_GridColorG", 0.5f),
                EditorPrefs.GetFloat("PuzzleInstance_GridColorB", 0.5f),
                EditorPrefs.GetFloat("PuzzleInstance_GridColorA", 0.2f)
            );

            showObstacles = EditorPrefs.GetBool("PuzzleInstance_ShowObstacles", true);
            obstacleColor = new Color(
                EditorPrefs.GetFloat("PuzzleInstance_ObstacleColorR", 1f),
                EditorPrefs.GetFloat("PuzzleInstance_ObstacleColorG", 0f),
                EditorPrefs.GetFloat("PuzzleInstance_ObstacleColorB", 0f),
                EditorPrefs.GetFloat("PuzzleInstance_ObstacleColorA", 0.3f)
            );

            // Don't override persistent visualization from prefs if it's already set in the object
            if (puzzle != null && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                bool savedValue = EditorPrefs.GetBool($"PuzzleInstance_{puzzle.GetInstanceID()}_PersistentVisualization", false);
                if (persistentVisualizationProp != null && persistentVisualizationProp.boolValue != savedValue)
                {
                    persistentVisualizationProp.boolValue = savedValue;
                    serializedObject.ApplyModifiedProperties();
                }
            }
        }

        private void SaveVisualizationPrefs()
        {
            // Save shared settings (these affect all instances)
            EditorPrefs.SetBool("PuzzleInstance_ShowVisualizationSettings", showVisualizationSettings);
            EditorPrefs.SetBool("PuzzleInstance_ShowBoundaryBox", showBoundaryBox);
            EditorPrefs.SetBool("PuzzleInstance_ShowFaceHandles", showFaceHandles);
            EditorPrefs.SetBool("PuzzleInstance_ShowEdgeHandles", showEdgeHandles);
            EditorPrefs.SetBool("PuzzleInstance_ShowCornerHandles", showCornerHandles);
            EditorPrefs.SetBool("PuzzleInstance_ShowPlatformHandle", showPlatformHandle);
            EditorPrefs.SetBool("PuzzleInstance_ShowGrid", showGrid);
            EditorPrefs.SetBool("PuzzleInstance_ShowOnlyPlatformLevel", showOnlyPlatformLevel);
            EditorPrefs.SetBool("PuzzleInstance_UseProximityGrid", useProximityGrid);
            EditorPrefs.SetFloat("PuzzleInstance_GridProximityRadius", gridProximityRadius);
            EditorPrefs.SetFloat("PuzzleInstance_GridFalloffDistance", gridFalloffDistance);
            EditorPrefs.SetFloat("PuzzleInstance_GridFalloffPower", gridFalloffPower);
            EditorPrefs.SetFloat("PuzzleInstance_GridLineOpacity", gridLineOpacity);

            // Save grid color
            EditorPrefs.SetFloat("PuzzleInstance_GridColorR", gridLineColor.r);
            EditorPrefs.SetFloat("PuzzleInstance_GridColorG", gridLineColor.g);
            EditorPrefs.SetFloat("PuzzleInstance_GridColorB", gridLineColor.b);
            EditorPrefs.SetFloat("PuzzleInstance_GridColorA", gridLineColor.a);

            // Force all editors to refresh
            var editors = Resources.FindObjectsOfTypeAll<PuzzleInstanceEditor>();
            foreach (var editor in editors)
            {
                if (editor != this)
                {
                    editor.LoadVisualizationPrefs();
                    editor.Repaint();
                }
            }
            SceneView.RepaintAll();

            EditorPrefs.SetBool("PuzzleInstance_ShowObstacles", showObstacles);
            EditorPrefs.SetFloat("PuzzleInstance_ObstacleColorR", obstacleColor.r);
            EditorPrefs.SetFloat("PuzzleInstance_ObstacleColorG", obstacleColor.g);
            EditorPrefs.SetFloat("PuzzleInstance_ObstacleColorB", obstacleColor.b);
            EditorPrefs.SetFloat("PuzzleInstance_ObstacleColorA", obstacleColor.a);

            // Save persistent visualization per instance
            if (puzzle != null)
            {
                EditorPrefs.SetBool(
                    $"PuzzleInstance_{puzzle.GetInstanceID()}_PersistentVisualization",
                    persistentVisualizationProp.boolValue
                );
            }
        }

        private void OnPlatformStateChanged(PlatformState newState)
        {
            // Force repaint when state changes
            Repaint();
            SceneView.RepaintAll();
        }

        private void CopySettingsToIndependent(PlatformMovementSettings source, SerializedProperty independentProp)
        {
            independentProp.FindPropertyRelative("moveSpeed").floatValue = source.moveSpeed;
            independentProp.FindPropertyRelative("movementCurve").animationCurveValue = new AnimationCurve(source.movementCurve.keys);
            independentProp.FindPropertyRelative("enableContinuousMovement").boolValue = source.enableContinuousMovement;
            independentProp.FindPropertyRelative("accelerationTime").floatValue = source.accelerationTime;
            independentProp.FindPropertyRelative("decelerationTime").floatValue = source.decelerationTime;
        }

        private void CopyIndependentToAsset(SerializedProperty independentProp, PlatformMovementSettings target)
        {
            target.moveSpeed = independentProp.FindPropertyRelative("moveSpeed").floatValue;
            target.movementCurve = new AnimationCurve(independentProp.FindPropertyRelative("movementCurve").animationCurveValue.keys);
            target.enableContinuousMovement = independentProp.FindPropertyRelative("enableContinuousMovement").boolValue;
            target.accelerationTime = independentProp.FindPropertyRelative("accelerationTime").floatValue;
            target.decelerationTime = independentProp.FindPropertyRelative("decelerationTime").floatValue;
        }

        private void DrawObstacles()
        {
            if (!showObstacles || puzzle == null) return;

            var obstacles = puzzle.GetComponentsInChildren<ObstacleComponent>();
            foreach (var obstacle in obstacles)
            {
                if (!obstacle.enabled) continue;

                foreach (var cell in obstacle.OccupiedCells)
                {
                    Vector3Int gridSize = puzzle.gridSize;
                    Vector3 cellPos = new Vector3(
                        cell.x * gridSize.x,
                        cell.y * gridSize.y,
                        cell.z * gridSize.z
                    );

                    Color fillColor = new Color(obstacleColor.r, obstacleColor.g, obstacleColor.b, obstacleColor.a * 0.5f);
                    Color outlineColor = new Color(obstacleColor.r, obstacleColor.g, obstacleColor.b, obstacleColor.a);

                    DrawCellFaces(cellPos, gridSize, fillColor);
                    Handles.color = outlineColor;
                    Handles.DrawWireCube(cellPos + (Vector3)gridSize * 0.5f, gridSize);
                }
            }
        }
    }
}