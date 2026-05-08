namespace StudentLog.Core.Models;

public class AttendanceRecord
{
    public int StudentId { get; set; }

    public string StudentName { get; set; } = string.Empty;

    public string StudentSurname { get; set; } = string.Empty;

    public DateTime? SignInTime { get; set; }

    public DateTime? SignOutTime { get; set; }

    public TimeSpan? Duration
    {
        get
        {
            if (SignInTime.HasValue && SignOutTime.HasValue)
            {
                return SignOutTime.Value - SignInTime.Value;
            }
            return null;
        }
    }

    public string FormattedSignInTime => SignInTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";

    public string FormattedSignOutTime => SignOutTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";

    public string FormattedDuration
    {
        get
        {
            if (Duration.HasValue)
            {
                return $"{Duration.Value.Hours}h {Duration.Value.Minutes}m";
            }
            return "In Progress";
        }
    }
}
