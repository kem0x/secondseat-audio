using System.Text.Json;

namespace SpeakerAudioMirror;

internal sealed class HeadphonePreference
{
    private const string FileName = "headphones.json";
    private static readonly string LocalData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private readonly string filePath;
    private readonly string? previousFilePath;

    internal HeadphonePreference() : this(Path.Combine(LocalData, Program.AppName),
        Path.Combine(LocalData, "Speaker Audio Mirror")) { }

    internal HeadphonePreference(string folder, string? previousFolder = null)
    {
        filePath = Path.Combine(folder, FileName);
        previousFilePath = previousFolder is null ? null : Path.Combine(previousFolder, FileName);
    }

    internal PlaybackDevice? Load() =>
        Read(filePath) ?? (previousFilePath is null ? null : Read(previousFilePath));

    private static PlaybackDevice? Read(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<PlaybackDevice>(File.ReadAllText(path)) is { } device
                && !string.IsNullOrWhiteSpace(device.Id) && !string.IsNullOrWhiteSpace(device.Name)
                ? device
                : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    internal bool Save(PlaybackDevice device)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            string pending = filePath + ".tmp";
            File.WriteAllText(pending, JsonSerializer.Serialize(device));
            File.Move(pending, filePath, overwrite: true);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
