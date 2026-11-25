using UnityEngine;
namespace HalfAndHalf.Presentation
{
    public sealed class SamplePart : MonoBehaviour
    {
        public Material Section;
        public float Density = 1;
        public float SectionBias; // Small display-only separation for a nested core's coplanar cap.
    }
}
