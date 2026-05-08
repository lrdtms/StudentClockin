namespace StudentLog.UI.Messaging;

public sealed class AttendanceRecordedMessage
{
    public AttendanceRecordedMessage(int cohortId, DateTime timestamp)
    {
        CohortId = cohortId;
        Timestamp = timestamp;
    }

    public int CohortId { get; }

    public DateTime Timestamp { get; }
}
