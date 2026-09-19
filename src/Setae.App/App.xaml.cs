using System.Threading;
using System.Windows;
using Setae.App.Audio;
using Setae.App.Infrastructure;
using Setae.App.UI;

namespace Setae.App;

public partial class App : System.Windows.Application
{
    private const string MutexName = "Local\\Setae.App.SingleInstance";

    private Mutex? _instanceMutex;
    private bool _ownsMutex;

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

                Shutdown();
                return;
            }

            var settingsStore = new SettingsStore();
            var loadResult = settingsStore.Load();
            var deviceService = new AudioDeviceService();
            var mainWindow = new MainWindow(settingsStore, deviceService, loadResult.Settings, loadResult.Warning);

            MainWindow = mainWindow;
            mainWindow.Show();
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
}
