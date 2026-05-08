using System.Globalization;

namespace StudentLog.UI.Converters;

public class SignInStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime dateTime)
        {
            return $"✓ {dateTime:HH:mm}";
        }

        return "◯ Not signed in";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
