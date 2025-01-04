using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;
using PuzzlePlatform.Core;

namespace PuzzlePlatform.Instructions
{

    [Title("Set Puzzle Command")]
    [Description("Sets a specific command at the given slot in a PuzzleInstance")]
    [Category("Puzzle Platform/Set Puzzle Command")]

    [Keywords("Puzzle", "Platform", "Command", "Set")]
    [Image(typeof(IconGear), ColorTheme.Type.Purple)]

    [Serializable]
    public class Instruction_PuzzleInstance_SetCommand : Instruction
    {
        [SerializeField] private PropertyGetGameObject m_PuzzleInstance = GetGameObjectInstance.Create();
        [SerializeField] private PropertyGetInteger m_SlotIndex = GetDecimalInteger.Create(0);
        [SerializeField] private PuzzleInstance.MovementCommand m_Command = PuzzleInstance.MovementCommand.Idle;

        protected override async System.Threading.Tasks.Task Run(Args args)
        {
            GameObject puzzleObj = m_PuzzleInstance.Get(args);
            if (puzzleObj == null) return;

            PuzzleInstance puzzle = puzzleObj.GetComponent<PuzzleInstance>();
            if (puzzle == null) return;

            int slotIndex = (int)m_SlotIndex.Get(args);
            puzzle.SetSpecificCommand(slotIndex, m_Command);

            await System.Threading.Tasks.Task.Yield();
        }
    }
}