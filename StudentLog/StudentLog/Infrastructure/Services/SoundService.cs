using Plugin.Maui.Audio;
using StudentLog.Application.Interfaces;
using StudentLog.Core.Models;

namespace StudentLog.Infrastructure.Services;

public class SoundService : ISoundService
{
    private readonly IAudioManager _audioManager;

    public SoundService(IAudioManager audioManager)
    {
        _audioManager = audioManager;
    }

    public async Task PlayAsync(Student student, Cohort cohort, SessionType sessionType)
    {
        var path = sessionType == SessionType.ClockIn
            ? (student.ClockInSoundPath ?? cohort.ClockInSoundPath)
            : (student.ClockOutSoundPath ?? cohort.ClockOutSoundPath);

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return;

        await using var stream = File.OpenRead(path);
        var player = _audioManager.CreateAsyncPlayer(stream);
        await player.PlayAsync(CancellationToken.None);
    }

    public async Task<string?> UploadSoundAsync()
    {
        var options = new PickOptions
        {
            PickerTitle = "Select a sound file",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".mp3", ".wav", ".m4a", ".ogg", ".flac" } },
                { DevicePlatform.macOS, new[] { "mp3", "wav", "m4a", "ogg", "flac" } },
            })
        };

        var result = await FilePicker.Default.PickAsync(options);
        if (result is null)
            return null;

        var soundsDir = Path.Combine(FileSystem.AppDataDirectory, "sounds");
        Directory.CreateDirectory(soundsDir);

        var destFileName = $"{Guid.NewGuid():N}-{Path.GetFileName(result.FileName)}";
        var destPath = Path.Combine(soundsDir, destFileName);

        await using var src = await result.OpenReadAsync();
        await using var dst = File.Create(destPath);
        await src.CopyToAsync(dst);

        return destPath;
    }
}
