using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Setae.App.Audio;
using Setae.App.Infrastructure;
using Setae.App.Monitoring;
using MediaBrush = System.Windows.Media.Brush;

namespace Setae.App.UI;

public partial class MainWindow : Window
{
    private const double UiRefreshMilliseconds = 34;

    private readonly SettingsStore _settingsStore;
    private readonly AudioDeviceService _deviceService;
    private readonly AudioMonitor _monitor;
    private readonly BeepPlayer _beepPlayer;
    private readonly DispatcherTimer _uiTimer;

    private readonly MediaBrush _normalFillBrush;
    private readonly MediaBrush _warningFillBrush;
    private readonly MediaBrush _dangerFillBrush;
    private readonly MediaBrush _inactiveFillBrush;
    private readonly MediaBrush _normalBrush;
    private readonly MediaBrush _warningBrush;
    private readonly MediaBrush _dangerBrush;
    private readonly MediaBrush _mutedBrush;
    private readonly MediaBrush _dangerTextBrush;

    private AppSettings _settings;
    private MonitoringPreferences _runtimeMonitoring;
    private string? _deviceNotice;
    private string? _settingsNotice;
    private bool _loadingUi;
    private bool _settingsExpanded;
    private bool _exitRequested;
    private bool _disposed;
    private DispatcherTimer? _restoreNoticeTimer;

    public MainWindow(
        SettingsStore settingsStore,
        AudioDeviceService deviceService,
        AppSettings settings,
        string? settingsWarning)
    {
        InitializeComponent();

        _settingsStore = settingsStore;
        _deviceService = deviceService;
        _settings = settings;
        _runtimeMonitoring = settings.Monitoring;
        _settingsNotice = settingsWarning;

        _monitor = new AudioMonitor(deviceService);
        _monitor.AlertTriggered += Monitor_OnAlertTriggered;
        _beepPlayer = new BeepPlayer();

        _normalFillBrush = (MediaBrush)FindResource("NormalFillBrush");
        _warningFillBrush = (MediaBrush)FindResource("WarningFillBrush");
        _dangerFillBrush = (MediaBrush)FindResource("DangerFillBrush");
        _inactiveFillBrush = (MediaBrush)FindResource("InactiveFillBrush");
        _normalBrush = (MediaBrush)FindResource("NormalBrush");
        _warningBrush = (MediaBrush)FindResource("WarningBrush");
        _dangerBrush = (MediaBrush)FindResource("DangerBrush");
        _mutedBrush = (MediaBrush)FindResource("MutedTextBrush");
        _dangerTextBrush = (MediaBrush)FindResource("DangerBrush");

        _uiTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(UiRefreshMilliseconds)
        };
        _uiTimer.Tick += UiTimer_OnTick;

        Topmost = settings.Topmost;
        ApplySavedWindowLayout(settings);
        Loaded += Window_OnLoaded;
    }

    public void DisposeForApplicationExit()
    {
        if (_disposed)
        {
            return;
        }

        _exitRequested = true;
        PersistSettings();
        _uiTimer.Stop();
        Task.Run(() => _monitor.Dispose()).GetAwaiter().GetResult();
        _beepPlayer.Dispose();
        _disposed = true;
    }

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadPreferencesIntoUi();
        RefreshDevices();
        _uiTimer.Start();
        UpdateUi();
    }

    private void UiTimer_OnTick(object? sender, EventArgs e)
    {
        UpdateUi();
    }

    private void SettingsButton_OnClick(object sender, RoutedEventArgs e)
    {
        _settingsExpanded = !_settingsExpanded;
        SettingsPanel.Visibility = _settingsExpanded ? Visibility.Visible : Visibility.Collapsed;

        if (!_settingsExpanded)
        {
            PersistSettings();
        }

        UpdateUi();
    }

    private void MicrophoneComboBox_OnSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (!_loadingUi && IsLoaded)
        {
            ApplySettingsFromUi();
        }
    }

    private void ThresholdSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ThresholdValueText is not null)
        {
            ThresholdValueText.Text = $"{ThresholdSlider.Value:0}";
        }

        if (!_loadingUi && IsLoaded)
        {
            ApplySettingsFromUi();
        }
    }

    private void HysteresisTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loadingUi && IsLoaded)
        {
            ApplySettingsFromUi();
        }
    }

    private void HysteresisTextBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (TryParseMilliseconds(HysteresisTextBox.Text, out var milliseconds))
        {
            HysteresisTextBox.Text = milliseconds.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            HysteresisTextBox.Text = ((int)_runtimeMonitoring.MinimumAlertDuration.TotalMilliseconds)
                .ToString(CultureInfo.InvariantCulture);
            SetNotice("La histéresis debe ser un número entero entre 100 y 3000 ms.", isError: true);
        }
    }

    private void HysteresisTextBox_OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Keyboard.ClearFocus();
        }
    }

    private void CooldownTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loadingUi && IsLoaded)
        {
            ApplySettingsFromUi();
        }
    }

    private void CooldownTextBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (TryParseCooldownSeconds(CooldownTextBox.Text, out var seconds))
        {
            CooldownTextBox.Text = seconds.ToString("0.##", CultureInfo.InvariantCulture);
        }
        else
        {
            CooldownTextBox.Text = _runtimeMonitoring.Cooldown.TotalSeconds
                .ToString("0.##", CultureInfo.InvariantCulture);
            SetNotice("El enfriamiento debe ser un número entre 0 y 30 segundos.", isError: true);
        }
    }

    private void CooldownTextBox_OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Keyboard.ClearFocus();
        }
    }

    private void BeepCheckBox_OnToggled(object sender, RoutedEventArgs e)
    {
        if (!_loadingUi && IsLoaded)
        {
            ApplySettingsFromUi();
        }
    }

    private void TopmostCheckBox_OnToggled(object sender, RoutedEventArgs e)
    {
        if (!_loadingUi && IsLoaded)
        {
            ApplySettingsFromUi();
        }
    }

    private void RestoreDefaultsButton_OnClick(object sender, RoutedEventArgs e)
    {
        var defaults = MonitoringPreferences.Defaults;

        _loadingUi = true;
        ThresholdSlider.Value = defaults.Threshold;
        HysteresisTextBox.Text = ((int)defaults.MinimumAlertDuration.TotalMilliseconds)
            .ToString(CultureInfo.InvariantCulture);
        CooldownTextBox.Text = defaults.Cooldown.TotalSeconds
            .ToString("0.##", CultureInfo.InvariantCulture);
        BeepCheckBox.IsChecked = defaults.BeepEnabled;
        _loadingUi = false;

        ApplySettingsFromUi();
        ShowRestoreNotice();
    }

    private void ShowRestoreNotice()
    {
        RestoreNoticeText.Visibility = Visibility.Visible;

        _restoreNoticeTimer?.Stop();
        _restoreNoticeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _restoreNoticeTimer.Tick += (_, _) =>
        {
            _restoreNoticeTimer?.Stop();
            RestoreNoticeText.Visibility = Visibility.Collapsed;
        };
        _restoreNoticeTimer.Start();
    }

    private void LoadPreferencesIntoUi()
    {
        _loadingUi = true;
        ThresholdSlider.Value = _runtimeMonitoring.Threshold;
        HysteresisTextBox.Text = ((int)_runtimeMonitoring.MinimumAlertDuration.TotalMilliseconds)
            .ToString(CultureInfo.InvariantCulture);
        CooldownTextBox.Text = _runtimeMonitoring.Cooldown.TotalSeconds
            .ToString("0.##", CultureInfo.InvariantCulture);
        BeepCheckBox.IsChecked = _runtimeMonitoring.BeepEnabled;
        TopmostCheckBox.IsChecked = _settings.Topmost;
        _loadingUi = false;
    }

    private string? SelectedDeviceId => (MicrophoneComboBox.SelectedItem as AudioDeviceInfo)?.Id;

    private MonitoringPreferences BuildPreferencesFromUi()
    {
        var hysteresisMilliseconds = TryParseMilliseconds(HysteresisTextBox.Text, out var milliseconds)
            ? milliseconds
            : (int)_runtimeMonitoring.MinimumAlertDuration.TotalMilliseconds;
        var cooldownSeconds = TryParseCooldownSeconds(CooldownTextBox.Text, out var seconds)
            ? seconds
            : _runtimeMonitoring.Cooldown.TotalSeconds;

        return new MonitoringPreferences
        {
            MicrophoneId = SelectedDeviceId,
            Threshold = (float)ThresholdSlider.Value,
            MinimumAlertDuration = TimeSpan.FromMilliseconds(hysteresisMilliseconds),
            Cooldown = TimeSpan.FromSeconds(cooldownSeconds),
            BeepEnabled = BeepCheckBox.IsChecked == true
        };
    }

    private static bool TryParseMilliseconds(string? text, out int milliseconds)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out milliseconds)
            && milliseconds >= (int)MonitoringPreferencesValidator.MinimumAlertDuration.TotalMilliseconds
            && milliseconds <= (int)MonitoringPreferencesValidator.MaximumAlertDuration.TotalMilliseconds;
    }

    private static bool TryParseCooldownSeconds(string? text, out double seconds)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds)
            && double.IsFinite(seconds)
            && seconds >= MonitoringPreferencesValidator.MinimumCooldown.TotalSeconds
            && seconds <= MonitoringPreferencesValidator.MaximumCooldown.TotalSeconds;
    }

    private void ApplySettingsFromUi()
    {
        var preferences = BuildPreferencesFromUi();
        var microphoneChanged = preferences.MicrophoneId != _runtimeMonitoring.MicrophoneId;

        _runtimeMonitoring = preferences;
        _settings = _settings with
        {
            Monitoring = preferences,
            Topmost = TopmostCheckBox.IsChecked == true
        };
        Topmost = _settings.Topmost;

        if (_monitor.Snapshot.IsRunning)
        {
            if (microphoneChanged)
            {
                if (SelectedDeviceId is not null)
                {
                    _ = RestartMonitoringAsync();
                }
                else
                {
                    _ = StopMonitoringAsync();
                    _deviceNotice = "El micrófono seleccionado no está disponible. Conecta el dispositivo y vuelve a iniciar.";
                }
            }
            else
            {
                try
                {
                    _monitor.UpdatePreferences(preferences);
                }
                catch (Exception exception)
                {
                    SetNotice($"No se pudieron aplicar los ajustes: {exception.Message}", isError: true);
                }
            }
        }

        if (microphoneChanged)
        {
            RefreshDevices();
        }
        else
        {
            UpdateUi();
        }
    }

    private void RefreshDevices()
    {
        try
        {
            var devices = _deviceService.EnumerateCaptureDevices();
            var defaultDeviceId = _deviceService.GetDefaultCaptureDeviceId();
            var previousId = SelectedDeviceId ?? _runtimeMonitoring.MicrophoneId;

            var selection = devices.Any(device => device.Id == previousId)
                ? previousId
                : previousId is null
                    ? devices.FirstOrDefault(device => device.Id == defaultDeviceId)?.Id
                    : null;

            _loadingUi = true;
            MicrophoneComboBox.ItemsSource = devices;
            MicrophoneComboBox.SelectedValue = selection;
            _loadingUi = false;

            _deviceNotice = devices.Count == 0
                ? "No hay micrófonos de entrada activos. Conecta uno o revisa los permisos de Windows."
                : selection is null
                    ? "El micrófono guardado no está disponible. Selecciona otro dispositivo."
                    : null;

            if (selection is null && _monitor.Snapshot.IsRunning)
            {
                _ = StopMonitoringAsync();
            }
        }
        catch (Exception exception)
        {
            _settingsNotice = $"No se pudieron enumerar los micrófonos: {exception.Message}";
        }

        UpdateUi();
    }

    private async void StartStopButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_monitor.Snapshot.IsRunning)
        {
            await StopMonitoringAsync();
        }
        else
        {
            await StartMonitoringAsync();
        }
    }

    private async Task StartMonitoringAsync()
    {
        if (SelectedDeviceId is null)
        {
            SetNotice("Selecciona un micrófono de entrada antes de iniciar.", isError: true);
            return;
        }

        try
        {
            var preferences = _runtimeMonitoring with { MicrophoneId = SelectedDeviceId };
            await _monitor.StartAsync(SelectedDeviceId, preferences);
            _runtimeMonitoring = preferences;
            _deviceNotice = null;
            _settingsNotice = null;
        }
        catch (Exception exception)
        {
            SetNotice(DescribeStartError(exception), isError: true);
        }

        UpdateUi();
    }

    private async Task StopMonitoringAsync()
    {
        try
        {
            await _monitor.StopAsync();
        }
        catch (Exception exception)
        {
            SetNotice($"No se pudo detener la monitorización: {exception.Message}", isError: true);
        }

        UpdateUi();
    }

    private async Task RestartMonitoringAsync()
    {
        try
        {
            var preferences = _runtimeMonitoring with { MicrophoneId = SelectedDeviceId };
            await _monitor.RestartAsync(SelectedDeviceId!, preferences);
            _runtimeMonitoring = preferences;
            _deviceNotice = null;
            _settingsNotice = null;
        }
        catch (Exception exception)
        {
            SetNotice(DescribeStartError(exception), isError: true);
        }

        UpdateUi();
    }

    private void Monitor_OnAlertTriggered(object? sender, AlertTriggeredEventArgs e)
    {
        _beepPlayer.Play();
    }

    private void UpdateUi()
    {
        var snapshot = _monitor.Snapshot;
        var level = Math.Clamp(snapshot.Level, 0f, 100f);
        var threshold = _runtimeMonitoring.Threshold;
        ThresholdText.Text = $"Umbral {threshold:0}";

        var levelBrush = ResolveLevelBrush(snapshot, level, threshold);
        LevelFill.Fill = levelBrush;
        StatusBadge.BorderBrush = snapshot.ErrorMessage is not null ? _dangerBrush : levelBrush;
        StatusText.Foreground = snapshot.ErrorMessage is not null ? _dangerBrush : levelBrush;
        StatusText.Text = GetStatusText(snapshot, level, threshold);

        NoticeText.Text = BuildNoticeText(snapshot);
        NoticeText.Visibility = string.IsNullOrWhiteSpace(NoticeText.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
        NoticeText.Foreground = snapshot.ErrorMessage is not null || _settingsNotice is not null
            ? _dangerTextBrush
            : _mutedBrush;

        var meterWidth = Math.Max(0, MeterBar.ActualWidth);
        var markerX = Math.Clamp(meterWidth * threshold / 100d - 1d, 0d, Math.Max(0d, meterWidth - 2d));
        LevelFill.Width = meterWidth * level / 100d;
        ThresholdMarker.Margin = new Thickness(markerX, 0, 0, 0);

        ThresholdText.Text = $"Umbral {threshold:0}";
        var labelWidth = Math.Max(0, ThresholdText.ActualWidth);
        var labelX = Math.Clamp(markerX + 1d - labelWidth / 2d, 0d, Math.Max(0d, meterWidth - labelWidth));
        ThresholdText.Margin = new Thickness(labelX, 0, 0, 4);

        StartStopButton.Content = snapshot.IsRunning ? "Detener" : "Iniciar";
        StartStopButton.IsEnabled = snapshot.IsRunning || SelectedDeviceId is not null;
    }

    private MediaBrush ResolveLevelBrush(MonitorSnapshot snapshot, float level, float threshold)
    {
        if (!snapshot.IsRunning || snapshot.ErrorMessage is not null)
        {
            return _inactiveFillBrush;
        }

        if (level >= threshold)
        {
            return _dangerFillBrush;
        }

        return level >= MathF.Max(0f, threshold - 10f) ? _warningFillBrush : _normalFillBrush;
    }

    private static string GetStatusText(MonitorSnapshot snapshot, float level, float threshold)
    {
        if (snapshot.ErrorMessage is not null)
        {
            return "Error";
        }

        if (!snapshot.IsRunning)
        {
            return "Detenido";
        }

        if (snapshot.AlertState == AlertState.Pending)
        {
            return "Sobre el umbral...";
        }

        return level >= threshold ? "Demasiado alto" : "Monitorizando";
    }

    private void SetNotice(string message, bool isError)
    {
        if (isError)
        {
            _settingsNotice = message;
        }
        else
        {
            _deviceNotice = message;
        }

        UpdateUi();
    }

    private string BuildNoticeText(MonitorSnapshot snapshot)
    {
        var notices = new List<string>(capacity: 3);

        if (!string.IsNullOrWhiteSpace(snapshot.ErrorMessage))
        {
            notices.Add(snapshot.ErrorMessage!);
        }

        if (!string.IsNullOrWhiteSpace(_deviceNotice))
        {
            notices.Add(_deviceNotice!);
        }

        if (!string.IsNullOrWhiteSpace(_settingsNotice))
        {
            notices.Add(_settingsNotice!);
        }

        return string.Join(Environment.NewLine, notices);
    }

    private void Window_OnClosing(object? sender, CancelEventArgs e)
    {
        if (_exitRequested)
        {
            return;
        }

        _exitRequested = true;
        PersistSettings();
        System.Windows.Application.Current.Shutdown();
    }

    private void PersistSettings()
    {
        if (_disposed)
        {
            return;
        }

        var updatedSettings = _settings with
        {
            Monitoring = _runtimeMonitoring,
            WindowLeft = double.IsFinite(Left) ? Left : _settings.WindowLeft,
            WindowTop = double.IsFinite(Top) ? Top : _settings.WindowTop,
            Topmost = Topmost
        };

        try
        {
            _settingsStore.Save(updatedSettings);
            _settings = updatedSettings;
            _settingsNotice = null;
        }
        catch (Exception exception)
        {
            _settingsNotice = $"No se pudo guardar la configuración: {exception.Message}";
        }
    }

    private void ApplySavedWindowLayout(AppSettings settings)
    {
        if (settings.WindowLeft is double left
            && settings.WindowTop is double top
            && IsOnVisibleScreen(left, top))
        {
            Left = left;
            Top = top;
            WindowStartupLocation = WindowStartupLocation.Manual;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    private static bool IsOnVisibleScreen(double left, double top)
    {
        return left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
            && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight
            && top >= SystemParameters.VirtualScreenTop;
    }

    private static string DescribeStartError(Exception exception)
    {
        if (exception is UnauthorizedAccessException
            || exception.HResult == unchecked((int)0x80070005)
            || exception.Message.Contains("access", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("permiso", StringComparison.OrdinalIgnoreCase))
        {
            return "Acceso al micrófono bloqueado. Revisa Configuración > Privacidad y seguridad > Micrófono.";
        }

        return $"No se pudo iniciar la monitorización: {exception.Message} Comprueba que el micrófono siga conectado y vuelve a intentarlo.";
    }
}