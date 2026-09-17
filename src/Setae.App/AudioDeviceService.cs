using NAudio.CoreAudioApi;
using System.Runtime.InteropServices;

namespace Setae.App;

public sealed class AudioDeviceService
{
    public IReadOnlyList<AudioDeviceInfo> EnumerateCaptureDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = new List<AudioDeviceInfo>();

        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            using (device)
            {
                devices.Add(new AudioDeviceInfo(device.ID, device.FriendlyName));
            }
        }

        return devices;
    }

    public string? GetDefaultCaptureDeviceId()
    {
        using var enumerator = new MMDeviceEnumerator();

        try
        {
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            return device.ID;
        }
        catch (NAudio.MmException)
        {
            return null;
        }
        catch (COMException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public MMDevice OpenCaptureDevice(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("El identificador del micrófono no puede estar vacío.", nameof(deviceId));
        }

        var enumerator = new MMDeviceEnumerator();

        try
        {
            return enumerator.GetDevice(deviceId);
        }
        finally
        {
            // MMDevice owns the COM endpoint; the enumerator is no longer needed.
            enumerator.Dispose();
        }
    }
}
