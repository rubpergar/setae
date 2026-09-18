using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Forms;
using Setae.Core;
using MediaBrush = System.Windows.Media.Brush;

namespace Setae.App;

public partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore;
    private readonly AudioDeviceService _deviceService;
    private readonly AudioMonitor _monitor;
    private readonly BeepPlayer _beepPlayer;
    private readonly DispatcherTimer _uiTimer;
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _trayStartItem;
    private readonly ToolStripMenuItem _trayStopItem;
    private readonly ToolStripMenuItem _traySettingsItem;

    private AppSettings _settings;
    private MonitoringPreferences _runtimeMonitoring;
    private string? _selectedDeviceId;
    private string? _deviceNotice;
    private string? _settingsNotice;
    private bool _exitRequested;
    private bool _disposed;

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
        _beepPlayer.PlaybackFailed += BeepPlayer_OnPlaybackFailed;

        var tray = CreateTrayIcon();
        _trayIcon = tray.Icon;
        _trayStartItem = tray.Start;
        _trayStopItem = tray.Stop;
        _traySettingsItem = tray.Settings;

        _uiTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(34)
        };
        _uiTimer.Tick += UiTimer_OnTick;
        StateChanged += Window_OnStateChanged;

        Topmost = settings.Topmost;
        ApplySavedWindowLayout(settings);
        Loaded += Window_OnLoaded;
    }

    public void ShowFromTray()
    {
        if (_disposed)
        {
            return;
        }

        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Topmost = _settings.Topmost;
    }

    public void DisposeForApplicationExit()
    {
        if (_disposed)
        {
            return;
        }

        _exitRequested = true;
        SaveWindowLayout();
        _uiTimer.Stop();
        Task.Run(() => _monitor.Dispose()).GetAwaiter().GetResult();
        _beepPlayer.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _disposed = true;
    }

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshDevices();
        _uiTimer.Start();
        UpdateUi();
    }

    private void Window_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible && IsLoaded)
        {
            _uiTimer.Interval = TimeSpan.FromMilliseconds(34);
            _uiTimer.Start();
            UpdateUi();
        }
        else if (IsLoaded)
        {
            // Keep tray commands in sync without refreshing the hidden meter at 30 Hz.
            _uiTimer.Interval = TimeSpan.FromMilliseconds(500);
            _uiTimer.Start();
        }
    }

    private void UiTimer_OnTick(object? sender, EventArgs e)
    {
        UpdateUi();
    }

    private void RefreshDevices()
    {
        var previousId = _selectedDeviceId ?? _runtimeMonitoring.MicrophoneId;

        try
        {
            var devices = _deviceService.EnumerateCaptureDevices();
            var defaultDeviceId = _deviceService.GetDefaultCaptureDeviceId();

            _selectedDeviceId = devices.Any(device => device.Id == previousId)
                ? previousId
                : previousId is null
                    ? devices.FirstOrDefault(device => device.Id == defaultDeviceId)?.Id
                    : null;

            _deviceNotice = devices.Count == 0
                ? "No hay micrófonos de entrada activos. Conecta uno o revisa los permisos de Windows."
                : _selectedDeviceId is null
                    ? "El micrófono guardado no está disponible. Selecciona otro dispositivo."
                    : null;

            if (_selectedDeviceId is null && _monitor.Snapshot.IsRunning)
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
        if (_selectedDeviceId is null)
        {
            SetNotice("Selecciona un micrófono de entrada antes de iniciar.", isError: true);
            return;
        }

        try
        {
            var preferences = _runtimeMonitoring with { MicrophoneId = _selectedDeviceId };
            await _monitor.StartAsync(_selectedDeviceId, preferences);
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
            var preferences = _runtimeMonitoring with { MicrophoneId = _selectedDeviceId };
            await _monitor.RestartAsync(_selectedDeviceId!, preferences);
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

    private void SettingsButton_OnClick(object sender, RoutedEventArgs e)
    {
        OpenSettings();
    }

    private void OpenSettings()
    {
        ShowFromTray();

        var settingsWindow = new SettingsWindow(
            _deviceService,
            _monitor,
            _runtimeMonitoring,
            _selectedDeviceId ?? _runtimeMonitoring.MicrophoneId,
            _settings.Topmost)
        {
            Owner = this
        };
        settingsWindow.TrySaveSettings = preferences => TryApplySettings(preferences, settingsWindow.KeepOnTop);
        settingsWindow.ShowDialog();
    }

    private bool TryApplySettings(MonitoringPreferences preferences, bool topmost)
    {
        MonitoringPreferencesValidator.EnsureValid(preferences);
        var updatedSettings = _settings with
        {
            Monitoring = preferences,
            Topmost = topmost
        };

        try
        {
            _settingsStore.Save(updatedSettings);
        }
        catch (Exception exception)
        {
            _settingsNotice = $"No se pudieron guardar los ajustes: {exception.Message}";
            UpdateUi();
            return false;
        }

        var microphoneChanged = _runtimeMonitoring.MicrophoneId != preferences.MicrophoneId;
        _settings = updatedSettings;
        _runtimeMonitoring = preferences;
        _settingsNotice = null;
        Topmost = topmost;
        _selectedDeviceId = preferences.MicrophoneId;
        RefreshDevices();

        if (_monitor.Snapshot.IsRunning)
        {
            if (microphoneChanged && _selectedDeviceId is not null)
            {
                _ = RestartMonitoringAsync();
            }
            else if (microphoneChanged)
            {
                _ = StopMonitoringAsync();
                _deviceNotice = "El micrófono seleccionado no está disponible. Conecta el dispositivo y vuelve a iniciar.";
            }
            else
            {
                try
                {
                    _monitor.UpdatePreferences(_runtimeMonitoring);
                }
                catch (Exception exception)
                {
                    SetNotice($"No se pudieron aplicar los ajustes: {exception.Message}", isError: true);
                }
            }
        }

        UpdateUi();
        return true;
    }

    private void Monitor_OnAlertTriggered(object? sender, AlertTriggeredEventArgs e)
    {
        _beepPlayer.Play();
    }

    private void BeepPlayer_OnPlaybackFailed(Exception exception)
    {
        if (!Dispatcher.HasShutdownStarted)
        {
            Dispatcher.BeginInvoke(() => SetNotice($"No se pudo reproducir el pitido: {exception.Message}", isError: true));
        }
    }

    private void UpdateUi()
    {
        var snapshot = _monitor.Snapshot;
        var level = Math.Clamp(snapshot.Level, 0f, 100f);
        ThresholdText.Text = $"Umbral {_runtimeMonitoring.Threshold:0}";

        var levelBrush = snapshot.ErrorMessage is not null
            ? (MediaBrush)FindResource("DangerBrush")
            : level >= _runtimeMonitoring.Threshold
                ? (MediaBrush)FindResource("DangerBrush")
                : level >= MathF.Max(0f, _runtimeMonitoring.Threshold - 10f)
                    ? (MediaBrush)FindResource("WarningBrush")
                    : (MediaBrush)FindResource("NormalBrush");
        LevelFill.Fill = levelBrush;
        StatusText.Foreground = levelBrush;
        StatusBadge.BorderBrush = levelBrush;
        StatusText.Text = GetStatusText(snapshot, level, _runtimeMonitoring.Threshold);
        NoticeText.Text = BuildNoticeText(snapshot);
        NoticeText.Foreground = snapshot.ErrorMessage is not null || _settingsNotice is not null
            ? (MediaBrush)FindResource("DangerBrush")
            : (MediaBrush)FindResource("MutedTextBrush");

        var meterWidth = Math.Max(0, MeterArea.ActualWidth);
        LevelFill.Width = meterWidth * level / 100d;
        ThresholdMarker.Margin = new Thickness(
            Math.Clamp(meterWidth * _runtimeMonitoring.Threshold / 100d - 1d, 0d, Math.Max(0d, meterWidth - 2d)),
            0,
            0,
            0);

        StartStopButton.Content = snapshot.IsRunning ? "Detener" : "Iniciar";
        StartStopButton.IsEnabled = snapshot.IsRunning || _selectedDeviceId is not null;
        _trayStartItem.Enabled = !snapshot.IsRunning && _selectedDeviceId is not null;
        _trayStopItem.Enabled = snapshot.IsRunning;
        _traySettingsItem.Enabled = true;
        _trayIcon.Text = snapshot.IsRunning ? $"Setae: {level:0}/100" : "Setae: detenido";
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

        if (level >= threshold)
        {
            return "Demasiado alto";
        }

        return level > 0f ? "Monitorizando" : "Escuchando...";
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

    private void Window_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsLoaded)
        {
            UpdateMeterGeometry();
        }
    }

    private void Window_OnLocationChanged(object? sender, EventArgs e)
    {
        // Layout is persisted on explicit exit, not on every drag/resize.
    }

    private void UpdateMeterGeometry()
    {
        var meterWidth = Math.Max(0, MeterArea.ActualWidth);
        var level = Math.Clamp(_monitor.Snapshot.Level, 0f, 100f);
        LevelFill.Width = meterWidth * level / 100d;
        ThresholdMarker.Margin = new Thickness(
            Math.Clamp(meterWidth * _runtimeMonitoring.Threshold / 100d - 1d, 0d, Math.Max(0d, meterWidth - 2d)),
            0,
            0,
            0);
    }

    private void Window_OnClosing(object? sender, CancelEventArgs e)
    {
        if (_exitRequested)
        {
            return;
        }

        e.Cancel = true;
        SaveWindowLayout();
        Hide();
    }

    private void SaveWindowLayout()
    {
        if (_disposed || !double.IsFinite(Width) || !double.IsFinite(Height))
        {
            return;
        }

        var updatedSettings = _settings with
        {
            Monitoring = _runtimeMonitoring,
            WindowLeft = double.IsFinite(Left) ? Left : _settings.WindowLeft,
            WindowTop = double.IsFinite(Top) ? Top : _settings.WindowTop,
            WindowWidth = Width,
            WindowHeight = Height,
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
            _settingsNotice = $"No se pudo guardar la posición de la ventana: {exception.Message}";
        }
    }

    private void ApplySavedWindowLayout(AppSettings settings)
    {
        Width = settings.WindowWidth;
        Height = settings.WindowHeight;

        if (settings.WindowLeft is double left
            && settings.WindowTop is double top
            && IsOnVisibleScreen(left, top, settings.WindowWidth, settings.WindowHeight))
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

    private static bool IsOnVisibleScreen(double left, double top, double width, double height)
    {
        return left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
            && left + width > SystemParameters.VirtualScreenLeft
            && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight
            && top + height > SystemParameters.VirtualScreenTop;
    }

    private (NotifyIcon Icon, ToolStripMenuItem Start, ToolStripMenuItem Stop, ToolStripMenuItem Settings) CreateTrayIcon()
    {
        var menu = new ContextMenuStrip();
        var showItem = new ToolStripMenuItem("Mostrar");
        var startItem = new ToolStripMenuItem("Iniciar");
        var stopItem = new ToolStripMenuItem("Detener");
        var settingsItem = new ToolStripMenuItem("Ajustes");
        var exitItem = new ToolStripMenuItem("Salir");

        showItem.Click += (_, _) => ShowFromTray();
        startItem.Click += async (_, _) => await StartMonitoringAsync();
        stopItem.Click += async (_, _) => await StopMonitoringAsync();
        settingsItem.Click += (_, _) => OpenSettings();
        exitItem.Click += (_, _) => ExitApplication();

        menu.Items.Add(showItem);
        menu.Items.Add(startItem);
        menu.Items.Add(stopItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        var icon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Setae: detenido",
            ContextMenuStrip = menu,
            Visible = true
        };
        icon.DoubleClick += (_, _) => ShowFromTray();
        return (icon, startItem, stopItem, settingsItem);
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        SaveWindowLayout();
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    private void Window_OnStateChanged(object? sender, EventArgs e)
    {
        if (!_exitRequested && !_disposed && WindowState == WindowState.Minimized)
        {
            Hide();
        }
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
