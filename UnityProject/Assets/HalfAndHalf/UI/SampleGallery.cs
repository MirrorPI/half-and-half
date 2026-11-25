using HalfAndHalf.Content;
using HalfAndHalf.Gameplay;
using HalfAndHalf.Presentation;
using TMPro;
using UnityEngine;

namespace HalfAndHalf.UI
{
    public sealed class SampleGallery : MonoBehaviour
    {
        public AppleGame Game;
        public VisualSampleAsset[] Samples;
        public TMP_Text Label, Hint;
        public UnityEngine.UI.Button Previous, Next;
        public int Selected { get; private set; }
        public bool ReviewOnly;
        void Start()
        {
            if (ReviewOnly) return;
            Previous.onClick.AddListener(() => Select((Selected + Samples.Length - 1) % Samples.Length));
            Next.onClick.AddListener(() => Select((Selected + 1) % Samples.Length));
            Select(0);
        }
        void Update()
        { if (!ReviewOnly && Game.Session != null) Hint.gameObject.SetActive(Game.Session.Phase == HalfAndHalf.Core.AttemptPhase.Ready); }
        public void Select(int index)
        {
            if (index < 0 || index >= Samples.Length) return;
            Selected = index; var sample = Samples[index];
            var presentation = Game.Presentation;
            if (sample.ObjectId == "B01") presentation.Sample.gameObject.SetActive(false);
            else { presentation.Sample.gameObject.SetActive(true); presentation.Sample.Show(sample); }
            presentation.Audio.ResetAttempt(); presentation.Audio.CutVariants = sample.CutVariants;
            Label.text = sample.DisplayName; Hint.text = sample.MassHint;
            Game.Initialize(sample.Definition());
            Game.Hud.Refresh(Game.Session, (long)(Time.realtimeSinceStartupAsDouble * 1000));
        }
    }
}
