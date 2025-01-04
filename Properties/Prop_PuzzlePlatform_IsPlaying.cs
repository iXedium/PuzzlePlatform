using System;
using GameCreator.Runtime.Common;
using UnityEngine;
using PuzzlePlatform.Core;

namespace PuzzlePlatform.Properties
{
    [Title("Is Playing")]
    [Category("Puzzle Platform/Is Playing")]
    [Image(typeof(IconGear), ColorTheme.Type.Purple)]
    [Serializable]
    public class Prop_PuzzlePlatform_IsPlaying : PropertyTypeGetBool
    {
        [SerializeField] private PropertyGetGameObject m_Platform = GetGameObjectInstance.Create();

        public override bool Get(Args args)
        {
            GameObject platformGO = this.m_Platform.Get(args);
            if (platformGO == null) return false;

            PuzzleInstance platform = platformGO.GetComponent<PuzzleInstance>();
            return platform != null && platform.IsPlaying;
        }

        public static PropertyGetBool Create => new PropertyGetBool(
            new Prop_PuzzlePlatform_IsPlaying()
        );

        public override string String => "Platform Is Playing";
    }
}