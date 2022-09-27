using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using GeoUK.OSTN;
using OsMapDownloader.Gui.Areas;
using OsMapDownloader.Gui.Config;
using OsMapDownloader.Gui.Tile;

namespace OsMapDownloader.Gui
{
    public partial class App : Application
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public AreaManager AreaManagerInstance { get; private set; }
        public TileManager TileManagerInstance { get; private set; }
        public ConfigLoader<Settings> ConfigInstance { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime)
            {
                return;
            }
            IClassicDesktopStyleApplicationLifetime desktop = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime;

            //Show splash
            desktop.MainWindow = new Splash();
            desktop.MainWindow.Show();

            //Run startup tasks
            Task.Run(async () => //Gotta run in separate thread to prevent deadlock
            {
                Task<AreaManager> areaManagerTask = AreaManager.CreateAreaManager();
                Task<ConfigLoader<Settings>> configTask = ConfigLoader<Settings>.CreateConfigLoader("config.json", () => new Settings());
                Task preloadTask = Task.Run(() => Transform.PreloadResources());
                await Task.WhenAll(areaManagerTask, configTask, preloadTask);
                AreaManagerInstance = areaManagerTask.Result;
                ConfigInstance = configTask.Result;
            }).Wait();

            TileManagerInstance = new TileManager(new HttpClient());
            ConfigInstance.OnModify += (s, e) => TileManagerInstance.WebToken = ConfigInstance.Config.Token;
            TileManagerInstance.WebToken = ConfigInstance.Config.Token;

            

            //Show main window
            desktop.MainWindow.Close();
            desktop.MainWindow = new MainWindow();

            base.OnFrameworkInitializationCompleted();
        }
    }
}
