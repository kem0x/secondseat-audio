using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SpeakerAudioMirror;

internal sealed class AudioMirror : IDisposable
{
    private readonly SpeakerCapture capture;
    private readonly WasapiOut playback;
    private volatile bool playbackEnded;

    internal AudioMirror(string sourceId, string targetId)
    {
        if (sourceId == targetId)
            throw new ArgumentException("The speaker and headphone outputs must be different.");

        using var devices = new MMDeviceEnumerator();
        using var source = devices.GetDevice(sourceId);
        using var target = devices.GetDevice(targetId);

        capture = new SpeakerCapture(source);
        try
        {
            playback = new WasapiOut(target, AudioClientShareMode.Shared, true, 30);
            try
            {
                var buffer = new MirrorBuffer(capture.WaveFormat);
                capture.DataAvailable += (_, audio) => buffer.Add(audio.Buffer, audio.BytesRecorded);
                playback.PlaybackStopped += (_, _) => playbackEnded = true;
                playback.Init(buffer);
                capture.StartRecording();
                playback.Play();
            }
            catch
            {
                playback.Dispose();
                throw;
            }
        }
        catch
        {
            capture.Dispose();
            throw;
        }

        SourceId = sourceId;
        TargetId = targetId;
    }

    internal string SourceId { get; }
    internal string TargetId { get; }

    internal bool IsRunning => !playbackEnded && playback.PlaybackState == PlaybackState.Playing
        && capture.CaptureState is CaptureState.Starting or CaptureState.Capturing;

    internal static bool IsDeviceError(Exception error) =>
        error is COMException or InvalidOperationException or NotSupportedException or ArgumentException;

    public void Dispose()
    {
        try { capture.Dispose(); }
        finally { playback.Dispose(); }
    }
}

internal sealed class SpeakerCapture(MMDevice endpoint) : WasapiCapture(endpoint, true, 25)
{
    protected override AudioClientStreamFlags GetAudioClientStreamFlags() => AudioClientStreamFlags.Loopback;
}

internal sealed class MirrorBuffer : IWaveProvider
{
    private readonly BufferedWaveProvider buffer;
    private readonly byte[] discarded;
    private readonly object sync = new();

    internal MirrorBuffer(WaveFormat format)
    {
        int maxFrames = format.SampleRate * 80 / 1000;
        discarded = new byte[maxFrames * format.BlockAlign];
        buffer = new BufferedWaveProvider(format) { BufferLength = discarded.Length, ReadFully = false };
    }

    public WaveFormat WaveFormat => buffer.WaveFormat;

    internal void Add(byte[] samples, int count)
    {
        lock (sync)
        {
            int start = Math.Max(0, count - buffer.BufferLength);
            int bytesToAdd = count - start;
            int overflow = buffer.BufferedBytes + bytesToAdd - buffer.BufferLength;
            if (overflow > 0)
                buffer.Read(discarded, 0, overflow);

            buffer.AddSamples(samples, start, bytesToAdd);
        }
    }

    public int Read(byte[] destination, int offset, int count)
    {
        lock (sync)
        {
            int read = buffer.Read(destination, offset, count);
            Array.Clear(destination, offset + read, count - read);
            return count;
        }
    }
}

internal static class TestTone
{
    internal const double FirstFrequency = 700;
    internal const double SecondFrequency = 950;
    private const int SampleRate = 48000;

    internal static WasapiOut Start(string sourceId, EventHandler<StoppedEventArgs> completed)
    {
        using var devices = new MMDeviceEnumerator();
        using var source = devices.GetDevice(sourceId);
        var player = new WasapiOut(source, AudioClientShareMode.Shared, true, 30);
        try
        {
            player.PlaybackStopped += completed;
            ISampleProvider first = Note(FirstFrequency);
            ISampleProvider second = Note(SecondFrequency);
            var silence = new SignalGenerator(SampleRate, 2) { Gain = 0 };
            player.Init(first.FollowedBy(TimeSpan.FromMilliseconds(90), second)
                .FollowedBy(silence.Take(TimeSpan.FromMilliseconds(200))).ToWaveProvider());
            player.Play();
            return player;
        }
        catch
        {
            player.Dispose();
            throw;
        }
    }

    private static ISampleProvider Note(double frequency)
    {
        var signal = new SignalGenerator(SampleRate, 2) { Frequency = frequency, Gain = 0.045 };
        var fade = new FadeInOutSampleProvider(signal, initiallySilent: true);
        fade.BeginFadeIn(15);
        return fade.Take(TimeSpan.FromMilliseconds(230));
    }
}
