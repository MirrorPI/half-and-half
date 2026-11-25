using UnityEngine;
using UnityEngine.UI;

namespace HalfAndHalf.UI
{
    [RequireComponent(typeof(RawImage))]
    public sealed class WorkshopDemo : MonoBehaviour
    {
        const int Columns = 4;
        const int Rows = 2;
        const float FramesPerSecond = 4f;
        RawImage image;
        float started;
        int frame;

        void OnEnable()
        {
            image = GetComponent<RawImage>();
            started = Time.unscaledTime;
            frame = -1;
            Update();
        }

        void Update()
        {
            int next = (int)((Time.unscaledTime - started) * FramesPerSecond) % (Rows * Columns);
            if (frame == next) return;
            frame = next;
            int rowFromBottom = Rows - 1 - frame / Columns;
            image.uvRect = new Rect((float)(frame % Columns) / Columns, (float)rowFromBottom / Rows,
                1f / Columns, 1f / Rows);
        }
    }
}
