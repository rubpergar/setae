using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Setae.Core;
using MediaBrush = System.Windows.Media.Brush;

namespace Setae.App;

public partial class SettingsWindow : Window
{
    private readonly AudioDeviceService _deviceService;
    private readonly AudioMonitor _monitor;
    private readonly DispatcherTimer _previewTimer;

    public SettingsWindow(
        AudioDeviceService deviceService,
        AudioMonitor monitor,
        MonitoringPreferences preferences,
        string? selectedDeviceId,
        bool keepOnTop)
    {
        InitializeComponent();

        _deviceService = deviceService;
        _monitor = monitor;
        TrySaveSettings = _ => true;
        _previewTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(34)
        };
        _previewTimer.Tick += PreviewTimer_OnTick;
        Loaded += SettingsWindow_OnLoaded;
        Closed += SettingsWindow_OnClosed;
        LoadPreferences(preferences, keepOnTop);
        RefreshDevices(selectedDeviceId);
    }

    public Func<MonitoringPreferences, bool> TrySaveSettings { get; set; }

    public bool KeepOnTop => TopmostCheckBox.IsChecked == true;

    private void RefreshButton_OnClick(object sender, RoutedEventArgs e)
    {
        RefreshDevices(SelectedDeviceId);
    }

    private void RefreshDevices(string? preferredDeviceId)
    {
        try
        {
            var devices = _deviceService.EnumerateCaptureDevices();
            var defaultId = _deviceService.GetDefaultCaptureDeviceId();
            var targetId = devices.Any(device => device.Id == preferredDeviceId)
                ? preferredDeviceId
                : preferredDeviceId is null
                    ? devices.FirstOrDefault(device => device.Id == defaultId)?.Id
                    : null;

            MicrophoneComboBox.ItemsSource = devices;
            MicrophoneComboBox.SelectedValue = targetId;

            StatusText.Text = devices.Count == 0
                ? "No hay micrófonos activos. Puedes guardar los demás ajustes y conectar uno después."
                : targetId is null
                    ? "El dispositivo guardado no está conectado."
                    : string.Empty;
            StatusText.Foreground = devices.Count == 0 || targetId is null
                ? (MediaBrush)FindResource("WarningBrush")
                : (MediaBrush)FindResource("MutedTextBrush");
        }
        catch (Exception exception)
        {
            StatusText.Text = $"No se pudieron enumerar los micrófonos: {exception.Message}";
            StatusText.Foreground = (MediaBrush)FindResource("DangerBrush");
        }
    }

    private string? SelectedDeviceId => (MicrophoneComboBox.SelectedItem as AudioDeviceInfo)?.Id;

    private void ThresholdSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ThresholdValueText is not null)
        {
            ThresholdValueText.Text = $"{ThresholdSlider.Value:0}";
        }

        UpdatePreview();
    }

    private void SettingsWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        _previewTimer.Start();
        UpdatePreview();
    }

    private void SettingsWindow_OnClosed(object? sender, EventArgs e)
    {
        _previewTimer.Stop();
    }

    private void PreviewTimer_OnTick(object? sender, EventArgs e)
    {
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (PreviewMeterArea is null || ThresholdSlider is null)
        {
            return;
        }

        var snapshot = _monitor.Snapshot;
        var isRunning = snapshot.IsRunning;
        var level = isRunning ? Math.Clamp(snapshot.Level, 0f, 100f) : 0f;
        var threshold = Math.Clamp((float)ThresholdSlider.Value, 0f, 100f);
        var levelBrush = !isRunning
            ? (MediaBrush)FindResource("ControlBorderBrush")
            : level >= threshold
                ? (MediaBrush)FindResource("DangerBrush")
                : level >= MathF.Max(0f, threshold - 10f)
                    ? (MediaBrush)FindResource("WarningBrush")
                    : (MediaBrush)FindResource("NormalBrush");

        PreviewLevelFill.Fill = levelBrush;
        PreviewLevelFill.Width = Math.Max(0d, PreviewMeterArea.ActualWidth) * level / 100d;
        var meterWidth = Math.Max(0d, PreviewMeterArea.ActualWidth);
        PreviewThresholdMarker.Margin = new Thickness(
            Math.Clamp(meterWidth * threshold / 100d - 1d, 0d, Math.Max(0d, meterWidth - 2d)),
            0,
            0,
            0);

        PreviewStatusText.Text = !isRunning
            ? "Inicia la monitorización para ver el nivel en vivo."
            : snapshot.ErrorMessage is not null
                ? snapshot.ErrorMessage
                : snapshot.AlertState == AlertState.Pending
                    ? "Nivel sobre el umbral..."
                    : level >= threshold
                        ? "Nivel por encima del umbral."
                        : "Nivel dentro del rango.";
        PreviewStatusText.Foreground = snapshot.ErrorMessage is not null
            ? (MediaBrush)FindResource("DangerBrush")
            : (MediaBrush)FindResource("MutedTextBrush");
    }

    private void RestoreDefaultsButton_OnClick(object sender, RoutedEventArgs e)
    {
        LoadPreferences(MonitoringPreferences.Defaults, keepOnTop: true);
        RefreshDevices(null);
        StatusText.Text = "Valores iniciales cargados. Pulsa Guardar para aplicarlos.";
        StatusText.Foreground = (MediaBrush)FindResource("WarningBrush");
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadPreferences(out var preferences))
        {
            return;
        }

        var preferencesToSave = preferences with { MicrophoneId = SelectedDeviceId };

        try
        {
            if (TrySaveSettings(preferencesToSave))
            {
                DialogResult = true;
            }
            else
            {
                ShowValidationError("No se pudieron guardar los ajustes. Corrige el problema y vuelve a intentarlo.");
            }
        }
        catch (Exception exception)
        {
            ShowValidationError($"No se pudieron guardar los ajustes: {exception.Message}");
        }
    }

    private void LoadPreferences(MonitoringPreferences preferences, bool keepOnTop)
    {
        ThresholdSlider.Value = preferences.Threshold;
        DurationTextBox.Text = preferences.MinimumAlertDuration.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture);
        CooldownTextBox.Text = preferences.Cooldown.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture);
        BeepCheckBox.IsChecked = preferences.BeepEnabled;
        TopmostCheckBox.IsChecked = keepOnTop;
    }

    private bool TryReadPreferences(out MonitoringPreferences preferences)
    {
        preferences = MonitoringPreferences.Defaults;

        if (!int.TryParse(DurationTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var durationMilliseconds)
            || durationMilliseconds < (int)MonitoringPreferencesValidator.MinimumAlertDuration.TotalMilliseconds
            || durationMilliseconds > (int)MonitoringPreferencesValidator.MaximumAlertDuration.TotalMilliseconds)
        {
            ShowValidationError("La duración debe ser un número entero entre 100 y 3000 ms.");
            return false;
        }

        var cooldownParsed = double.TryParse(CooldownTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var cooldownSeconds)
            || double.TryParse(CooldownTextBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out cooldownSeconds);

        if (!cooldownParsed
            || !double.IsFinite(cooldownSeconds)
            || cooldownSeconds < MonitoringPreferencesValidator.MinimumCooldown.TotalSeconds
            || cooldownSeconds > MonitoringPreferencesValidator.MaximumCooldown.TotalSeconds)
        {
            ShowValidationError("El enfriamiento debe ser un número entre 0 y 30 segundos.");
            return false;
        }

        preferences = new MonitoringPreferences
        {
            Threshold = (float)ThresholdSlider.Value,
            MinimumAlertDuration = TimeSpan.FromMilliseconds(durationMilliseconds),
            Cooldown = TimeSpan.FromSeconds(cooldownSeconds),
            BeepEnabled = BeepCheckBox.IsChecked == true
        };

        if (!MonitoringPreferencesValidator.IsValid(preferences))
        {
            ShowValidationError("Los valores introducidos están fuera de rango.");
            return false;
        }

        return true;
    }

    private void ShowValidationError(string message)
    {
        StatusText.Text = message;
        StatusText.Foreground = (MediaBrush)FindResource("DangerBrush");
    }
}
