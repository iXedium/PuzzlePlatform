using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using PuzzlePlatform.Core;

namespace PuzzlePlatform.Core
{
    public enum PlatformState
    {
        Idle,
        Moving,
        Waiting,
        ExecutingCommands,
        Reversing
    }

    public class PuzzleInstance : MonoBehaviour
    {
        public Vector3Int gridSize;
        public Bounds movementArea;
        public Vector3Int restingPosition;
        public List<MovementCommand> commandList = new List<MovementCommand>();
        public float waitTime = 1f;
        public string puzzleId;

        [SerializeField] private GameObject platformObject;
        public GameObject PlatformObject => platformObject;
        private const string PLATFORM_CHILD_NAME = "Platform";

        private int currentCommandIndex = -1;
        public int CurrentCommandIndex
        {
            get => currentCommandIndex;
            private set
            {
                currentCommandIndex = value;
            }
        }

        private Dictionary<int, float> waitDurations = new Dictionary<int, float>();
        private Vector3 platformPosition;

        [System.Serializable]
        private class IndependentMovementSettings
        {
            public float moveSpeed = 5f;
            public AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            public bool enableContinuousMovement = true;
            public float accelerationTime = 0.2f;
            public float decelerationTime = 0.2f;

            public void CopyFrom(PlatformMovementSettings source)
            {
                moveSpeed = source.moveSpeed;
                movementCurve = new AnimationCurve(source.movementCurve.keys);
                enableContinuousMovement = source.enableContinuousMovement;
                accelerationTime = source.accelerationTime;
                decelerationTime = source.decelerationTime;
            }
        }

        [SerializeField] private IndependentMovementSettings independentSettings;

        [SerializeField] private PlatformMovementSettings movementSettings;
        private bool isMoving = false;
        private Vector3 moveStartPosition;
        private Vector3 moveTargetPosition;
        private float moveProgress = 0f;
        private List<Vector3> pathPoints = new List<Vector3>();

        private bool isWaiting = false;
        private bool DEBUG_MODE = false; // Set to true to enable debug logs

        private bool isExecuting = false;
        public bool IsExecuting => isExecuting;

        private PlatformState currentState = PlatformState.Idle;
        public PlatformState CurrentState => currentState;

        public event System.Action<PlatformState> OnStateChanged;

        private void SetState(PlatformState newState)
        {
            if (currentState != newState)
            {
                currentState = newState;
                OnStateChanged?.Invoke(currentState);
            }
        }

        public enum MovementCommand
        {
            Idle,
            Up,
            Down,
            Left,
            Right,
            Forward,
            Backward,
            Wait,
        }

        [SerializeField] private PlatformVisualizationSettings visualizationSettings = new PlatformVisualizationSettings();
        public PlatformVisualizationSettings VisualizationSettings => visualizationSettings;

        [SerializeField] private bool persistentVisualization = false;
        public bool PersistentVisualization
        {
            get => persistentVisualization;
            set
            {
                if (persistentVisualization != value)
                {
                    persistentVisualization = value;
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(this);
                    UnityEditor.SceneView.RepaintAll();
#endif
                }
            }
        }

        private Vector3 initialPosition;
        private bool isInitialMove = true;

        private bool isPlaying = false;
        public bool IsPlaying => isPlaying;

        [SerializeField] public bool showBoundaryInGame = true;

        void Start()
        {
            platformPosition = new Vector3(
                restingPosition.x * gridSize.x,
                restingPosition.y * gridSize.y,
                restingPosition.z * gridSize.z
            );
            initialPosition = platformPosition;

            EnsurePlatformExists();
            UpdatePlatformTransform();

            ValidateCommandList();
        }

        private void EnsurePlatformExists()
        {
            if (platformObject == null)
            {
                // Check if child already exists
                Transform existingPlatform = transform.Find(PLATFORM_CHILD_NAME);
                if (existingPlatform != null)
                {
                    platformObject = existingPlatform.gameObject;
                }
                else if (!Application.isPlaying && !Application.isEditor)
                {
                    // Don't create during validation
                    return;
                }
                else
                {
                    // Create new platform child
                    var newPlatform = new GameObject(PLATFORM_CHILD_NAME);
                    newPlatform.transform.parent = transform;
                    newPlatform.transform.localPosition = Vector3.zero;
                    platformObject = newPlatform;
                }
            }
        }

        private void UpdatePlatformTransform()
        {
            if (platformObject != null && !EditorApplication.isUpdating)
            {
                platformObject.transform.localPosition = platformPosition;
            }
        }

        private void OnEnable()
        {
            EnsurePlatformExists();
            UpdatePlatformTransform();
        }

        private void ValidateCommandList()
        {
            if (commandList.Count == 0)
            {
                Debug.LogWarning("Command list is empty. Please add commands.");
            }
        }

        public void ExecuteCommands()
        {
            if (commandList.Count == 0)
            {
                Debug.LogWarning("Command list is empty. Please add commands.");
                return;
            }
            if (isPlaying)
            {
                Debug.LogWarning("Platform is already executing commands.");
                return;
            }
            isPlaying = true;
            StartCoroutine(ExecuteCommandSequence());
        }

        private IEnumerator ExecuteCommandSequence()
        {
            SetState(PlatformState.ExecutingCommands);
            DebugLog("Starting command sequence");
            CurrentCommandIndex = -1;
            yield return null;
            CurrentCommandIndex = 0;
            startingPosition = platformPosition;
            executedCommands.Clear();
            isInitialMove = true;

            while (currentCommandIndex < commandList.Count)
            {
                while (currentState == PlatformState.Moving)
                {
                    yield return null;
                }

                MovementCommand command = commandList[currentCommandIndex];
                DebugLog($"Processing command {currentCommandIndex}: {command}");

                if (command == MovementCommand.Wait)
                {
                    DebugLog($"Wait command started. Duration: {waitTime}s");
                    SetState(PlatformState.Waiting);
                    yield return new WaitForSeconds(waitTime);
                    SetState(PlatformState.ExecutingCommands);
                    DebugLog("Wait command completed");
                    executedCommands.Push(command);
                    currentCommandIndex++;
                    continue;
                }

                Vector3 direction = GetDirectionFromCommand(command);
                if (direction == Vector3.zero)
                {
                    currentCommandIndex++;
                    continue;
                }

                // Calculate movement path with continuous movement
                List<Vector3> movementPath = new List<Vector3> { platformPosition };
                List<MovementCommand> pathCommands = new List<MovementCommand>();
                int consecutiveSteps = 0;
                int lookAheadIndex = currentCommandIndex;
                bool hitObstacle = false;

                // Validate entire path before moving
                while (lookAheadIndex < commandList.Count && movementSettings.enableContinuousMovement)
                {
                    MovementCommand nextCmd = commandList[lookAheadIndex];
                    if (nextCmd == MovementCommand.Wait) break;

                    Vector3 nextDir = GetDirectionFromCommand(nextCmd);
                    if (nextDir != direction) break;

                    Vector3 nextPos = movementPath[^1] + Vector3.Scale(direction, (Vector3)gridSize);
                    Vector3Int nextCell = WorldToGridCell(transform.TransformPoint(nextPos));

                    // Check if next position is valid
                    if (!movementArea.Contains(nextPos) || IsCellOccupied(nextCell))
                    {
                        hitObstacle = IsCellOccupied(nextCell);
                        break;
                    }

                    movementPath.Add(nextPos);
                    pathCommands.Add(nextCmd);
                    consecutiveSteps++;
                    lookAheadIndex++;
                }

                // If no continuous movement or only one step
                if (consecutiveSteps == 0)
                {
                    Vector3 nextPos = platformPosition + Vector3.Scale(direction, (Vector3)gridSize);
                    Vector3Int nextCell = WorldToGridCell(transform.TransformPoint(nextPos));

                    if (IsCellOccupied(nextCell))
                    {
                        DebugLog($"Obstacle detected at {nextCell}, starting reverse sequence");
                        OnObstacleCollision?.Invoke(nextCell);
                        if (isInitialMove)
                        {
                            DebugLog("Obstacle detected on first move, immediate reverse");
                            SetState(PlatformState.Idle);
                            isPlaying = false;
                            yield break;
                        }
                        StartReverseSequence();
                        yield break;
                    }

                    if (movementArea.Contains(nextPos))
                    {
                        movementPath.Add(nextPos);
                        pathCommands.Add(command);
                    }
                    else
                    {
                        DebugLog($"Cannot move to position {nextPos} - starting reverse sequence");
                        StartReverseSequence();
                        yield break;
                    }
                }

                // Execute movement if path is valid
                if (movementPath.Count > 1)
                {
                    DebugLog($"Starting movement over {movementPath.Count - 1} tiles");
                    yield return StartCoroutine(SmoothMove(movementPath));
                    
                    // Add commands to executed stack in correct order
                    for (int i = 0; i < pathCommands.Count; i++)
                    {
                        executedCommands.Push(pathCommands[i]);
                    }
                    
                    DebugLog("Movement completed");

                    if (hitObstacle)
                    {
                        DebugLog("Obstacle detected after continuous movement, starting reverse sequence");
                        StartReverseSequence();
                        yield break;
                    }
                }

                currentCommandIndex += pathCommands.Count;
                isInitialMove = false;
            }

            CurrentCommandIndex = -1;
            DebugLog("Command sequence completed, starting reverse sequence");
            StartReverseSequence();
        }

        private Vector3 GetDirectionFromCommand(MovementCommand command)
        {
            return command switch
            {
                MovementCommand.Up => Vector3.up,
                MovementCommand.Down => Vector3.down,
                MovementCommand.Left => Vector3.left,
                MovementCommand.Right => Vector3.right,
                MovementCommand.Forward => Vector3.forward,
                MovementCommand.Backward => Vector3.back,
                _ => Vector3.zero
            };
        }

        private int GetConsecutiveSteps(int startIndex, Vector3 direction)
        {
            int count = 1;
            for (int i = startIndex + 1; i < commandList.Count; i++)
            {
                MovementCommand command = commandList[i];
                if (command == MovementCommand.Wait) break; // Stop if a Wait command is encountered
                Vector3 nextDir = GetDirectionFromCommand(command);
                if (nextDir != direction) break;
                count++;
            }
            return count;
        }

        private IEnumerator SmoothMove(List<Vector3> path)
        {
            SetState(PlatformState.Moving);

            // Calculate total distance and time
            float totalDistance = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                totalDistance += Vector3.Distance(path[i - 1], path[i]);
            }

            // Base duration for single tile movement
            float baseDuration = 1f / movementSettings.moveSpeed;

            // Scale duration by number of tiles, but add extra time for acceleration/deceleration
            float totalTime = baseDuration * (path.Count - 1);

            // Add acceleration and deceleration time
            totalTime += movementSettings.accelerationTime + movementSettings.decelerationTime;

            float elapsedTime = 0f;

            while (elapsedTime < totalTime)
            {
                // Check if we should pause for a Wait command
                if (isWaiting)
                {
                    DebugLog("Movement paused by Wait command");
                    yield return new WaitUntil(() => !isWaiting);
                    DebugLog("Movement resumed after Wait");
                }

                float normalizedTime = elapsedTime / totalTime;

                // Apply easing curve
                float curvedTime = movementSettings.movementCurve.Evaluate(normalizedTime);

                // Calculate position along path
                float pathProgress = curvedTime * totalDistance;
                float accumulatedDistance = 0f;
                float segmentDistance = 0f;
                int currentSegment = 0;

                // Find current segment
                while (currentSegment < path.Count - 1)
                {
                    segmentDistance = Vector3.Distance(path[currentSegment], path[currentSegment + 1]);
                    if (accumulatedDistance + segmentDistance > pathProgress)
                        break;

                    accumulatedDistance += segmentDistance;
                    currentSegment++;
                }

                // Calculate progress within current segment
                float remainingProgress = pathProgress - accumulatedDistance;
                segmentDistance = Vector3.Distance(path[currentSegment], path[currentSegment + 1]);
                float segmentProgress = remainingProgress / segmentDistance;

                // Update platform position
                platformPosition = Vector3.Lerp(path[currentSegment], path[currentSegment + 1], segmentProgress);
                UpdatePlatformTransform();

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // Ensure exact final position
            platformPosition = path[^1];
            UpdatePlatformTransform();
            SetState(PlatformState.ExecutingCommands);
        }

        private void Move(Vector3 direction)
        {
            Vector3 newLocalPosition = platformPosition + Vector3.Scale(direction, (Vector3)gridSize);
            if (movementArea.Contains(newLocalPosition))
            {
                platformPosition = newLocalPosition;
                UpdatePlatformTransform();
            }
            else
            {
                Debug.LogWarning("New position is outside the movement area.");
            }
        }

        public void IncreaseCommandIndex()
        {
            currentCommandIndex = (currentCommandIndex + 1) % commandList.Count;
        }

        public void SetSpecificCommand(int index, MovementCommand command)
        {
            if (index >= 0 && index < commandList.Count)
            {
                commandList[index] = command;
            }
            else
            {
                Debug.LogError("Index out of bounds for command list.");
            }
        }

        public void StartCommandSequence()
        {
            StopAllCoroutines();  // Stop any ongoing sequences
            CurrentCommandIndex = -1;  // Reset to invalid index
            StartCoroutine(DelayedStart());
        }

        private IEnumerator DelayedStart()
        {
            yield return null;  // Wait one frame
            ExecuteCommands();
        }

        public void SetCommands(List<MovementCommand> commands)
        {
            commandList = commands;
        }

        private HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();
        private List<ObstacleComponent> obstacles = new List<ObstacleComponent>();
        private Stack<MovementCommand> executedCommands = new Stack<MovementCommand>();
        private bool isReversing = false;
        private Vector3 startingPosition;

        public event System.Action<Vector3Int> OnObstacleCollision;
        public event System.Action OnReverseStart;
        public event System.Action OnReverseComplete;

        public void OnObstacleUpdated(ObstacleComponent obstacle)
        {
            if (!obstacles.Contains(obstacle))
            {
                obstacles.Add(obstacle);
            }
            RecalculateOccupiedCells();
        }

        private void RecalculateOccupiedCells()
        {
            occupiedCells.Clear();
            foreach (var obstacle in obstacles)
            {
                occupiedCells.UnionWith(obstacle.OccupiedCells);
            }
        }

        private bool IsCellOccupied(Vector3Int cellPosition)
        {
            return occupiedCells.Contains(cellPosition);
        }

        private Vector3Int WorldToGridCell(Vector3 worldPosition)
        {
            Vector3 localPos = transform.InverseTransformPoint(worldPosition);
            return new Vector3Int(
                Mathf.RoundToInt(localPos.x / gridSize.x),
                Mathf.RoundToInt(localPos.y / gridSize.y),
                Mathf.RoundToInt(localPos.z / gridSize.z)
            );
        }

        public void StartReverseSequence()
        {
            if (!isReversing && executedCommands.Count > 0)
            {
                isReversing = true;
                OnReverseStart?.Invoke();
                StartCoroutine(ReverseCommandSequence());
            }
        }

        private IEnumerator ReverseCommandSequence()
        {
            SetState(PlatformState.Reversing);
            DebugLog("Starting reverse sequence");

            // Create a list of reverse commands to maintain order
            List<MovementCommand> reverseCommands = new List<MovementCommand>();
            while (executedCommands.Count > 0)
            {
                reverseCommands.Add(executedCommands.Pop());
            }

            int currentIndex = 0;
            while (currentIndex < reverseCommands.Count)
            {
                MovementCommand command = reverseCommands[currentIndex];
                MovementCommand reverseCommand = GetReverseCommand(command);
                
                DebugLog($"Processing reverse command {currentIndex + 1}/{reverseCommands.Count}: {reverseCommand}");
                
                if (reverseCommand == MovementCommand.Wait)
                {
                    DebugLog($"Reverse Wait command started. Duration: {waitTime}s");
                    SetState(PlatformState.Waiting);
                    yield return new WaitForSeconds(waitTime);
                    SetState(PlatformState.Reversing);
                    currentIndex++;
                    continue;
                }

                Vector3 direction = GetDirectionFromCommand(reverseCommand);
                if (direction == Vector3.zero)
                {
                    currentIndex++;
                    continue;
                }

                // Calculate movement path
                List<Vector3> movementPath = new List<Vector3> { platformPosition };

                // Look ahead for consecutive identical commands
                int consecutiveSteps = 0;
                int lookAheadIndex = currentIndex;

                while (lookAheadIndex < reverseCommands.Count)
                {
                    MovementCommand nextCmd = reverseCommands[lookAheadIndex];
                    MovementCommand nextReverseCmd = GetReverseCommand(nextCmd);

                    if (nextReverseCmd == MovementCommand.Wait)
                    {
                        DebugLog($"Found Wait command at index {lookAheadIndex}, breaking continuous movement");
                        break;
                    }

                    Vector3 nextDir = GetDirectionFromCommand(nextReverseCmd);
                    if (nextDir != direction)
                        break;

                    Vector3 nextPos = movementPath[^1] + Vector3.Scale(direction, (Vector3)gridSize);
                    
                    if (!movementArea.Contains(nextPos))
                        break;

                    movementPath.Add(nextPos);
                    consecutiveSteps++;
                    lookAheadIndex++;
                }

                if (consecutiveSteps > 0)
                {
                    DebugLog($"Found {consecutiveSteps} consecutive reverse movements in same direction");
                    currentIndex += consecutiveSteps;
                }
                else
                {
                    Vector3 nextPos = platformPosition + Vector3.Scale(direction, (Vector3)gridSize);
                    if (movementArea.Contains(nextPos))
                    {
                        movementPath.Add(nextPos);
                    }
                    currentIndex++;
                }

                if (movementPath.Count > 1)
                {
                    DebugLog($"Starting reverse movement over {movementPath.Count - 1} tiles");
                    yield return StartCoroutine(SmoothMove(movementPath));
                    DebugLog("Reverse movement completed");
                }
            }

            // Only adjust position if we're not exactly at the initial position
            if (Vector3.Distance(platformPosition, initialPosition) > 0.001f)
            {
                DebugLog($"Final position adjustment needed. Current: {platformPosition}, Target: {initialPosition}");
                yield return StartCoroutine(SmoothMove(new List<Vector3> { platformPosition, initialPosition }));
            }

            isReversing = false;
            isPlaying = false;
            SetState(PlatformState.Idle);
            OnReverseComplete?.Invoke();
            DebugLog("Reverse sequence completed");
        }

        private MovementCommand GetReverseCommand(MovementCommand command)
        {
            return command switch
            {
                MovementCommand.Up => MovementCommand.Down,
                MovementCommand.Down => MovementCommand.Up,
                MovementCommand.Left => MovementCommand.Right,
                MovementCommand.Right => MovementCommand.Left,
                MovementCommand.Forward => MovementCommand.Backward,
                MovementCommand.Backward => MovementCommand.Forward,
                _ => command // Wait and Idle remain the same
            };
        }

        private void DebugLog(string message)
        {
            if (DEBUG_MODE) Debug.Log($"[PuzzleInstance] {message}");
        }

        private int numberOfCommandSlots;
        public int NumberOfCommandSlots
        {
            get => commandList?.Count ?? numberOfCommandSlots;
            set
            {
                numberOfCommandSlots = value;
                if (commandList == null || commandList.Count != value)
                {
                    commandList = new List<MovementCommand>(new MovementCommand[value]);
                }
            }
        }

#if UNITY_EDITOR
        public void OnValidate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isUpdating)
                return;

            if (!Application.isPlaying)
            {
                platformPosition = new Vector3(
                    restingPosition.x * gridSize.x,
                    restingPosition.y * gridSize.y,
                    restingPosition.z * gridSize.z
                );
            }

            // Use EditorApplication.delayCall to defer platform updates
            EditorApplication.delayCall += () =>
            {
                if (this == null) return; // Check if object still exists
                EnsurePlatformExists();
                UpdatePlatformTransform();
            };

            if (movementSettings == null)
            {
                Debug.LogWarning("Platform Movement Settings not assigned!", this);
            }
        }
#endif

        public Vector3 GetPlatformWorldPosition()
        {
            return transform.TransformPoint(platformPosition);
        }

        public Vector3 WorldToLocalPosition(Vector3 worldPos)
        {
            return transform.InverseTransformPoint(worldPos);
        }

        private Vector3 SnapToGrid(Vector3 position)
        {
            return new Vector3(
                Mathf.Round(position.x / gridSize.x) * gridSize.x,
                Mathf.Round(position.y / gridSize.y) * gridSize.y,
                Mathf.Round(position.z / gridSize.z) * gridSize.z
            );
        }

        public void SetWaitDuration(int commandIndex, float duration)
        {
            waitDurations[commandIndex] = duration;
        }
    }
}
