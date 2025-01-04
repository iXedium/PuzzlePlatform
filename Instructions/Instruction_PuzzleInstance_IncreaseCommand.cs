using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;
using PuzzlePlatform.Core;

namespace PuzzlePlatform.Instructions
{

    [Title("Increase Puzzle Command")]
    [Description("Increments the command at the specified slot in a PuzzleInstance")]
    [Category("Puzzle Platform/Increase Puzzle Command")]

    [Keywords("Puzzle", "Platform", "Command", "Increment")]
    [Image(typeof(IconGear), ColorTheme.Type.Purple)]

    [Serializable]
    public class Instruction_PuzzleInstance_IncreaseCommand : Instruction
    {
        [SerializeField]
        private PropertyGetGameObject m_PuzzleInstance = GetGameObjectInstance.Create();

        [SerializeField]
        private PropertyGetInteger m_SlotIndex = GetDecimalInteger.Create(0);

        protected override async System.Threading.Tasks.Task Run(Args args)
        {
            GameObject puzzleObj = m_PuzzleInstance.Get(args);
            if (puzzleObj == null) return;

            PuzzleInstance puzzle = puzzleObj.GetComponent<PuzzleInstance>();
            if (puzzle == null) return;

            int slotIndex = (int)m_SlotIndex.Get(args);
            if (slotIndex < 0 || slotIndex >= puzzle.commandList.Count)
            {
                Debug.LogWarning($"Invalid slot index: {slotIndex}");
                return;
            }

            // Get current command and increment to next enum value
            PuzzleInstance.MovementCommand currentCmd = puzzle.commandList[slotIndex];
            int nextCmdIndex = ((int)currentCmd + 1) %
                System.Enum.GetValues(typeof(PuzzleInstance.MovementCommand)).Length;

            puzzle.SetSpecificCommand(slotIndex, (PuzzleInstance.MovementCommand)nextCmdIndex);

            await System.Threading.Tasks.Task.Yield();
        }
    }
}