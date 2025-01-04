using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PuzzlePlatform.Core
{
    [RequireComponent(typeof(Collider))]
    [ExecuteInEditMode]
    public class ObstacleComponent : MonoBehaviour
    {
        #region Fields
        [SerializeField] private bool isStatic = true;
        [SerializeField] private Color gizmoColor = new Color(1f, 0f, 0f, 0.5f);
        [SerializeField] private bool showGizmos = true;

        private Collider obstacleCollider;
        private PuzzleInstance parentPuzzle;
        private HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();
        #endregion

        #region Properties
        public bool IsStatic => isStatic;
        public HashSet<Vector3Int> OccupiedCells => occupiedCells;
        #endregion

        #region Unity Methods
        private void Awake()
        {
            FindParentPuzzle();
            obstacleCollider = GetComponent<Collider>();
            if (obstacleCollider == null)
            {
                Debug.LogWarning($"ObstacleComponent on {gameObject.name} does not have a collider! Adding a BoxCollider.", this);
                obstacleCollider = gameObject.AddComponent<BoxCollider>();
            }
            if (isStatic)
            {
                CalculateOccupiedCells();
            }
        }

        private void OnEnable()
        {
            CalculateOccupiedCells();
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void Update()
        {
            if (!isStatic)
            {
                CalculateOccupiedCells();
                UpdateVisualization();
            }
        }
        #endregion

        #region Editor Methods
#if UNITY_EDITOR
        private void OnEditorUpdate()
        {
            if (!Application.isPlaying)
            {
                CalculateOccupiedCells();
                UpdateVisualization();
            }
        }

        private void OnDrawGizmos()
        {
            if (showGizmos)
            {
                DrawGizmos();
            }
        }

        private void OnValidate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isUpdating)
                return;

            FindParentPuzzle();
            obstacleCollider = GetComponent<Collider>();
            if (obstacleCollider == null)
            {
                Debug.LogWarning($"ObstacleComponent on {gameObject.name} does not have a collider! Adding a BoxCollider.", this);
                obstacleCollider = gameObject.AddComponent<BoxCollider>();
            }
            UpdateVisualization();
        }
#endif
        #endregion

        #region Calculation Methods
        public void CalculateOccupiedCells()
        {
            if (parentPuzzle == null) return;

            occupiedCells.Clear();
            Bounds colliderBounds = obstacleCollider.bounds;

            Vector3 localMin = parentPuzzle.transform.InverseTransformPoint(colliderBounds.min);
            Vector3 localMax = parentPuzzle.transform.InverseTransformPoint(colliderBounds.max);
            Vector3Int gridSize = parentPuzzle.gridSize;

            int minX = Mathf.FloorToInt(localMin.x / gridSize.x);
            int maxX = Mathf.CeilToInt(localMax.x / gridSize.x);
            int minY = Mathf.FloorToInt(localMin.y / gridSize.y);
            int maxY = Mathf.CeilToInt(localMax.y / gridSize.y);
            int minZ = Mathf.FloorToInt(localMin.z / gridSize.z);
            int maxZ = Mathf.CeilToInt(localMax.z / gridSize.z);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        Vector3Int cellPos = new Vector3Int(x, y, z);
                        if (DoesCellIntersectCollider(cellPos))
                        {
                            occupiedCells.Add(cellPos);
                        }
                    }
                }
            }

            parentPuzzle.OnObstacleUpdated(this);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }
#endif
        }

        private bool DoesCellIntersectCollider(Vector3Int cellPos)
        {
            if (parentPuzzle == null) return false;

            Vector3Int gridSize = parentPuzzle.gridSize;
            Vector3 cellWorldMin = parentPuzzle.transform.TransformPoint(new Vector3(
                cellPos.x * gridSize.x,
                cellPos.y * gridSize.y,
                cellPos.z * gridSize.z
            ));
            Vector3 cellWorldMax = parentPuzzle.transform.TransformPoint(new Vector3(
                (cellPos.x + 1) * gridSize.x,
                (cellPos.y + 1) * gridSize.y,
                (cellPos.z + 1) * gridSize.z
            ));

            Bounds cellBounds = new Bounds(
                (cellWorldMin + cellWorldMax) * 0.5f,
                cellWorldMax - cellWorldMin
            );

            if (obstacleCollider is BoxCollider)
            {
                return cellBounds.Intersects(obstacleCollider.bounds);
            }

            if (obstacleCollider is SphereCollider || obstacleCollider is CapsuleCollider)
            {
                Vector3 direction;
                float distance;

                GameObject tempGO = new GameObject("TempCollider");
                tempGO.transform.position = cellBounds.center;
                tempGO.transform.rotation = parentPuzzle.transform.rotation;
                BoxCollider tempCollider = tempGO.AddComponent<BoxCollider>();
                tempCollider.size = cellBounds.size;

                bool intersects = Physics.ComputePenetration(
                    tempCollider, tempGO.transform.position, tempGO.transform.rotation,
                    obstacleCollider, transform.position, transform.rotation,
                    out direction, out distance
                );

                DestroyImmediate(tempGO);
                return intersects;
            }

            if (obstacleCollider is MeshCollider)
            {
                return cellBounds.Intersects(obstacleCollider.bounds);
            }

            return false;
        }
        #endregion

        #region Gizmo Methods
        private void DrawGizmos()
        {
            if (parentPuzzle == null) return;

            Bounds colliderBounds = obstacleCollider.bounds;

            Gizmos.color = gizmoColor;
            Gizmos.DrawWireCube(colliderBounds.center, colliderBounds.size);

            foreach (Vector3Int cell in occupiedCells)
            {
                Vector3 cellWorldPos = parentPuzzle.transform.TransformPoint(new Vector3(
                    cell.x * parentPuzzle.gridSize.x,
                    cell.y * parentPuzzle.gridSize.y,
                    cell.z * parentPuzzle.gridSize.z
                ));

                Vector3 cellSize = parentPuzzle.transform.TransformVector(parentPuzzle.gridSize);

                Color fillColor = gizmoColor;
                fillColor.a *= 0.3f;
                Gizmos.color = fillColor;
                Gizmos.DrawCube(cellWorldPos + cellSize * 0.5f, cellSize);

                Gizmos.color = gizmoColor;
                Gizmos.DrawWireCube(cellWorldPos + cellSize * 0.5f, cellSize);
            }
            SceneView.RepaintAll();
        }

        private void UpdateGizmoVisualization()
        {
            DrawGizmos();
        }
        #endregion

        #region Helper Methods
        private void FindParentPuzzle()
        {
            Transform current = transform;
            while (current != null)
            {
                parentPuzzle = current.GetComponent<PuzzleInstance>();
                if (parentPuzzle != null)
                {
                    break;
                }
                current = current.parent;
            }

            if (parentPuzzle == null)
            {
                Debug.LogWarning($"ObstacleComponent on {gameObject.name} could not find a parent PuzzleInstance!", this);
            }
        }

        public void UpdateVisualization()
        {
            CalculateOccupiedCells();
            SceneView.RepaintAll();
        }
        #endregion
    }
}