using System;

namespace HalfAndHalf.Core
{
    // Rotate/scale each rectangle, then clip it against the screen's vertical knife plane.
    // Overlapping bodies remain separate cells; visual occlusion never removes their mass.
    internal static class MassProjection
    {
        readonly struct Point
        {
            public readonly decimal X, Y;
            public Point(decimal x, decimal y) { X = x; Y = y; }
        }

        public static MassSplit Split(MassMap map, decimal knifeX, TransformState state)
        {
            if (state.ScaleX <= 0 || state.ScaleY <= 0)
                throw new ArgumentException("Mass projection requires positive scale.");
            decimal degrees = (state.RotationDegrees % 360m + 360m) % 360m;
            if (degrees == 0)
                return map.Split((knifeX - state.X) / state.ScaleX);

            decimal cosine, sine;
            if (degrees == 90) { cosine = 0; sine = 1; }
            else if (degrees == 180) { cosine = -1; sine = 0; }
            else if (degrees == 270) { cosine = 0; sine = -1; }
            else
            {
                double angle = (double)degrees * Math.PI / 180;
                cosine = (decimal)Math.Cos(angle); sine = (decimal)Math.Sin(angle);
            }
            // Translation is removed from the plane so area math is independent of world origin.
            decimal plane = knifeX - state.X, left = 0;
            var polygon = new Point[4]; var clipped = new Point[6];
            foreach (var cell in map.Cells)
            {
                if (cell.Mass == 0) continue;
                polygon[0] = Rotate(cell.MinX, cell.MinY, state, cosine, sine);
                polygon[1] = Rotate(cell.MaxX, cell.MinY, state, cosine, sine);
                polygon[2] = Rotate(cell.MaxX, cell.MaxY, state, cosine, sine);
                polygon[3] = Rotate(cell.MinX, cell.MaxY, state, cosine, sine);
                int count = 0; Point previous = polygon[3]; bool wasInside = previous.X <= plane;
                foreach (var current in polygon)
                {
                    bool inside = current.X <= plane;
                    if (inside != wasInside)
                    {
                        decimal fraction = (plane - previous.X) / (current.X - previous.X);
                        clipped[count++] = new Point(plane, previous.Y + (current.Y - previous.Y) * fraction);
                    }
                    if (inside) clipped[count++] = current;
                    previous = current; wasInside = inside;
                }
                decimal fractionLeft = count < 3 ? 0 : Area(clipped, count) / Area(polygon, 4);
                left += cell.Mass * Math.Max(0m, Math.Min(1m, fractionLeft));
            }
            return new MassSplit(left, map.TotalMass - left);
        }
        static Point Rotate(decimal x, decimal y, TransformState s, decimal c, decimal sin)
        {
            x *= s.ScaleX; y *= s.ScaleY;
            return new Point(x * c - y * sin, x * sin + y * c);
        }
        static decimal Area(Point[] points, int count)
        {
            decimal twiceArea = 0; var origin = points[0];
            for (int i = 1; i < count - 1; i++)
                twiceArea += (points[i].X - origin.X) * (points[i + 1].Y - origin.Y)
                           - (points[i + 1].X - origin.X) * (points[i].Y - origin.Y);
            return Math.Abs(twiceArea) / 2m;
        }
    }
}
