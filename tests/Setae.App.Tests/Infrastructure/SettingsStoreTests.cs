using System.Text.Json;
using Setae.App.Infrastructure;
using Setae.App.Monitoring;

namespace Setae.App.Tests;

public class SettingsStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsPreferencesWithoutAudioData()
    {
        using var directory = TemporaryDirectory.Create();
        var store = new SettingsStore(directory.Path);
        var settings = AppSettings.Defaults with
        {
            Monitoring = MonitoringPreferences.Defaults with
            {
                Threshold = 64f,
                MinimumAlertDuration = TimeSpan.FromMilliseconds(800),
                Cooldown = TimeSpan.FromSeconds(5),
                BeepEnabled = false,
                MicrophoneId = "device-id"
            },
            WindowLeft = 120,
            WindowTop = 80
        };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Null(loaded.Warning);
        Assert.Equal(settings, loaded.Settings);
        Assert.DoesNotContain("audio", File.ReadAllText(store.FilePath), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_InvalidJson_RestoresDefaultsWithWarning()
    {
        using var directory = TemporaryDirectory.Create();
        var store = new SettingsStore(directory.Path);
        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(store.FilePath, "not-json");

        var result = store.Load();

        Assert.Equal(AppSettings.Defaults, result.Settings);
        Assert.NotNull(result.Warning);
    }

    [Fact]
    public void Load_InvalidValues_RestoresDefaultsWithWarning()
    {
        using var directory = TemporaryDirectory.Create();
        var store = new SettingsStore(directory.Path);
        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(store.FilePath, JsonSerializer.Serialize(new
        {
            Monitoring = new { Threshold = 101f }
        }));

        var result = store.Load();

        Assert.Equal(AppSettings.Defaults, result.Settings);
        Assert.NotNull(result.Warning);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            return new TemporaryDirectory(System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "setae-tests-" + Guid.NewGuid().ToString("N")));
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
