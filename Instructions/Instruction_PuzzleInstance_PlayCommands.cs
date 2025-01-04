using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;
using PuzzlePlatform.Core;

namespace PuzzlePlatform.Instructions
{
    [Title("Play Puzzle Commands")]
    [Description("Executes the command sequence of a PuzzleInstance")]
    [Category("Puzzle Platform/Play Puzzle Commands")]

    [Keywords("Puzzle", "Platform", "Execute", "Play", "Start")]
    [Image(typeof(IconGear), ColorTheme.Type.Purple)]

    [Serializable]
    public class Instruction_PuzzleInstance_PlayCommands : Instruction
    {
        [SerializeField] private PropertyGetGameObject m_PuzzleInstance = GetGameObjectInstance.Create();

        protected override async System.Threading.Tasks.Task Run(Args args)
        {
            GameObject puzzleObj = m_PuzzleInstance.Get(args);
            if (puzzleObj == null) return;

            PuzzleInstance puzzle = puzzleObj.GetComponent<PuzzleInstance>();
            if (puzzle == null) return;

            puzzle.StartCommandSequence();
            await System.Threading.Tasks.Task.Yield();
        }
    }
}