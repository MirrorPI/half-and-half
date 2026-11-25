using System;
using System.Collections.Generic;
using UnityEngine;

namespace HalfAndHalf.Presentation
{
    public static class PhotoCutoutMask
    {
        // The path is in bottom-left-origin image UVs, matching Texture2D.GetPixels32.
        // Scan at texel centers and return a tight crop with discarded pixels cleared.
        public static bool TryCrop(Color32[] source, int width, int height, IReadOnlyList<Vector2> path,
            out Color32[] result, out int cropWidth, out int cropHeight)
        {
            result = null;
            cropWidth = cropHeight = 0;
            if (path.Count < 3) return false;
            var mask = new Color32[source.Length];
            var crossings = new List<float>();
            int left = width, right = -1, bottom = height, top = -1;
            for (int y = 0; y < height; y++)
            {
                float row = (y + .5f) / height;
                crossings.Clear();
                for (int i = 0, j = path.Count - 1; i < path.Count; j = i++)
                {
                    Vector2 a = path[j], b = path[i];
                    if ((a.y > row) == (b.y > row)) continue;
                    crossings.Add(a.x + (row - a.y) * (b.x - a.x) / (b.y - a.y));
                }
                crossings.Sort();
                for (int k = 1; k < crossings.Count; k += 2)
                {
                    int start = Math.Max(0, (int)Math.Ceiling(crossings[k - 1] * width - .5f));
                    int end = Math.Min(width, (int)Math.Ceiling(crossings[k] * width - .5f));
                    for (int x = start; x < end; x++)
                    {
                        int index = y * width + x;
                        if (source[index].a == 0) continue;
                        mask[index] = source[index];
                        if (source[index].a <= 8) continue;
                        left = Math.Min(left, x); right = Math.Max(right, x);
                        bottom = Math.Min(bottom, y); top = Math.Max(top, y);
                    }
                }
            }
            if (right < left) return false;
            cropWidth = right - left + 1;
            cropHeight = top - bottom + 1;
            result = new Color32[cropWidth * cropHeight];
            for (int y = 0; y < cropHeight; y++)
                Array.Copy(mask, (bottom + y) * width + left, result, y * cropWidth, cropWidth);
            return true;
        }
    }
}
