using System.Collections.Generic;
using UnityEngine;
using PuzzlePlatform.Core;

namespace PuzzlePlatform.Managers
{
    public class PuzzlePlatformManager : MonoBehaviour
{
    private int nextPuzzleId = 0;
    public List<PuzzleInstance> puzzleInstances = new List<PuzzleInstance>();

    public PuzzleInstance CreatePuzzleInstance(
        Vector3 position,
        Vector3Int gridSize,
        Bounds movementArea,
        Vector3Int? restingPos = null,
        int? cmdSlots = null,
        List<PuzzleInstance.MovementCommand> initialCommands = null
    )
    {
        GameObject puzzleObject = new GameObject($"PuzzleInstance_{nextPuzzleId}");
        PuzzleInstance puzzleInstance = puzzleObject.AddComponent<PuzzleInstance>();

        puzzleInstance.puzzleId = nextPuzzleId.ToString();
        puzzleInstance.gridSize = gridSize;
        puzzleInstance.movementArea = movementArea;
        puzzleInstance.restingPosition = restingPos ?? Vector3Int.zero;
        puzzleInstance.NumberOfCommandSlots = cmdSlots ?? 5;

        if (initialCommands != null)
        {
            puzzleInstance.SetCommands(initialCommands);
        }

        puzzleObject.transform.position = position;
        puzzleInstances.Add(puzzleInstance);
        nextPuzzleId++;

        return puzzleInstance;
    }

    public void StartAllPuzzles()
    {
        foreach (var puzzle in puzzleInstances)
        {
            puzzle.StartCommandSequence();
        }
    }

    private void OnDrawGizmos()
    {
        foreach (var puzzle in puzzleInstances)
        {
            if (puzzle == null)
                continue;

            Gizmos.color = Color.yellow;
            if (puzzleInstances.IndexOf(puzzle) > 0)
            {
                var prevPuzzle = puzzleInstances[puzzleInstances.IndexOf(puzzle) - 1];
                Gizmos.DrawLine(puzzle.transform.position, prevPuzzle.transform.position);
            }
        }
    }

    public void InitializeCommands()
    {
        foreach (var puzzle in puzzleInstances)
        {
            if (puzzle != null)
            {
                var commands = new List<PuzzleInstance.MovementCommand>(puzzle.NumberOfCommandSlots);
                for (int i = 0; i < puzzle.NumberOfCommandSlots; i++)
                {
                    commands.Add(PuzzleInstance.MovementCommand.Idle);
                }
                puzzle.SetCommands(commands);
            }
        }
    }
}
}