using HalfAndHalf.Content;
using UnityEngine;

namespace HalfAndHalf.Gameplay
{
    [DefaultExecutionOrder(-100)]
    public sealed class CarVoxelTrialController : MonoBehaviour
    {
        public AppleGame Game;
        public VisualSampleAsset Content;
        void Start()
        {
            Game.Presentation.Audio.ResetAttempt();
            Game.Presentation.Audio.CutVariants=Content.CutVariants;
            Game.Initialize(Content.Definition());
        }
    }
}
