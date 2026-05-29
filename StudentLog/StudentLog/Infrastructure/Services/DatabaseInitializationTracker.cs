using StudentLog.Application.Interfaces;

namespace StudentLog.Infrastructure.Services;

public sealed class DatabaseInitializationTracker : IDatabaseInitializationTracker
{
    private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitAsync(CancellationToken cancellationToken = default)
        => _tcs.Task.WaitAsync(cancellationToken);

    public void SignalComplete() => _tcs.TrySetResult();
}
