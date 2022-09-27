using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using OsMapDownloader.Gui.Areas;

namespace OsMapDownloader.Gui.Views
{
    public partial class AreaListView : UserControl, INotifyPropertyChanged
    {
        private readonly AreaManager areaManager;

        public new event PropertyChangedEventHandler? PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public IEnumerable ItemNames { get => areaManager.Areas.OrderBy(area => area.Name.ToLower()); }

        public int SelectedIndex
        {
            get => areaManager.SelectedArea == null ? -1 : areaManager.Areas.OrderBy(area => area.Name.ToLower()).TakeWhile(area => area != areaManager.SelectedArea).Count();
            set => areaManager.ChangeSelectedAreaIndex(value < 0 ? value : areaManager.Areas.IndexOf(areaManager.Areas.OrderBy(area => area.Name.ToLower()).ToArray()[value]));
        }

        public AreaListView()
        {
            DataContext = this;
            areaManager = ((App)Application.Current!).AreaManagerInstance;
            areaManager.OnAreaListUpdate += OnAreaListChange;
            areaManager.OnAreaRename += OnAreaListChange;
            areaManager.OnSelectedAreaChange += (sender, e) => NotifyPropertyChanged(nameof(SelectedIndex));

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnAreaListChange(object? sender, EventArgs e)
        {
            Area? oldSelectedArea = areaManager.SelectedArea;
            NotifyPropertyChanged(nameof(ItemNames));
            areaManager.ChangeSelectedArea(oldSelectedArea);
            //NotifyPropertyChanged(nameof(SelectedIndex));
        }
    }
}
