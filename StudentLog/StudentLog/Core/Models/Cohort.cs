namespace StudentLog.Core.Models;

public class Cohort
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? ClockInSoundPath { get; set; }

    public string? ClockOutSoundPath { get; set; }
}
