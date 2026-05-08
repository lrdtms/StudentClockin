namespace StudentLog.Application.Interfaces;

public interface INfcService
{
    bool IsListening { get; }
    Task StartListeningAsync(Func<string, Task> onUidScanned, CancellationToken cancellationToken = default);
    Task StopListeningAsync(CancellationToken cancellationToken = default);
    Task<string?> ScanSingleUidAsync(CancellationToken cancellationToken = default);
}
