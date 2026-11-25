using System;
using UnityEngine;

namespace HalfAndHalf.Presentation
{
    // A future device image picker passes a decoded/resized, readable Texture2D here.
    // The caller owns its lifetime. This view changes neither the cut axis nor the contact anchor.
    public sealed class KnifeAppearance : MonoBehaviour
    {
        public Transform ContactPoint;
        public GameObject DefaultVisual;
        public Renderer ImageRenderer;
        public float MaximumImageWidth = .7f, MaximumImageHeight = 1.13f;
        const float DefaultVisualScale = 1.12f;
        const float CustomVisualScale = 1.68f;
        const int MaximumTextureDimension = 2048;
        const byte MinimumVisibleAlpha = 8;
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
        MaterialPropertyBlock properties;

        public float ContactLocalY => transform.InverseTransformPoint(ContactPoint.position).y;

        void Awake()
        {
            var visual = DefaultVisual.transform;
            Vector3 tip = visual.parent.InverseTransformPoint(ContactPoint.position);
            visual.localPosition = tip + (visual.localPosition - tip) * DefaultVisualScale;
            visual.localScale *= DefaultVisualScale;
        }

        public void SetImage(Texture2D image)
        {
            if (image == null) { UseDefault(); return; }
            if (ContactPoint == null || DefaultVisual == null || ImageRenderer == null)
                throw new InvalidOperationException("Knife appearance references are not configured.");
            if (!image.isReadable || image.width > MaximumTextureDimension || image.height > MaximumTextureDimension)
                throw new ArgumentException("Knife images must be readable and resized to at most 2048 pixels per side.", nameof(image));
            if (MaximumImageWidth <= 0 || MaximumImageHeight <= 0)
                throw new InvalidOperationException("Knife display bounds must be positive.");

            ImageRegion region = VisiblePixels(image); RectInt visible = region.Pixels;
            float contactU = visible.width == 1 ? .5f : Mathf.Clamp01((region.ContactX - visible.x - .5f) / (visible.width - 1));
            float widestSide = Mathf.Max(contactU, 1 - contactU) * visible.width;
            float scale = CustomVisualScale * Mathf.Min(MaximumImageWidth * .5f / widestSide, MaximumImageHeight / visible.height);
            float width = visible.width * scale, height = visible.height * scale;
            Vector3 contact = transform.InverseTransformPoint(ContactPoint.position);
            var view = ImageRenderer.transform;
            view.localPosition = new Vector3(contact.x + (.5f - contactU) * width, contact.y + height * .5f, -.09f);
            view.localRotation = Quaternion.identity;
            view.localScale = new Vector3(width, height, 1);
            if (properties == null) properties = new MaterialPropertyBlock();
            properties.Clear(); properties.SetTexture(BaseMap, image);
            // Sample texel centers so bilinear Repeat textures never wrap an opposite edge into the crop.
            // A one-pixel crop has a zero UV span and samples its single center consistently.
            properties.SetVector(BaseMapST, new Vector4((float)(visible.width - 1) / image.width, (float)(visible.height - 1) / image.height,
                (visible.x + .5f) / image.width, (visible.y + .5f) / image.height));
            ImageRenderer.SetPropertyBlock(properties);
            DefaultVisual.SetActive(false); ImageRenderer.gameObject.SetActive(true);
        }

        public void UseDefault()
        {
            if (ImageRenderer != null) { ImageRenderer.SetPropertyBlock(null); ImageRenderer.gameObject.SetActive(false); }
            if (properties != null) properties.Clear();
            if (DefaultVisual != null) DefaultVisual.SetActive(true);
        }

        readonly struct ImageRegion
        {
            public readonly RectInt Pixels;
            public readonly float ContactX;
            public ImageRegion(RectInt pixels, float contactX) { Pixels = pixels; ContactX = contactX; }
        }

        static ImageRegion VisiblePixels(Texture2D image)
        {
            var pixels = image.GetPixels32();
            int minX = image.width, minY = image.height, maxX = -1, maxY = -1;
            for (int y = 0; y < image.height; y++)
                for (int x = 0; x < image.width; x++)
                    if (pixels[y * image.width + x].a > MinimumVisibleAlpha)
                    { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }
            if (maxX < 0) throw new ArgumentException("Knife image has no visible pixels.", nameof(image));
            var visible = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            float center = minX + visible.width * .5f, contactX = center, nearest = float.PositiveInfinity;
            // Use the midpoint of the nearest opaque run on the lowest row. A gap between two tips
            // is never chosen, and a tip at the side still aligns with the fixed gameplay cut axis.
            for (int x = minX; x <= maxX;)
            {
                while (x <= maxX && pixels[minY * image.width + x].a <= MinimumVisibleAlpha) x++;
                int first = x;
                while (x <= maxX && pixels[minY * image.width + x].a > MinimumVisibleAlpha) x++;
                if (first == x) break;
                float midpoint = (first + x) * .5f, distance = Mathf.Abs(midpoint - center);
                if (distance < nearest) { nearest = distance; contactX = midpoint; }
            }
            return new ImageRegion(visible, contactX);
        }
    }
}
