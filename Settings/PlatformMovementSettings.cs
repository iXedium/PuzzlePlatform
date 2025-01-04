using UnityEngine;

namespace PuzzlePlatform
{
    [CreateAssetMenu(fileName = "PlatformMovementSettings", menuName = "Puzzle Platform/Movement Settings")]
    public class PlatformMovementSettings : ScriptableObject
    {
        [Tooltip("Units per second the platform moves")]
        public float moveSpeed = 5f;

        [Tooltip("Animation curve for movement easing")]
        public AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Tooltip("Combine consecutive movements in the same direction")]
        public bool enableContinuousMovement = true;

        [Tooltip("Time taken to reach full speed (as fraction of total movement)")]
        [Range(0f, 1f)] 
        public float accelerationTime = 0.2f;

        [Tooltip("Time taken to stop (as fraction of total movement)")]
        [Range(0f, 1f)] 
        public float decelerationTime = 0.2f;

        private void OnValidate()
        {
            // Ensure minimum speed
            moveSpeed = Mathf.Max(0.1f, moveSpeed);

            // Ensure acceleration + deceleration don't exceed total movement time
            if (accelerationTime + decelerationTime > 1f)
            {
                float total = accelerationTime + decelerationTime;
                float scale = 1f / total;
                accelerationTime *= scale;
                decelerationTime *= scale;
            }
        }
    }
} 