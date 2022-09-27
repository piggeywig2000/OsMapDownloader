using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OsMapDownloader.Gui.Config
{
    public class ConfigLoader<T>
    {
        private SemaphoreSlim semaphore = new SemaphoreSlim(1);

        public string Path { get; }
        public T Config { get; private set; }

        public event EventHandler? OnModify;

        public static async Task<ConfigLoader<T>> CreateConfigLoader(string path, Func<T> newConfigGenerator, CancellationToken cancellationToken = default)
        {
            T initialConfig;
            if (!File.Exists(path))
            {
                initialConfig = newConfigGenerator();
                await SaveToFile(path, initialConfig, cancellationToken);
            }
            else
            {
                initialConfig = await LoadFromFile(path, cancellationToken);
            }
            return new ConfigLoader<T>(path, initialConfig);
        }

        private ConfigLoader(string path, T initialConfig)
        {
            Path = path;
            Config = initialConfig;
        }

        public async Task Save(CancellationToken cancellationToken = default)
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                await SaveToFile(Path, Config, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
            OnModify?.Invoke(this, EventArgs.Empty);
        }

        public async Task Load(CancellationToken cancellationToken = default)
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                Config = await LoadFromFile(Path, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
            OnModify?.Invoke(this, EventArgs.Empty);
        }

        private static async Task SaveToFile(string path, T config, CancellationToken cancellationToken = default)
        {
            using FileStream jsonStream = File.OpenWrite(path);
            await JsonSerializer.SerializeAsync<T>(jsonStream, config, cancellationToken: cancellationToken);
            jsonStream.SetLength(jsonStream.Position);
        }

        private static async Task<T> LoadFromFile(string path, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("The config file could not be found");
            using FileStream jsonStream = File.OpenRead(path);

            T? newConfig = await JsonSerializer.DeserializeAsync<T>(jsonStream, cancellationToken: cancellationToken);
            if (newConfig == null)
                throw new JsonException("An error occurred while reading the config file");

            return newConfig;
        }
    }
}
