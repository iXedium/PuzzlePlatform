using UnityEngine;

namespace PuzzlePlatform.Core
{
    [System.Serializable]
    public class PlatformVisualizationSettings
    {
        [Header("Grid Settings")]
        public bool showGrid = true;
        public bool showOnlyPlatformLevel = false;
        public bool useProximityGrid = false;
        public float gridProximityRadius = 3f;
        public float gridFalloffDistance = 2f;
        public float gridFalloffPower = 1f;
        public float gridLineOpacity = 0.2f;
        public Color gridLineColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);

        [Header("Boundary Settings")]
        public bool showBoundaryBox = true;
        public bool showFaceHandles = true;
        public bool showEdgeHandles = true;
        public bool showCornerHandles = true;
        public bool showPlatformHandle = true;

        [Header("Obstacle Settings")]
        public bool showObstacles = true;
        public Color obstacleColor = new Color(1f, 0f, 0f, 0.3f);

        public void CopyFrom(PlatformVisualizationSettings other)
        {
            if (other == null) return;

            showGrid = other.showGrid;
            showOnlyPlatformLevel = other.showOnlyPlatformLevel;
            useProximityGrid = other.useProximityGrid;
            gridProximityRadius = other.gridProximityRadius;
            gridFalloffDistance = other.gridFalloffDistance;
            gridFalloffPower = other.gridFalloffPower;
            gridLineOpacity = other.gridLineOpacity;
            gridLineColor = other.gridLineColor;

            showBoundaryBox = other.showBoundaryBox;
            showFaceHandles = other.showFaceHandles;
            showEdgeHandles = other.showEdgeHandles;
            showCornerHandles = other.showCornerHandles;
            showPlatformHandle = other.showPlatformHandle;

            showObstacles = other.showObstacles;
            obstacleColor = other.obstacleColor;
        }
    }
} 