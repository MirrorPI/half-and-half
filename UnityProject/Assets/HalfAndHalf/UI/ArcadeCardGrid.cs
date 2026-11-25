using UnityEngine;
using UnityEngine.UI;

namespace HalfAndHalf.UI
{
    public sealed class ArcadeCardGrid : GridLayoutGroup
    {
        public override void CalculateLayoutInputHorizontal()
        {
            float width = (rectTransform.rect.width - padding.horizontal - spacing.x) / 2f;
            if (width > 0) m_CellSize = new Vector2(width, width * .94f);
            base.CalculateLayoutInputHorizontal();
        }
    }
}
