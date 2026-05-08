using StudentLog.Application.Interfaces;
using StudentLog.Core.Models;

namespace StudentLog.Application.Services;

public class SessionStateService : ISessionStateService
{
    public SessionType SessionType { get; private set; } = SessionType.ClockIn;
    public int? ActiveCohortId { get; private set; }
    public DateOnly? ActiveDay { get; private set; }
    public bool IsSessionActive { get; private set; }

    public void StartSession(int cohortId, SessionType sessionType, DateOnly day)
    {
        ActiveCohortId = cohortId;
        SessionType = sessionType;
        ActiveDay = day;
        IsSessionActive = true;
    }

    public void StopSession()
    {
        IsSessionActive = false;
        ActiveCohortId = null;
        ActiveDay = null;
    }
}
