namespace StudentLog.Core.Models;

public class Student
{
    public int Id { get; set; }

    public string UID { get; set; } = string.Empty;

    public int CohortId { get; set; }

    public DateTime? SignInTime { get; set; }

    public DateTime? SignOutTime { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Surname { get; set; } = string.Empty;
}
