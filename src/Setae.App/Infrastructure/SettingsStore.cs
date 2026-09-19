using System.Text;
using System.Text.Json;
using System.IO;
using Setae.App.Monitoring;

namespace Setae.App.Infrastructure;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _directoryPath;

    public SettingsStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "setae"))
    {
    }

    internal SettingsStore(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("La carpeta de ajustes no puede estar vacía.", nameof(directoryPath));
        }

        _directoryPath = directoryPath;
        FilePath = Path.Combine(_directoryPath, "settings.json");
    }

    public string FilePath { get; }

    public SettingsLoadResult Load()
    {
        if (!File.Exists(FilePath))
        {
            return new SettingsLoadResult(AppSettings.Defaults, null);
        }

        try
        {
            var json = File.ReadAllText(FilePath, Encoding.UTF8);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);

            if (!AppSettingsValidator.IsValid(settings))
            {
                return new SettingsLoadResult(
                    AppSettings.Defaults,
                    "Los ajustes guardados no son válidos. Se han restaurado los valores iniciales.");
            }

            return new SettingsLoadResult(settings!, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new SettingsLoadResult(
                AppSettings.Defaults,
                $"No se pudieron cargar los ajustes. Se usarán los valores iniciales. ({exception.Message})");
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        AppSettingsValidator.EnsureValid(settings);
        MonitoringPreferencesValidator.EnsureValid(settings.Monitoring);

        Directory.CreateDirectory(_directoryPath);
        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        var temporaryPath = $"{FilePath}.{Environment.ProcessId}.tmp";

        try
        {
            File.WriteAllText(temporaryPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, FilePath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (IOException)
            {
                // The original save error is more useful to the caller.
            }
            catch (UnauthorizedAccessException)
            {
                // The original save error is more useful to the caller.
            }
        }
    }
}
