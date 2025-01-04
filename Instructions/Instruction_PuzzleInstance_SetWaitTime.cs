using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;
using PuzzlePlatform.Core;

namespace PuzzlePlatform.Instructions
{
    [Title("Set Puzzle Wait Time")]
    [Description("Sets the wait duration for a specific Wait command or globally")]
    [Category("Puzzle Platform/Set Puzzle Wait Time")]

    [Keywords("Puzzle", "Platform", "Wait", "Duration", "Time")]
    [Image(typeof(IconGear), ColorTheme.Type.Purple)]

    [Serializable]
    public class Instruction_PuzzleInstance_SetWaitTime : Instruction
    {
        [SerializeField] private PropertyGetGameObject m_PuzzleInstance = GetGameObjectInstance.Create();
        [SerializeField] private PropertyGetInteger m_SlotIndex = GetDecimalInteger.Create(-1);
        [SerializeField] private PropertyGetDecimal m_Duration = GetDecimalDecimal.Create(1f);

        protected override async System.Threading.Tasks.Task Run(Args args)
        {
            GameObject puzzleObj = m_PuzzleInstance.Get(args);
            if (puzzleObj == null) return;

            PuzzleInstance puzzle = puzzleObj.GetComponent<PuzzleInstance>();
            if (puzzle == null) return;

            int slotIndex = (int)m_SlotIndex.Get(args);
            float duration = (float)m_Duration.Get(args);

            if (slotIndex >= 0)
            {
                puzzle.SetWaitDuration(slotIndex, duration);
            }
            else
            {
                puzzle.waitTime = duration;
            }

            await System.Threading.Tasks.Task.Yield();
        }
    }
}