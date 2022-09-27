using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OsMapDownloader.Coords;
using OsMapDownloader.Gui.Config;

namespace OsMapDownloader.Gui.Areas
{
    public class AreaManager
    {
        private readonly ConfigLoader<List<Area>> configLoader;
        private List<Area> areas => configLoader.Config;

        public ReadOnlyCollection<Area> Areas { get => new ReadOnlyCollection<Area>(areas); }
        public Area? SelectedArea { get; private set; } = null;
        public bool HasSelection { get => SelectedArea != null; }

        public class SpecificAreaEventArgs : EventArgs
        {
            public SpecificAreaEventArgs(Area? affectedArea) : base()
            {
                AffectedArea = affectedArea;
            }

            public Area? AffectedArea { get; }
        }
        public event EventHandler? OnAreaListUpdate;
        public event EventHandler<SpecificAreaEventArgs>? OnSelectedAreaChange;
        public event EventHandler<SpecificAreaEventArgs>? OnAreaRename;
        public event EventHandler<SpecificAreaEventArgs>? OnAreaPointsChange;

        public static async Task<AreaManager> CreateAreaManager()
        {
            ConfigLoader<List<Area>> configLoader = await ConfigLoader<List<Area>>.CreateConfigLoader("areas.json", () => new List<Area>());
            AreaManager areaManager = new AreaManager(configLoader);
            return areaManager;
        }

        private AreaManager(ConfigLoader<List<Area>> configLoader)
        {
            this.configLoader = configLoader;
            foreach (Area area in areas)
            {
                BindEvents(area);
            }
        }

        public Task Save() => configLoader.Save();

        public void RaisePointsChange(Area areaToRaise)
        {
            if (!areas.Contains(areaToRaise))
                throw new ArgumentException("Tried to raise points updated to an area that's not in the list of areas", "areaToRaise");

            OnAreaPointsChange?.Invoke(this, new SpecificAreaEventArgs(areaToRaise));
        }

        public void ChangeSelectedAreaIndex(int areaIndex) => ChangeSelectedArea(areaIndex < 0 ? null : areas[areaIndex]);
        public void ChangeSelectedArea(Area? area)
        {
            if (area != null && !areas.Contains(area))
                throw new ArgumentException("Tried to change selected area to an area not in the list of areas", "area");

            SelectedArea = area;
            OnSelectedAreaChange?.Invoke(this, new SpecificAreaEventArgs(area));
        }

        public Task NewArea(string name) => NewArea(name, new List<Wgs84Coordinate>());
        public async Task NewArea(string name, List<Wgs84Coordinate> points)
        {
            Area area = new Area(name, points);
            areas.Add(area);
            OnAreaListUpdate?.Invoke(this, EventArgs.Empty);
            BindEvents(area);
            ChangeSelectedArea(area);
            await configLoader.Save();
        }

        public async Task DuplicateArea(Area areaToDuplicate)
        {
            if (!areas.Contains(areaToDuplicate))
                throw new ArgumentException("Tried to duplicate an area that's not in the list of areas", "areaToDuplicate");

            List<Wgs84Coordinate> points = new List<Wgs84Coordinate>();
            foreach (Wgs84Coordinate oldPoint in areaToDuplicate.Points)
            {
                points.Add(new Wgs84Coordinate(oldPoint.Longitude, oldPoint.Latitude));
            }
            await NewArea(areaToDuplicate.Name, points);
        }

        public async Task DeleteArea(Area areaToDelete)
        {
            if (!areas.Contains(areaToDelete))
                throw new ArgumentException("Tried to delete an area that's not in the list of areas", "areaToDelete");

            if (areaToDelete == SelectedArea)
                ChangeSelectedArea(null);

            UnbindEvents(areaToDelete);
            areas.Remove(areaToDelete);
            OnAreaListUpdate?.Invoke(this, EventArgs.Empty);
            await configLoader.Save();
        }

        private void BindEvents(Area area)
        {
            area.OnRename += Area_OnRename;
        }

        private void UnbindEvents(Area area)
        {
            area.OnRename -= Area_OnRename;
        }

        private void Area_OnRename(object? sender, EventArgs e)
        {
            OnAreaRename?.Invoke(this, new SpecificAreaEventArgs((Area?)sender));
        }
    }
}
