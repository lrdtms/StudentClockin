namespace StudentLog.Application.Interfaces;

public interface IDatabaseInitializationTracker
{
    Task WaitAsync(CancellationToken cancellationToken = default);
    void SignalComplete();
}
