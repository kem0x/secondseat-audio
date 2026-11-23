using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace SpeakerAudioMirror;

internal sealed record PlaybackDevice(string Id, string Name);

internal sealed record PlaybackDevices(PlaybackDevice? Primary, PlaybackDevice[] Available)
{
    internal static PlaybackDevices Read()
    {
        using var enumerator = new MMDeviceEnumerator();
        var available = new List<PlaybackDevice>();
        foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            using (device)
            {
                try
                {
                    available.Add(new PlaybackDevice(device.ID, device.FriendlyName));
                }
                catch (COMException)
                {
                    // The device can disappear between enumeration and reading its name.
                }
            }
        }

        PlaybackDevice? primary = null;
        if (enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
        {
            using MMDevice device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            primary = available.Find(candidate => candidate.Id == device.ID);
        }

        return new PlaybackDevices(primary,
            available.OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase).ToArray());
    }
}
