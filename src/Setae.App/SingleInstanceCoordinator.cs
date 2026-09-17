using System.IO.Pipes;
using System.Text;
using System.IO;

namespace Setae.App;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly string _pipeName;
    private readonly CancellationTokenSource _cancellation = new();
    private Action? _activate;
    private Task? _listenTask;
    private int _disposed;

    public SingleInstanceCoordinator(string pipeName)
    {
        _pipeName = pipeName;
    }

    public static void SignalExistingInstance(string pipeName)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            client.Connect(timeout: 500);
            var command = Encoding.UTF8.GetBytes("show\n");
            client.Write(command, 0, command.Length);
            client.Flush();
        }
        catch (IOException)
        {
            // The primary instance may be exiting between the mutex and pipe checks.
        }
        catch (TimeoutException)
        {
            // The primary instance is not ready to receive the activation signal.
        }
        catch (UnauthorizedAccessException)
        {
            // The primary instance may have a different pipe security context.
        }
    }

    public void Start(Action activate)
    {
        ArgumentNullException.ThrowIfNull(activate);
        _activate = activate;
        _listenTask = ListenAsync();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _cancellation.Cancel();
        try
        {
            _listenTask?.Wait(TimeSpan.FromMilliseconds(500));
        }
        catch (AggregateException)
        {
            // There is no persistent logging in the MVP.
        }

        if (_listenTask?.IsCompleted == true)
        {
            _cancellation.Dispose();
        }
    }

    private async Task ListenAsync()
    {
        try
        {
            while (!_cancellation.IsCancellationRequested)
            {
                using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(_cancellation.Token).ConfigureAwait(false);
                using var reader = new StreamReader(
                    server,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: 128,
                    leaveOpen: true);
                _ = await reader.ReadLineAsync(_cancellation.Token).ConfigureAwait(false);

                if (!_cancellation.IsCancellationRequested)
                {
                    _activate?.Invoke();
                }
            }
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
        }
        catch (IOException) when (_cancellation.IsCancellationRequested)
        {
        }
    }
}
