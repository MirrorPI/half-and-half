using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HalfAndHalf.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PhotoOutlineInput : MaskableGraphic, IPointerDownHandler, IPointerUpHandler,
        IInitializePotentialDragHandler, IDragHandler, IScrollHandler
    {
        public PhotoCutoutEditor Editor;
        public RectTransform ZoomRoot, Viewport;
        readonly List<Vector2> path = new List<Vector2>();
        readonly Dictionary<int, Vector2> touches = new Dictionary<int, Vector2>();
        bool navigating;
        float zoom = 1;

        public void ResetView()
        {
            ResetGesture();
            zoom = 1;
            ZoomRoot.localScale = Vector3.one;
            ZoomRoot.anchoredPosition = Vector2.zero;
        }

        void ResetGesture()
        {
            path.Clear();
            touches.Clear();
            navigating = false;
            SetVerticesDirty();
        }

        protected override void OnDisable() { ResetGesture(); base.OnDisable(); }
        void OnApplicationPause(bool paused) { if (paused) ResetGesture(); }

        public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold = false; }

        public void OnPointerDown(PointerEventData e)
        {
            if (touches.Count >= 2 || e.button != PointerEventData.InputButton.Left) return;
            touches[e.pointerId] = Local(Viewport, e);
            if (touches.Count == 2)
            {
                navigating = true;
                path.Clear();
                SetVerticesDirty();
            }
            else if (Editor.CanTrace) AddPoint(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!touches.TryGetValue(e.pointerId, out var previous)) return;
            var current = Local(Viewport, e);
            if (touches.Count == 2)
            {
                foreach (var pair in touches)
                {
                    if (pair.Key == e.pointerId) continue;
                    Vector2 other = pair.Value;
                    float oldDistance = Vector2.Distance(other, previous);
                    float ratio = oldDistance > 1 ? Vector2.Distance(other, current) / oldDistance : 1;
                    AdjustView((other + previous) * .5f, (other + current) * .5f, ratio);
                    break;
                }
            }
            else if (!navigating && Editor.CanTrace) AddPoint(e);
            touches[e.pointerId] = current;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!touches.Remove(e.pointerId)) return;
            if (!navigating && Editor.CanTrace)
            {
                AddPoint(e);
                if (path.Count > 2) Editor.FinishStroke(path);
            }
            path.Clear();
            if (touches.Count == 0) navigating = false;
            SetVerticesDirty();
        }

        public void OnScroll(PointerEventData e)
        {
            var point = Local(Viewport, e);
            AdjustView(point, point, e.scrollDelta.y > 0 ? 1.25f : .8f);
        }

        static Vector2 Local(RectTransform rect, PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera ?? e.enterEventCamera, out var point);
            return point;
        }

        void AdjustView(Vector2 oldCenter, Vector2 newCenter, float ratio)
        {
            float next = Mathf.Clamp(zoom * ratio, 1, 5);
            var position = newCenter - (oldCenter - ZoomRoot.anchoredPosition) * (next / zoom);
            var limit = Vector2.Max(Vector2.zero, (rectTransform.rect.size * next - Viewport.rect.size) * .5f);
            ZoomRoot.anchoredPosition = new Vector2(Mathf.Clamp(position.x, -limit.x, limit.x), Mathf.Clamp(position.y, -limit.y, limit.y));
            ZoomRoot.localScale = new Vector3(next, next, 1);
            zoom = next;
            SetVerticesDirty();
        }

        void AddPoint(PointerEventData e)
        {
            var rect = rectTransform.rect;
            Vector2 p = Local(rectTransform, e);
            var uv = Rect.PointToNormalized(rect, p);
            uv = new Vector2(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y));
            if (path.Count > 0 && Vector2.Scale(path[path.Count - 1] - uv, rect.size).sqrMagnitude * zoom * zoom < 4) return;
            if (path.Count >= 2048)
            {
                for (int i = 1; i < 1024; i++) path[i] = path[i * 2];
                path.RemoveRange(1024, 1024);
            }
            path.Add(uv);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            Quad(mesh, rect.min, new Vector2(rect.xMin,rect.yMax), rect.max, new Vector2(rect.xMax,rect.yMin), Color.clear);
            for (int i = 1; i < path.Count; i++)
            {
                var from = Rect.NormalizedToPoint(rect, path[i - 1]);
                var to = Rect.NormalizedToPoint(rect, path[i]);
                var edge = Vector2.Perpendicular(to - from).normalized * (2.5f / zoom);
                Quad(mesh, from - edge, from + edge, to + edge, to - edge, color);
            }
        }

        static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 tint)
        {
            int i = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero); mesh.AddVert(b, tint, Vector2.zero);
            mesh.AddVert(c, tint, Vector2.zero); mesh.AddVert(d, tint, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2); mesh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
