using System;
using System.Linq;

namespace OsMapDownloader.Border
{
    internal struct Triangle
    {
        public Vertex[] Vertices;

        public Triangle(Vertex vert0, Vertex vert1, Vertex vert2)
        {
            Vertices = new Vertex[3] {vert0, vert1, vert2};
        }

        //https://stackoverflow.com/questions/2049582/how-to-determine-if-a-point-is-in-a-2d-triangle
        public bool IsPointInTriangle(Vertex point)
        {
            double d1 = Sign(point, Vertices[0], Vertices[1]);
            double d2 = Sign(point, Vertices[1], Vertices[2]);
            double d3 = Sign(point, Vertices[2], Vertices[0]);

            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
            return !(hasNeg && hasPos);
        }

        private double Sign(Vertex vert0, Vertex vert1, Vertex vert2)
        {
            return (vert0.X - vert2.X) * (vert1.Y - vert2.Y) - (vert1.X - vert2.X) * (vert0.Y - vert2.Y);
        }

        //https://www.geeksforgeeks.org/orientation-3-ordered-points/
        public bool IsClockwise()
        {
            double val = (Vertices[1].Y - Vertices[0].Y) * (Vertices[2].X - Vertices[1].X) - (Vertices[1].X - Vertices[0].X) * (Vertices[2].Y - Vertices[1].Y);
            //Collinear if =0, clockwise if >0, countclockwise if <0
            return val > 0;
        }

        public bool IntersectsTriangle(Triangle otherTri)
        {
            //Iterate over every edge in this triangle and the other triangle
            //For each edge, iterate over all 3 vertices in the opposite triangle
            //For each vertex, construct a new triangle using the edge and the vertex
            //If, for every vertex, the new triangle is anticlockwise or collinear, they don't collide
            //If, after trying all 6 edges, every edge fails this check, they collide
            for (int edge = 0; edge < 6; edge++)
            {
                Triangle edgeTri = edge < 3 ? this : otherTri;
                Triangle vertexTri = edge < 3 ? otherTri : this;
                bool passesTest = true;
                for (int vert = 0; vert < 3; vert++)
                {
                    if (new Triangle(edgeTri.Vertices[edge % 3], edgeTri.Vertices[(edge + 1) % 3], vertexTri.Vertices[vert]).IsClockwise())
                    {
                        //Triangle is clockwise, so this edge fails the test
                        passesTest = false;
                        break;
                    }
                }
                if (passesTest)
                {
                    return false;
                }
            }
            return true;
        }

        public override string ToString()
        {
            return $"({Vertices[0]}), ({Vertices[1]}), ({Vertices[2]})";
        }
    }
}
