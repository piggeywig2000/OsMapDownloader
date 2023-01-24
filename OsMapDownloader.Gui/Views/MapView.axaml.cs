using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.PanAndZoom;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Shared.PlatformSupport;
using OsMapDownloader.Coords;
using OsMapDownloader.Gui.Areas;
using OsMapDownloader.Gui.Areas.Bounds;
using OsMapDownloader.Gui.Tile;
using OsMapDownloader.Gui.ViewModels;
using OsMapDownloader.Qct.WebDownloader;

namespace OsMapDownloader.Gui.Views
{
    public partial class MapView : UserControl
    {
        private const byte MIN_ZOOM = 6;
        private const byte MAX_ZOOM = 16;

        private readonly MapViewModel viewModel;
        private readonly RotateTransform rotateTransform;
        private readonly Canvas tileContainer;
        private readonly ZoomBorder zoomBorder;
        private readonly TileManager tileManager;
        private readonly AreaManager areaManager;
        private bool isUpdating = true;

        private Point lastDrag = new Point();
        private double dragDistance = 0;

        private readonly Cursor arrowCursor;
        private readonly Cursor crossCursor;
        private readonly Cursor moveCursor;

        private MapMode _mode = MapMode.None;
        private MapMode Mode
        {
            get => _mode;
            set
            {
                if (_mode != value)
                {
                    _mode = value;
                    zoomBorder.EnablePan = Mode == MapMode.Panning;
                    viewModel.ZoomBorderCursor = _mode switch
                    {
                        MapMode.None => arrowCursor,
                        MapMode.Adding => crossCursor,
                        MapMode.Removing => crossCursor,
                        MapMode.Moving => moveCursor,
                        MapMode.Panning => moveCursor,
                        _ => arrowCursor
                    };
                    Debug.WriteLine($"Mode is now {value}");
                }
            }
        }
        private readonly Dictionary<Area, Network> networks = new Dictionary<Area, Network>();

        public byte ZoomLevel { get; private set; } = 6;
        public Scale? ScaleLock { get; private set; } = null;

        public MapView()
        {
            arrowCursor = new Cursor(StandardCursorType.Arrow);
            crossCursor = new Cursor(new Bitmap(new AssetLoader().Open(new Uri($"avares://{System.Reflection.Assembly.GetCallingAssembly().GetName().Name}/Assets/cursor_cross.png"))), new PixelPoint(16, 16));
            moveCursor = new Cursor(new Bitmap(new AssetLoader().Open(new Uri($"avares://{System.Reflection.Assembly.GetCallingAssembly().GetName().Name}/Assets/cursor_move.png"))), new PixelPoint(11, 11));

            InitializeComponent();

            viewModel = new MapViewModel(this, arrowCursor);
            rotateTransform = (RotateTransform)this.Find<ScrollViewer>("MapScrollViewer").RenderTransform!;
            zoomBorder = this.Find<ZoomBorder>("ZoomBorder");
            zoomBorder.Pan(-28 * 256, -18 * 256);
            zoomBorder.EnablePan = false;
            zoomBorder.ZoomChanged += ZoomBorder_ZoomChanged;
            zoomBorder.EffectiveViewportChanged += ZoomBorder_EffectiveViewportChanged;
            zoomBorder.PointerWheelChanged += ZoomBorder_PointerWheelChanged;
            zoomBorder.PointerPressed += ZoomBorder_PointerPressed;
            zoomBorder.PointerMoved += ZoomBorder_PointerMoved;
            zoomBorder.PointerReleased += ZoomBorder_PointerReleased;
            zoomBorder.PointerLeave += ZoomBorder_PointerLeave;
            tileContainer = this.Find<Canvas>("TileContainer");
            tileManager = ((App)Application.Current!).TileManagerInstance;
            tileManager.OnTileAdded += (object? sender, TileManager.TileImageEventArgs e) => tileContainer.Children.Add(e.ImageControl);
            tileManager.OnTileRemoved += (object? sender, TileManager.TileImageEventArgs e) => tileContainer.Children.Remove(e.ImageControl);
            areaManager = ((App)Application.Current!).AreaManagerInstance;
            areaManager.OnAreaListUpdate += AreaManager_OnAreaListUpdate;
            areaManager.OnSelectedAreaChange += AreaManager_OnSelectedAreaChange;

            foreach (Area area in areaManager.Areas)
            {
                networks.Add(area, new Network(areaManager, area, tileContainer.Children));
            }

            isUpdating = false;
            ChangeZoomLevel(ZoomLevel);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void AreaManager_OnAreaListUpdate(object? sender, EventArgs e)
        {
            foreach (Area area in networks.Keys)
            {
                if (!areaManager.Areas.Contains(area))
                {
                    networks.Remove(area);
                }
            }

            foreach (Area area in areaManager.Areas)
            {
                if (!networks.ContainsKey(area))
                {
                    Network network = new Network(areaManager, area, tileContainer.Children);
                    networks.Add(area, network);
                    network.Reposition(ZoomLevel);
                }
            }
        }

        private void AreaManager_OnSelectedAreaChange(object? sender, AreaManager.SpecificAreaEventArgs e)
        {
            viewModel.AreaSelected = areaManager.HasSelection;
            Mode = MapMode.None;
            foreach (Network network in networks.Values)
            {
                network.CancelPreview();
            }
        }

        private void SimulatePointerPressed(PointerEventArgs e, Point position)
        {
            zoomBorder.PointerPressed -= ZoomBorder_PointerPressed;
            zoomBorder.RaiseEvent(new PointerPressedEventArgs(zoomBorder, e.Pointer, tileContainer, position, e.Timestamp, new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), e.KeyModifiers));
            zoomBorder.PointerPressed += ZoomBorder_PointerPressed;
        }

        private void ZoomBorder_ZoomChanged(object? sender, ZoomChangedEventArgs e)
        {
            UpdateTiles();
        }

        private void ZoomBorder_EffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
        {
            Debug.WriteLine($"Viewport changed to {e.EffectiveViewport.Width} x {e.EffectiveViewport.Height}");
            UpdateTiles();
        }

        private void ZoomBorder_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (e.Delta.Y == 0) return;

            Point point = e.GetPosition(zoomBorder.Child);

            byte newZoomLevel = (byte)(ZoomLevel + (e.Delta.Y > 0 ? 1 : -1));
            Debug.WriteLine($"Changing zoom to {newZoomLevel} at {point.X}, {point.Y}");
            ChangeZoomLevel(newZoomLevel, point.X, point.Y);
        }

        private void ZoomBorder_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            PointerPoint pointerPoint = e.GetCurrentPoint(zoomBorder.Child);
            if (areaManager.HasSelection && pointerPoint.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
            {
                UpdatePreview(pointerPoint.Position); //Update preview to ensure that the network knows what to update
                lastDrag = pointerPoint.Position;
                dragDistance = 0;
            }
        }

        private void ZoomBorder_PointerMoved(object? sender, PointerEventArgs e)
        {
            PointerPoint pointerPoint = e.GetCurrentPoint(zoomBorder.Child);
            if (areaManager.HasSelection)
            {
                Network currentNetwork = networks[areaManager.SelectedArea!];
                //We need to determine if the preview needs changing
                if (pointerPoint.Properties.IsLeftButtonPressed)
                {
                    Point delta = lastDrag - pointerPoint.Position;
                    dragDistance += Math.Sqrt((delta.X * delta.X) + (delta.Y * delta.Y));
                    lastDrag = pointerPoint.Position;
                    if (dragDistance > 5)
                    {
                        if (Mode == MapMode.Moving || Mode == MapMode.Removing)
                        {
                            Mode = MapMode.Moving;
                            //Call method on network to continue moving preview
                            currentNetwork.UpdatePreview(MapMode.Moving, currentNetwork.AppliedIndex, SnapPosition(pointerPoint.Position), ZoomLevel);
                        }
                        else
                        {
                            MapMode oldMode = Mode;
                            Mode = MapMode.Panning;
                            if (oldMode != MapMode.Panning) SimulatePointerPressed(e, pointerPoint.Position);
                            //Call method on network to cancel preview
                            currentNetwork.CancelPreview();
                        }
                    }
                    else
                    {
                        //Call method on network to continue current preview
                        currentNetwork.UpdatePreview(currentNetwork.Mode, currentNetwork.AppliedIndex, SnapPosition(pointerPoint.Position), ZoomLevel);
                    }
                }
                else
                {
                    //As we're not holding left click, we need to determine if the Mode needs to change based on proximity to nodes/edges
                    UpdatePreview(pointerPoint.Position);
                }
            }
            else if (pointerPoint.Properties.IsLeftButtonPressed)
            {
                //Always change to panning immediately
                MapMode oldMode = Mode;
                Mode = MapMode.Panning;
                if (oldMode != MapMode.Panning) SimulatePointerPressed(e, pointerPoint.Position);
            }
        }

        private async void ZoomBorder_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            PointerPoint pointerPoint = e.GetCurrentPoint(zoomBorder.Child);
            if (pointerPoint.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
            {
                if (areaManager.HasSelection)
                {
                    await networks[areaManager.SelectedArea!].ApplyChangeToArea(SnapPosition(pointerPoint.Position), ZoomLevel);
                    networks[areaManager.SelectedArea!].CancelPreview();
                    UpdatePreview(pointerPoint.Position);
                }
                else
                {
                    Mode = MapMode.None;
                }
            }
        }

        private void ZoomBorder_PointerLeave(object? sender, PointerEventArgs e)
        {
            Mode = MapMode.None;
            if (areaManager.SelectedArea != null)
            {
                networks[areaManager.SelectedArea].CancelPreview();
            }
        }

        private void UpdatePreview(Point pointerPosition)
        {
            if (areaManager.SelectedArea == null)
            {
                Mode = MapMode.None;
                return;
            }
            else
            {
                Network selectedNetwork = networks[areaManager.SelectedArea!];
                int index;
                double distance;
                (index, distance) = selectedNetwork.GetClosestVertex(pointerPosition);
                if (distance < 10)
                {
                    Mode = MapMode.Removing;
                    //Call method on network to continue removing preview
                    networks[areaManager.SelectedArea].UpdatePreview(MapMode.Removing, index, SnapPosition(pointerPosition), ZoomLevel);
                }
                else
                {
                    if (areaManager.SelectedArea.Points.Count == 0)
                        index = 0;
                    else
                        (index, _) = selectedNetwork.GetClosestEdge(pointerPosition);
                    Mode = MapMode.Adding;
                    //Call method on network to continue adding (insert) preview
                    networks[areaManager.SelectedArea].UpdatePreview(MapMode.Adding, index, SnapPosition(pointerPosition), ZoomLevel);
                }
            }
        }

        private Point SnapPosition(Point position)
        {
            Osgb36Coordinate getDiagonalIntersect(Osgb36Coordinate point, Osgb36Coordinate lineIntersect, bool isUp)
            {
                double ux = point.Easting - lineIntersect.Easting;
                double uy = point.Northing - lineIntersect.Northing;
                double vx = 1;
                double vy = isUp ? 1 : -1;

                double vu = (ux * vx) + (uy * vy);
                double vv = (vx * vx) + (vy * vy);
                double param =  vu / vv; //In case of line of 0 length

                return new Osgb36Coordinate(lineIntersect.Easting + (param * vx), lineIntersect.Northing + (param * vy));
            }

            try
            {
                Osgb36Coordinate originCoordinate = WebTile.GetLonLatFromPixelsExact(new Coordinate(position.X, position.Y), ZoomLevel).ToOsgb36Accurate();
                Osgb36Coordinate snapCoordinate = originCoordinate;
                if (viewModel.SnapToPoints && areaManager.HasSelection)
                {
                    //Find distance to nearest snap point
                    double maxDistance = double.MaxValue; /*Math.Min(Math.Pow(2, 16 - ZoomLevel) * 250, 64000);*/
                    double closestDistance = double.MaxValue;
                    foreach (Osgb36Coordinate pointCoordinate in areaManager.SelectedArea!.Points.Select(point => point.ToOsgb36Accurate()))
                    {
                        double testDistance;
                        Osgb36Coordinate diagonal;
                        //Easting
                        testDistance = Math.Abs(originCoordinate.Easting - pointCoordinate.Easting);
                        if (testDistance < closestDistance && testDistance <= maxDistance)
                        {
                            snapCoordinate = new Osgb36Coordinate(pointCoordinate.Easting, originCoordinate.Northing);
                            closestDistance = testDistance;
                        }
                        //Northing
                        testDistance = Math.Abs(originCoordinate.Northing - pointCoordinate.Northing);
                        if (testDistance < closestDistance && testDistance <= maxDistance)
                        {
                            snapCoordinate = new Osgb36Coordinate(originCoordinate.Easting, pointCoordinate.Northing);
                            closestDistance = testDistance;
                        }
                        //Diagonal up
                        diagonal = getDiagonalIntersect(originCoordinate, pointCoordinate, true);
                        testDistance = Math.Sqrt(Math.Pow(originCoordinate.Easting - diagonal.Easting, 2) + Math.Pow(originCoordinate.Northing - diagonal.Northing, 2));
                        if (testDistance < closestDistance && testDistance <= maxDistance)
                        {
                            snapCoordinate = diagonal;
                            closestDistance = testDistance;
                        }
                        //Diagonal down
                        diagonal = getDiagonalIntersect(originCoordinate, pointCoordinate, false);
                        testDistance = Math.Sqrt(Math.Pow(originCoordinate.Easting - diagonal.Easting, 2) + Math.Pow(originCoordinate.Northing - diagonal.Northing, 2));
                        if (testDistance < closestDistance && testDistance <= maxDistance)
                        {
                            snapCoordinate = diagonal;
                            closestDistance = testDistance;
                        }
                    }
                }
                if (viewModel.SnapToGrid)
                {
                    //Use the snapCoordinate here in case our point was moved by snap to points
                    double gridLength;
                    if (ZoomLevel >= 15) gridLength = 500;
                    else if (ZoomLevel >= 13) gridLength = 1000;
                    else if (ZoomLevel >= 9) gridLength = 10000;
                    else gridLength = 100000;

                    double eastingsMod = snapCoordinate.Easting % gridLength;
                    double northingsMod = snapCoordinate.Northing % gridLength;
                    snapCoordinate = new Osgb36Coordinate(eastingsMod * 2 <= gridLength ? snapCoordinate.Easting - eastingsMod : snapCoordinate.Easting + (gridLength - eastingsMod),
                        northingsMod * 2 <= gridLength ? snapCoordinate.Northing - northingsMod : snapCoordinate.Northing + (gridLength - northingsMod));
                }
                Coordinate snapPixels = WebTile.GetPixelsFromLonLatExact(snapCoordinate.ToWgs84Accurate(), ZoomLevel);
                return new Point(snapPixels.X, snapPixels.Y);
            }
            catch
            {
                //An eastings/northing conversion was performed outside the allowed range, just return original position
                return position;
            }
        }

        public void ZoomIn() => ChangeZoomLevel((byte)(ZoomLevel + 1));
        public void ZoomOut() => ChangeZoomLevel((byte)(ZoomLevel - 1));
        public void ChangeZoomLevel(byte newZoomLevel) => ChangeZoomLevel(newZoomLevel, -zoomBorder.OffsetX + zoomBorder.Bounds.Width / 2, -zoomBorder.OffsetY + zoomBorder.Bounds.Height / 2);

        public void ChangeZoomLevel(byte newZoomLevel, double mouseX, double mouseY)
        {
            if (newZoomLevel < MIN_ZOOM || newZoomLevel > MAX_ZOOM) return;
            if (isUpdating) return;
            isUpdating = true;

            //Pan to different location based on change in zoom level
            int diff = newZoomLevel - ZoomLevel;
            double multiplier = Math.Pow(2.0, diff);
            double xOffset = mouseX + zoomBorder.OffsetX;
            double yOffset = mouseY + zoomBorder.OffsetY;
            double newMouseX = (-zoomBorder.OffsetX + xOffset) * multiplier;
            double newMouseY = (-zoomBorder.OffsetY + yOffset) * multiplier;
            zoomBorder.BeginPanTo(newMouseX - mouseX + newMouseX, newMouseY - mouseY + newMouseY);
            zoomBorder.ContinuePanTo(newMouseX, newMouseY);
            zoomBorder.BeginPanTo(newMouseX, newMouseY); //If we're currently panning while we scrolled, this will reset the initial position to prevent it pinging off

            ZoomLevel = newZoomLevel;

            viewModel.ZoomInEnabled = ZoomLevel < MAX_ZOOM;
            viewModel.ZoomOutEnabled = ZoomLevel > MIN_ZOOM;

            foreach (Network network in networks.Values)
            {
                network.Reposition(ZoomLevel);
            }
            if (areaManager.HasSelection)
            {
                Network selectedNetwork = networks[areaManager.SelectedArea!];
                selectedNetwork.UpdatePreview(selectedNetwork.Mode, selectedNetwork.AppliedIndex, SnapPosition(new Point(newMouseX, newMouseY)), ZoomLevel);
            }

            isUpdating = false;
            UpdateTiles();
        }

        public void ChangeScaleLock(Scale? newScaleLock)
        {
            if (isUpdating) return;
            isUpdating = true;
            ScaleLock = newScaleLock;
            isUpdating = false;
            UpdateTiles();
        }

        private void UpdateTiles()
        {
            if (isUpdating) return;
            isUpdating = true;
            (IntCoordinate topLeft, IntCoordinate bottomRight) = GetVisibleTiles();

            tileManager?.UpdateVisibleTiles(topLeft, bottomRight, UiTile.GetScaleFromZoom(ZoomLevel, ScaleLock), ZoomLevel);

            //Rotate canvas
            rotateTransform.Angle = GetCanvasRotation() * (180 / Math.PI);

            isUpdating = false;
        }

        private (IntCoordinate, IntCoordinate) GetVisibleTiles()
        {
            double centerX = -zoomBorder.OffsetX + (zoomBorder.Bounds.Width / 2);
            double centerY = -zoomBorder.OffsetY + (zoomBorder.Bounds.Height / 2);
            double cornerToCenter = Math.Sqrt((zoomBorder.Bounds.Width * zoomBorder.Bounds.Width) + (zoomBorder.Bounds.Height * zoomBorder.Bounds.Height)) / 2;
            double diagonalAngle = Math.Atan2(zoomBorder.Bounds.Height, zoomBorder.Bounds.Width);
            double angleError = GetCanvasRotation();

            IntCoordinate tr = new IntCoordinate((int)Math.Floor((centerX + (Math.Cos(diagonalAngle - angleError) * cornerToCenter)) / 256.0), (int)Math.Floor((centerY + -(Math.Sin(diagonalAngle - angleError) * cornerToCenter)) / 256.0));
            IntCoordinate tl = new IntCoordinate((int)Math.Floor((centerX + (Math.Cos(Math.PI - diagonalAngle - angleError) * cornerToCenter)) / 256.0), (int)Math.Floor((centerY + -(Math.Sin(Math.PI - diagonalAngle - angleError) * cornerToCenter)) / 256.0));
            IntCoordinate bl = new IntCoordinate((int)Math.Floor((centerX + (Math.Cos(Math.PI + diagonalAngle - angleError) * cornerToCenter)) / 256.0), (int)Math.Floor((centerY + -(Math.Sin(Math.PI + diagonalAngle - angleError) * cornerToCenter)) / 256.0));
            IntCoordinate br = new IntCoordinate((int)Math.Floor((centerX + (Math.Cos((2 * Math.PI) - diagonalAngle - angleError) * cornerToCenter)) / 256.0), (int)Math.Floor((centerY + -(Math.Sin((2 * Math.PI) - diagonalAngle - angleError) * cornerToCenter)) / 256.0));

            return (new IntCoordinate(Math.Min(tl.X, bl.X), Math.Min(tl.Y, tr.Y)), new IntCoordinate(Math.Max(tr.X, br.X), Math.Max(bl.Y, br.Y)));
        }

        private double GetCanvasRotation()
        {
            if (ZoomLevel < 8) return 0;
            try
            {
                Osgb36Coordinate tl = WebTile.GetLonLatFromPixelsExact(new Coordinate(-zoomBorder.OffsetX, -zoomBorder.OffsetY), ZoomLevel).ToOsgb36Accurate();
                Osgb36Coordinate br = WebTile.GetLonLatFromPixelsExact(new Coordinate(-zoomBorder.OffsetX + zoomBorder.Bounds.Width, -zoomBorder.OffsetY + zoomBorder.Bounds.Height), ZoomLevel).ToOsgb36Accurate();
                return Math.Atan2(tl.Northing - br.Northing, br.Easting - tl.Easting) - Math.Atan2(zoomBorder.Bounds.Height, zoomBorder.Bounds.Width);
            }
            catch
            {
                return 0;
            }
        }
    }
}
