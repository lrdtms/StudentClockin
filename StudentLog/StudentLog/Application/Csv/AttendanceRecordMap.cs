using CsvHelper.Configuration;
using StudentLog.Core.Models;

namespace StudentLog.Application.Csv;

public sealed class AttendanceRecordMap : ClassMap<AttendanceRecord>
{
    public AttendanceRecordMap()
    {
        Map(r => r.StudentName).Name("First Name").Index(0);
        Map(r => r.StudentSurname).Name("Last Name").Index(1);
        Map(r => r.SignInTime).Name("Sign In").Index(2)
            .Convert(args => args.Value.SignInTime?.ToString("dd/MM/yyyy HH:mm") ?? "N/A");
        Map(r => r.SignOutTime).Name("Sign Out").Index(3)
            .Convert(args => args.Value.SignOutTime?.ToString("dd/MM/yyyy HH:mm") ?? "N/A");
        Map(r => r.FormattedDuration).Name("Duration").Index(4);
    }
}
