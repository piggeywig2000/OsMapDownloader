using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using OsMapDownloader.Coords;
using OsMapDownloader.Gui.Areas;
using OsMapDownloader.Gui.Dialogs;
using OsMapDownloader.Qct;

namespace OsMapDownloader.Gui
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly AreaManager areaManager;

        public new event PropertyChangedEventHandler? PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool IsAreaSelected
        {
            get => areaManager.SelectedArea != null;
        }

        public bool IsAreaPopulated
        {
            get => areaManager.HasSelection && areaManager.SelectedArea!.Points.Count >= 3;
        }

        public MainWindow()
        {
            DataContext = this;
            areaManager = ((App)Application.Current!).AreaManagerInstance;
            areaManager.OnSelectedAreaChange += (sender, e) =>
            {
                NotifyPropertyChanged(nameof(IsAreaSelected));
                NotifyPropertyChanged(nameof(IsAreaPopulated));
            };
            areaManager.OnAreaPointsChange += (sender, e) => NotifyPropertyChanged(nameof(IsAreaPopulated));

            InitializeComponent();
        }

        public async void NewArea()
        {
            TextDialog dialog = new TextDialog("New Area", "Please enter a name for the new area:");
            string? name = await dialog.ShowDialog<string?>(this);
            if (name == null)
                return;

            await areaManager.NewArea(name);
        }

        public async void DuplicateArea()
        {
            if (!areaManager.HasSelection) return;

            await areaManager.DuplicateArea(areaManager.SelectedArea!);
        }

        public async void RenameArea()
        {
            if (!areaManager.HasSelection) return;

            TextDialog dialog = new TextDialog("Rename Area", "Please enter the new name for this area:", areaManager.SelectedArea!.Name);
            string? name = await dialog.ShowDialog<string?>(this);
            if (name == null)
                return;

            areaManager.SelectedArea.Name = name;
            await areaManager.Save();
        }

        public async void DeleteArea()
        {
            if (!areaManager.HasSelection) return;

            ConfirmationDialog dialog = new ConfirmationDialog("Delete Area", $"Are you sure you want to delete the area \"{areaManager.SelectedArea!.Name}\"?");
            bool shouldDelete = await dialog.ShowDialog<bool>(this);

            if (shouldDelete)
            {
                await areaManager.DeleteArea(areaManager.SelectedArea);
            }
        }

        public async void ImportArea()
        {
            OpenFileDialog dialog = new OpenFileDialog()
            {
                AllowMultiple = true,
                Filters = new List<FileDialogFilter>() { new FileDialogFilter() { Name = "Quick Chart file", Extensions = new List<string>() { "qct" } } },
                Title = "Import a QCT file"
            };
            string[]? filePath = await dialog.ShowAsync(this);
            if (filePath == null) return;
            for (int i = 0; i < filePath.Length; i++)
            {
                QctMetadata metadata;
                try
                {
                    QctReader reader = new QctReader(filePath[i]);
                    metadata = await reader.ReadMetadata();
                }
                catch
                {
                    await new MessageDialog("Import Error", "The file could not be read.\n\nCheck that the file is a valid QCT file, and that it's not open in another program.").ShowDialog(this);
                    return;
                }
                await areaManager.NewArea(metadata.Name ?? Path.GetFileNameWithoutExtension(filePath[i]), new List<Wgs84Coordinate>(metadata.MapOutline));
            }
        }

        public async void ExportArea()
        {
            if (!areaManager.HasSelection) return;

            ExportDialog dialog = new ExportDialog(areaManager.SelectedArea!);
            await dialog.ShowDialog(this);
        }

        public async void OpenSettings()
        {
            await new SettingsDialog().ShowDialog(this);
        }

        public async void OpenAbout()
        {
            await new AboutDialog().ShowDialog(this);
        }
    }
}
