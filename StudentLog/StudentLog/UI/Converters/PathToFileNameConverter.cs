using System.Globalization;

namespace StudentLog.UI.Converters;

public class PathToFileNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path))
            return string.Empty;

        var fileName = Path.GetFileName(path);

        // Strip the 32-char hex GUID prefix added during upload (e.g. "a1b2c3...-mysound.mp3")
        if (fileName.Length > 33 && fileName[32] == '-' && fileName[..32].All(Uri.IsHexDigit))
            return fileName[33..];

        return fileName;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
