using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;
using PuzzlePlatform.Core;

namespace PuzzlePlatform.Instructions
{

    [Title("Decrease Puzzle Command")]
    [Description("Decrements the command at the specified slot in a PuzzleInstance")]
    [Category("Puzzle Platform/Decrease Puzzle Command")]

    [Keywords("Puzzle", "Platform", "Command", "Decrement")]
    [Image(typeof(IconGear), ColorTheme.Type.Purple)]

    [Serializable]
    public class Instruction_PuzzleInstance_DecreaseCommand : Instruction
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

            // Get current command and decrement to previous enum value
            PuzzleInstance.MovementCommand currentCmd = puzzle.commandList[slotIndex];
            int enumLength = System.Enum.GetValues(typeof(PuzzleInstance.MovementCommand)).Length;
            int prevCmdIndex = ((int)currentCmd - 1 + enumLength) % enumLength;

            puzzle.SetSpecificCommand(slotIndex, (PuzzleInstance.MovementCommand)prevCmdIndex);

            await System.Threading.Tasks.Task.Yield();
        }
    }
}