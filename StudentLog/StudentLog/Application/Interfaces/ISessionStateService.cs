using StudentLog.Core.Models;

namespace StudentLog.Application.Interfaces;

public interface ISessionStateService
{
    SessionType SessionType { get; }
    int? ActiveCohortId { get; }
    DateOnly? ActiveDay { get; }
    bool IsSessionActive { get; }

    void StartSession(int cohortId, SessionType sessionType, DateOnly day);
    void StopSession();
}
