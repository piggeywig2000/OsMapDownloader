using System;
using System.Collections.Generic;
using System.Linq;
using OsMapDownloader.Coords;

namespace OsMapDownloader.Border
{
    public class MapArea
    {
        private readonly Osgb36Coordinate[] borderPoints;
        private Vertex[] _vertices = Array.Empty<Vertex>();
        private Triangle[] _triangles = Array.Empty<Triangle>();

        public MapArea(Osgb36Coordinate[] borderPoints)
        {
            this.borderPoints = borderPoints;
        }

        public void CalculateVerticesAndTriangles()
        {
            //Firstly try in the direction given
            try
            {
                _vertices = borderPoints.Select(point => new Vertex(point.Easting / 1000, point.Northing / 1000)).ToArray();
                _triangles = DoEarClipping(_vertices);
            }
            catch (TriangleGenerationException)
            {
                //Then try anticlockwise
                _vertices = borderPoints.Select(point => new Vertex(point.Easting / 1000, point.Northing / 1000)).Reverse().ToArray();
                _triangles = DoEarClipping(_vertices);
            }
        }

        private Triangle[] DoEarClipping(Vertex[] vertices)
        {
            List<Triangle> triangles = new List<Triangle>();
            List<Vertex> border = new List<Vertex>(vertices);
            while (triangles.Count < vertices.Length - 2)
            {
                bool hasAdded = false;
                for (int i = 0; i < border.Count; i++)
                {
                    Triangle triangle = new Triangle(border[(i - 1 + border.Count) % border.Count], border[i], border[(i + 1) % border.Count]);
                    //Vertex i must not be reflex, so triangle must be clockwise
                    //Triangle must not contain any of the other points
                    if (triangle.IsClockwise() && border.All(point => !triangle.IsPointInTriangle(point) || Array.Exists(triangle.Vertices, triPoint => triPoint == point)))
                    {
                        triangles.Add(triangle);
                        border.RemoveAt(i);
                        hasAdded = true;
                        break;
                    }
                }
                if (!hasAdded)
                {
                    throw new TriangleGenerationException("Failed to generate triangles for this map");
                }
            }
            return triangles.ToArray();
        }

        public bool IsPointInArea(double eastings, double northings)
        {
            Vertex point = new Vertex(eastings / 1000, northings / 1000);
            return _triangles.Any(tri => tri.IsPointInTriangle(point));
        }

        public bool IsRectangleInArea(double tlEastings, double tlNorthings, double brEastings, double brNorthings)
        {
            Triangle tlTri = new Triangle(new Vertex(tlEastings / 1000, brNorthings / 1000), new Vertex(tlEastings / 1000, tlNorthings / 1000), new Vertex(brEastings / 1000, tlNorthings / 1000));
            Triangle brTri = new Triangle(new Vertex(brEastings / 1000, tlNorthings / 1000), new Vertex(brEastings / 1000, brNorthings / 1000), new Vertex(tlEastings / 1000, brNorthings / 1000));
            return _triangles.Any(tri => tri.IntersectsTriangle(tlTri) || tri.IntersectsTriangle(brTri));
        }

        public float[] GetOpenGLVertices()
        {
            return _vertices.SelectMany(vert => new float[] { (float)vert.X, (float)vert.Y, 0.0f }).ToArray();
        }

        public uint[] GetOpenGLIndices()
        {
            return _triangles.SelectMany(tri => tri.Vertices.Select(vert => (uint)Array.IndexOf(_vertices, vert))).ToArray();
        }
    }
}
