using StudentLog.Application.Interfaces;
using StudentLog.Core.Models;

namespace StudentLog.Application.Services;

public class SessionStateService : ISessionStateService
{
    private readonly Lock _lock = new();

    private SessionType _sessionType = SessionType.ClockIn;
    private int? _activeCohortId;
    private DateOnly? _activeDay;
    private bool _isSessionActive;

    public SessionType SessionType
    {
        get { lock (_lock) { return _sessionType; } }
        private set { lock (_lock) { _sessionType = value; } }
    }

    public int? ActiveCohortId
    {
        get { lock (_lock) { return _activeCohortId; } }
        private set { lock (_lock) { _activeCohortId = value; } }
    }

    public DateOnly? ActiveDay
    {
        get { lock (_lock) { return _activeDay; } }
        private set { lock (_lock) { _activeDay = value; } }
    }

    public bool IsSessionActive
    {
        get { lock (_lock) { return _isSessionActive; } }
        private set { lock (_lock) { _isSessionActive = value; } }
    }

    public void StartSession(int cohortId, SessionType sessionType, DateOnly day)
    {
        lock (_lock)
        {
            _activeCohortId = cohortId;
            _sessionType = sessionType;
            _activeDay = day;
            _isSessionActive = true;
        }
    }

    public void StopSession()
    {
        lock (_lock)
        {
            _isSessionActive = false;
            _activeCohortId = null;
            _activeDay = null;
        }
    }
}
