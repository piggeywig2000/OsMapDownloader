using System;

namespace OsMapDownloader.Border
{
    internal struct Vertex
    {
        public double X;
        public double Y;

        public Vertex(double x, double y)
        {
            X = x;
            Y = y;
        }

        public override string ToString()
        {
            return $"{X}, {Y}";
        }

        public static bool operator ==(Vertex vert1, Vertex vert2) => vert1.X == vert2.X && vert1.Y == vert2.Y;
        public static bool operator !=(Vertex vert1, Vertex vert2) => vert1.X != vert2.X || vert1.Y != vert2.Y;
    }
}
