using Microsoft.Win32;

namespace StudentLog.Platforms.Windows;

internal static class WindowsSoundSuppressor
{
    private const string SoundEventsKey = @"AppEvents\Schemes\Apps\.Default";
    private static string? _savedConnect;
    private static string? _savedDisconnect;

    internal static void Suppress()
    {
        _savedConnect = ReadAndClear("DeviceConnect");
        _savedDisconnect = ReadAndClear("DeviceDisconnect");
    }

    internal static void Restore()
    {
        Write("DeviceConnect", _savedConnect);
        Write("DeviceDisconnect", _savedDisconnect);
    }

    private static string? ReadAndClear(string eventName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            $@"{SoundEventsKey}\{eventName}\.current", writable: true);
        if (key is null) return null;
        var value = key.GetValue(string.Empty) as string;
        key.SetValue(string.Empty, string.Empty);
        return value;
    }

    private static void Write(string eventName, string? value)
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            $@"{SoundEventsKey}\{eventName}\.current", writable: true);
        key?.SetValue(string.Empty, value ?? string.Empty);
    }
}
