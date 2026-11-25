using UnityEngine;

namespace HalfAndHalf.Presentation
{
    // Local-space landmarks in the authored meshes. Zoom is relative to the READY frame.
    internal static class StageScaleReveal
    {
        public static Transform Landmark(ApplePresentation view, string version, out Vector3 point, out float zoom)
        {
            (point, zoom) = version switch
            {
                "B02-pixel-cucumber-v1" => (new Vector3(-.95f,.05f,0), 1.25f),   // stem scar
                "B03-pixel-watermelon-v1" => (new Vector3(-.18f,.67f,-.12f), 1.45f),
                "B04-pixel-pumpkin-v1" => (new Vector3(.03f,.60f,0), 1.50f),
                "A01-pixel-eggplant-v1" => (new Vector3(.70f,.35f,0), 1.35f),
                "A02-pixel-banana-v2" => (new Vector3(.77f,.33f,0), 1.40f),
                "A04-pixel-hammer-v1" => (new Vector3(.55f,.1f,-.18f), 1.75f),
                "A05-pixel-guitar-v1" => (new Vector3(-.34f,0,-.17f), 2f),     // sound hole
                "L01-pixel-fan-v1" => (new Vector3(-.09f,.37f,-.39f), 2.35f),
                "L02-pixel-car-v2" => (new Vector3(.77f,-.76f,-.39f), 2.85f),
                "L03-pixel-bus-v1" => (new Vector3(-.74f,-.22f,-.29f), 3.4f),
                "L04-pixel-excavator-v1" => (new Vector3(-.35f,.11f,-.24f), 4f),
                "L05-pixel-building-v1" => (new Vector3(.36f,-.51f,-.21f), 4.7f), // entrance
                "N01-pixel-mountain-v1" => (new Vector3(-.17f,.61f,.01f), 5.25f),
                "N02-pixel-glacier-v3" => (new Vector3(.20f,.55f,-.065f), 5.5f),
                "N04-pixel-volcano-v1" => (new Vector3(-.12f,.30f,-.13f), 5.7f),
                "N05-pixel-island-v1" => (new Vector3(.46f,.42f,.04f), 1f/.17f),
                "P01-pixel-moon-v1" => (new Vector3(-.334f,.015f,-.78f), 6.4f),
                "P02-pixel-earth-v1" => (new Vector3(-.24f,.22f,-.80f), 7.2f), // west African coast
                "P05-pixel-saturn-v1" => (Vector3.zero, 8.2f),                  // Titan
                "S01-pixel-solarsystem-v1" => (Vector3.zero, 9f),               // Earth in its orbit
                "G01-pixel-whirlpool-v2" => (Vector3.zero, 1f/.105f),
                "G02-pixel-cartwheel-v2" => (Vector3.zero, 11f),
                "G03-pixel-antennae-v1" => (Vector3.zero, 12.5f),
                _ => (Vector3.zero, 1f)
            };
            if (zoom <= 1) return null;
            if (view.Space != null && view.Space.gameObject.activeSelf)
                return view.Space.Landmark(ref point);
            if (view.Car != null && view.Car.gameObject.activeSelf) return view.Car.transform;
            if (view.Voxel != null && view.Voxel.gameObject.activeSelf) return view.Voxel.transform;
            return null;
        }
    }
}
