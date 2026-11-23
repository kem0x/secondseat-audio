namespace SpeakerAudioMirror;

internal static class Program
{
    internal const string AppName = "SecondSeat Audio";
    internal const string Version = "0.1.0";

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var instance = new Mutex(true, @"Local\SecondSeatAudio", out bool firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show($"{AppName} is already open.", AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var window = new MirrorWindow();
        Application.Run(window);
    }
}
