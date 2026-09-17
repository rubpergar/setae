using System.Threading;
using System.Windows;

namespace Setae.App;

public partial class App : System.Windows.Application
{
    private const string MutexName = "Local\\Setae.App.SingleInstance";
    private const string PipeName = "Setae.App.Activate";

    private Mutex? _instanceMutex;
    private SingleInstanceCoordinator? _instanceCoordinator;
    private bool _ownsMutex;
    private bool _activationPending;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _instanceMutex = new Mutex(true, MutexName, out var createdNew);
            _ownsMutex = createdNew;

            if (!createdNew)
            {
                _instanceMutex.Dispose();
                _instanceMutex = null;

                for (var attempt = 0; attempt < 5; attempt++)
                {
                    SingleInstanceCoordinator.SignalExistingInstance(PipeName);
                    if (attempt < 4)
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(100));
                    }
                }

                Shutdown();
                return;
            }

            _instanceCoordinator = new SingleInstanceCoordinator(PipeName);
            _instanceCoordinator.Start(() =>
            {
                Dispatcher.BeginInvoke(new Action(HandleActivationRequest));
            });

            var settingsStore = new SettingsStore();
            var loadResult = settingsStore.Load();
            var deviceService = new AudioDeviceService();
            var mainWindow = new MainWindow(settingsStore, deviceService, loadResult.Settings, loadResult.Warning);

            MainWindow = mainWindow;
            mainWindow.Show();

            if (_activationPending)
            {
                _activationPending = false;
                Dispatcher.BeginInvoke(new Action(mainWindow.ShowFromTray));
            }
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                $"No se pudo iniciar Setae.\n\n{exception.Message}",
                "Setae",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (MainWindow is MainWindow mainWindow)
        {
            mainWindow.DisposeForApplicationExit();
        }

        _instanceCoordinator?.Dispose();

        if (_ownsMutex && _instanceMutex is not null)
        {
            try
            {
                _instanceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The process no longer owns the mutex.
            }
        }

        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private void HandleActivationRequest()
    {
        if (MainWindow is MainWindow mainWindow)
        {
            mainWindow.ShowFromTray();
        }
        else
        {
            _activationPending = true;
        }
    }
}
