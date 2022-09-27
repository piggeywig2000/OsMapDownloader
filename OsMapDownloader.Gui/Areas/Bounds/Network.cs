using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using OsMapDownloader.Coords;
using OsMapDownloader.WebDownloader;

namespace OsMapDownloader.Gui.Areas.Bounds
{
    internal class Network : IDisposable
    {
        private bool hasDisposed;
        private readonly AreaManager areaManager;
        private readonly AutoControlList<BoundVertex> vertices;
        private readonly AutoControlList<BoundEdge> edges;
        private readonly AutoControlList<BoundVertex> previewVertices;
        private readonly AutoControlList<BoundEdge> previewEdges;

        public MapMode Mode { get; private set; } = MapMode.None;
        public int AppliedIndex { get; private set; } = 0;
        public Area Area { get; }
        public bool IsSelected { get => areaManager.SelectedArea == Area; }

        public Network(AreaManager areaManager, Area area, Controls container)
        {
            this.areaManager = areaManager;
            Area = area;
            vertices = new AutoControlList<BoundVertex>(container);
            edges = new AutoControlList<BoundEdge>(container);
            previewVertices = new AutoControlList<BoundVertex>(container);
            previewEdges = new AutoControlList<BoundEdge>(container);

            RebuildVerticesAndEdges();
            BindEvents();
        }

        private void BindEvents()
        {
            areaManager.OnAreaListUpdate += AreaManager_OnAreaListUpdate;
            areaManager.OnSelectedAreaChange += AreaManager_OnSelectedAreaChange;
        }

        private void UnbindEvents()
        {
            areaManager.OnAreaListUpdate -= AreaManager_OnAreaListUpdate;
            areaManager.OnSelectedAreaChange -= AreaManager_OnSelectedAreaChange;
        }

        private void AreaManager_OnAreaListUpdate(object? sender, EventArgs e)
        {
            if (!areaManager.Areas.Contains(Area))
            {
                Dispose();
            }
        }

        private void AreaManager_OnSelectedAreaChange(object? sender, AreaManager.SpecificAreaEventArgs e)
        {
            foreach (BoundVertex vertex in vertices) vertex.IsSelected = IsSelected;
            foreach (BoundEdge edge in edges) edge.IsSelected = IsSelected;
        }

        public void Reposition(byte zoom)
        {
            for (int i = 0; i < Area.Points.Count; i++)
            {
                Coordinate pixels = WebTile.GetPixelsFromLonLatExact(Area.Points[i], zoom);
                Point point = new Point(pixels.X, pixels.Y);
                vertices[i].Center = point;
                vertices[i].IsInvalid = IsPositionInvalid(Area.Points[i].ToOsgb36Accurate());
                edges[i].From = point;
                edges[GetPreviousEdgeIndex(i)].To = point;
            }
        }

        private void RebuildVerticesAndEdges()
        {
            //Recreate vertex and edge lists
            vertices.Clear();
            edges.Clear();

            for (int i = 0; i < Area.Points.Count; i++)
            {
                AddVertex(i, new Point(), new Osgb36Coordinate());
            }
        }

        public void CancelPreview() => UpdatePreview(MapMode.None, 0, new Point(), 0);
        public void UpdatePreview(MapMode mode, int index, Point position, byte zoom)
        {
            MapMode oldMode = Mode;
            Mode = mode;
            AppliedIndex = index;
            //Debug.WriteLine(new StackTrace());
            //Debug.WriteLine($"Update preview    Mode: {mode}    Index: {index}    Position: {position}");

            if (oldMode != mode)
            {
                ResetPreview();
            }

            if (mode == MapMode.None)
            {
                //Nothing to do here
            }
            else if (mode == MapMode.Adding)
            {
                //Perform setup
                if (oldMode != MapMode.Adding)
                {
                    previewVertices.Add(new BoundVertex() { IsPreview = true, IsSelected = IsSelected });
                    if (vertices.Count >= 1)
                        previewEdges.Add(new BoundEdge() { IsPreview = true, IsSelected = IsSelected });
                    if (vertices.Count >= 2)
                        previewEdges.Add(new BoundEdge() { IsPreview = true, IsSelected = IsSelected });
                }
                //Perform update
                previewVertices[0].Center = position;
                previewVertices[0].IsInvalid = IsPositionInvalid(WebTile.GetLonLatFromPixelsExact(new Coordinate(position.X, position.Y), zoom).ToOsgb36Accurate());
                if (vertices.Count >= 1)
                {
                    previewEdges[0].From = position;
                    previewEdges[0].To = vertices[GetPreviousVertexIndex(index)].Center;
                }
                if (vertices.Count >= 2)
                {
                    previewEdges[1].From = position;
                    previewEdges[1].To = vertices[index % vertices.Count].Center;
                }
            }
            else if (mode == MapMode.Removing)
            {
                //Perform setup
                if (oldMode != MapMode.Removing)
                {
                    //if (vertices.Count >= 3)
                    //    previewEdges.Add(new BoundEdge() { IsPreview = false, IsSelected = IsSelected });
                }
                //Perform update
                for (int i = 0; i < vertices.Count; i++) vertices[i].IsPreview = i == index;
                for (int i = 0; i < edges.Count; i++) edges[i].IsPreview = i == index || i == GetPreviousEdgeIndex(index);
                //if (vertices.Count >= 3)
                //{
                //    previewEdges[0].From = vertices[GetPreviousVertexIndex(index)].Center;
                //    previewEdges[0].To = vertices[GetNextVertexIndex(index)].Center;
                //}
            }
            else if (mode == MapMode.Moving)
            {
                //Perform setup
                if (oldMode != MapMode.Moving)
                {
                    previewVertices.Add(new BoundVertex() { IsPreview = true, IsSelected = IsSelected });
                    if (vertices.Count >= 2)
                        previewEdges.Add(new BoundEdge() { IsPreview = true, IsSelected = IsSelected });
                    if (vertices.Count >= 3)
                        previewEdges.Add(new BoundEdge() { IsPreview = true, IsSelected = IsSelected });
                    vertices[index].IsVisible = false;
                    edges[index].IsVisible = false;
                    edges[GetPreviousEdgeIndex(index)].IsVisible = false;
                }
                //Perform update
                previewVertices[0].Center = position;
                previewVertices[0].IsInvalid = IsPositionInvalid(WebTile.GetLonLatFromPixelsExact(new Coordinate(position.X, position.Y), zoom).ToOsgb36Accurate());
                if (vertices.Count >= 2)
                {
                    previewEdges[0].From = position;
                    previewEdges[0].To = vertices[GetPreviousVertexIndex(index)].Center;
                }
                if (vertices.Count >= 3)
                {
                    previewEdges[1].From = position;
                    previewEdges[1].To = vertices[GetNextVertexIndex(index)].Center;
                }
            }
        }

        private void ResetPreview()
        {
            previewVertices.Clear();
            previewEdges.Clear();
            foreach (BoundVertex vertex in vertices)
            {
                vertex.IsPreview = false;
                vertex.IsVisible = true;
            }
            foreach (BoundEdge edge in edges)
            {
                edge.IsPreview = false;
                edge.IsVisible = true;
            }
        }

        public async Task ApplyChangeToArea(Point position, byte zoom)
        {
            if (Mode == MapMode.Adding)
            {
                Debug.WriteLine($"Applying add to {AppliedIndex}");
                Wgs84Coordinate lonLat = WebTile.GetLonLatFromPixelsExact(new Coordinate(position.X, position.Y), zoom);
                Area.Points.Insert(AppliedIndex, lonLat);
                areaManager.RaisePointsChange(Area);
                AddVertex(AppliedIndex, position, lonLat.ToOsgb36Accurate());
                await areaManager.Save();
            }
            else if (Mode == MapMode.Removing)
            {
                Debug.WriteLine($"Applying remove to {AppliedIndex}");
                Area.Points.RemoveAt(AppliedIndex);
                areaManager.RaisePointsChange(Area);
                RemoveVertex(AppliedIndex);
                await areaManager.Save();
            }
            else if (Mode == MapMode.Moving)
            {
                Debug.WriteLine($"Applying move to {AppliedIndex}");
                Wgs84Coordinate lonLat = WebTile.GetLonLatFromPixelsExact(new Coordinate(position.X, position.Y), zoom);
                Area.Points[AppliedIndex] = lonLat;
                areaManager.RaisePointsChange(Area);
                MoveVertex(AppliedIndex, position, lonLat.ToOsgb36Accurate());
                await areaManager.Save();
            }
        }

        private void AddVertex(int index, Point position, Osgb36Coordinate easNor)
        {
            BoundVertex vertex = new BoundVertex()
            {
                Center = position,
                IsInvalid = IsPositionInvalid(easNor),
                IsSelected = IsSelected
            };
            vertices.Insert(index, vertex);
            BoundEdge edge = new BoundEdge()
            {
                From = vertices[index].Center,
                To = vertices[GetNextVertexIndex(index)].Center,
                IsSelected = IsSelected
            };
            edges.Insert(index, edge);

            //Adjust previous edge
            edges[GetPreviousEdgeIndex(index)].To = position;
        }

        private void RemoveVertex(int index)
        {
            if (edges.Count >= 2)
            {
                edges[GetPreviousEdgeIndex(index)].To = vertices[GetNextVertexIndex(index)].Center;
            }
            vertices.RemoveAt(index);
            edges.RemoveAt(index);
        }

        private void MoveVertex(int index, Point position, Osgb36Coordinate easNor)
        {
            vertices[index].Center = position;
            vertices[index].IsInvalid = IsPositionInvalid(easNor);
            edges[index].From = position;
            edges[GetPreviousEdgeIndex(index)].To = position;
        }

        private bool IsPositionInvalid(Osgb36Coordinate easNor) => Math.Round(easNor.Easting) < 0 || Math.Round(easNor.Northing) > 1249000 || Math.Round(easNor.Easting) > 876248000 || Math.Round(easNor.Northing) < 0;

        private int GetPreviousVertexIndex(int index) => (index - 1 + vertices.Count) % vertices.Count;

        private int GetNextVertexIndex(int index) => (index + 1) % vertices.Count;
        private int GetPreviousEdgeIndex(int index) => (index - 1 + edges.Count) % edges.Count;

        private int GetNextEdgeIndex(int index) => (index + 1) % edges.Count;

        public (int index, double distance) GetClosestVertex(Point point)
        {
            int index = -1;
            double distanceSquared = double.MaxValue;
            for (int i = 0; i < vertices.Count; i++)
            {
                BoundVertex vertex = vertices[i];
                double dx = point.X - vertex.Center.X;
                double dy = point.Y - vertex.Center.Y;
                double vDistanceSquared = (dx * dx) + (dy * dy);
                if (vDistanceSquared < distanceSquared)
                {
                    distanceSquared = vDistanceSquared;
                    index = i;
                }
            }
            return (index, Math.Sqrt(distanceSquared));
        }

        public (int index, double distance) GetClosestEdge(Point point)
        {
            double getDistanceSquared(Point from, Point to)
            {
                double ux = point.X - from.X;
                double uy = point.Y - from.Y;
                double vx = to.X - from.X;
                double vy = to.Y - from.Y;

                double vu = (ux * vx) + (uy * vy);
                double vv = (vx * vx) + (vy * vy);
                double param = vv != 0 ? vu / vv : 0; //In case of line of 0 length

                Point intersect;
                if (param <= 0)
                    intersect = from;
                else if (param >= 1)
                    intersect = to;
                else
                    intersect = new Point(from.X + (param * vx), from.Y + (param * vy));

                Point delta = point - intersect;
                return (delta.X * delta.X) + (delta.Y * delta.Y);
            }

            int index = -1;
            double distanceSquared = double.MaxValue;
            for (int i = 0; i < edges.Count; i++)
            {
                BoundEdge edge = edges[GetPreviousEdgeIndex(i)];
                double eDistanceSquared = getDistanceSquared(edge.From, edge.To);
                if (eDistanceSquared < distanceSquared)
                {
                    distanceSquared = eDistanceSquared;
                    index = i;
                }
            }

            return (index, Math.Sqrt(distanceSquared));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!hasDisposed)
            {
                if (disposing)
                {
                    //There are no managed objects but if there were they'd be disposed here
                }

                vertices.Clear();
                edges.Clear();
                previewVertices.Clear();
                previewEdges.Clear();
                UnbindEvents();
                // TODO: set large fields to null
                hasDisposed = true;
            }
        }

        ~Network()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
