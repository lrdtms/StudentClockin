using Microsoft.Extensions.Logging;
using StudentLog.Application.Interfaces;
using Windows.Devices.Enumeration;
using Windows.Devices.SmartCards;

namespace StudentLog.Infrastructure.Services;

public class NfcService : INfcService, IAsyncDisposable
{
    private const int DefaultScanTimeoutSeconds = 10;

    private readonly ILogger<NfcService> _logger;

    private CancellationTokenSource? _listeningCts;
    private Task? _listeningTask;

    // Cached reader — resolved once per session; nulled when device watcher fires removal
    private SmartCardReader? _reader;
    private DeviceWatcher? _deviceWatcher;

    public bool IsListening { get; private set; }

    public NfcService(ILogger<NfcService> logger)
    {
        _logger = logger;
    }

    public Task StartListeningAsync(Func<string, Task> onUidScanned, CancellationToken cancellationToken = default)
    {
        try
        {
            _listeningCts?.Cancel();
            _listeningCts?.Dispose();
            _listeningCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            IsListening = true;

            _logger.LogInformation("[NFC] Starting listener");

            _listeningTask = Task.Run(async () =>
            {
                try
                {
                    await ResolveReaderAsync(_listeningCts.Token);
                    StartDeviceWatcher();

                    string? lastUid = null;
                    int consecutiveErrors = 0;
                    const int MaxConsecutiveErrors = 10;
                    const int ErrorDelayMs = 1000;
                    const int NormalDelayMs = 200;

                    while (!_listeningCts.IsCancellationRequested)
                    {
                        try
                        {
                            _logger.LogDebug("[NFC] Polling for cards...");

                            var uid = await TryReadUidFromReaderAsync(_listeningCts.Token);

                            if (!string.IsNullOrWhiteSpace(uid))
                            {
                                if (uid != lastUid)
                                {
                                    _logger.LogInformation("[NFC] New card detected - UID: {Uid}", uid);
                                    lastUid = uid;
                                    consecutiveErrors = 0;

                                    try
                                    {
                                        await onUidScanned(uid);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "[NFC] Error in onUidScanned callback");
                                    }
                                }
                                else
                                {
                                    _logger.LogDebug("[NFC] Card already scanned (duplicate)");
                                }
                            }
                            else
                            {
                                consecutiveErrors = 0;
                                if (_reader is null)
                                {
                                    _logger.LogWarning("[NFC] Reader lost — attempting re-discovery");
                                    await ResolveReaderAsync(_listeningCts.Token);
                                }
                            }

                            try
                            {
                                await Task.Delay(NormalDelayMs, _listeningCts.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.LogInformation("[NFC] Listening cancelled");
                            break;
                        }
                        catch (Exception ex)
                        {
                            consecutiveErrors++;
                            _logger.LogWarning(ex, "[NFC] Error during polling ({Count}/{Max})", consecutiveErrors, MaxConsecutiveErrors);

                            if (consecutiveErrors >= MaxConsecutiveErrors)
                            {
                                _logger.LogError("[NFC] Too many consecutive errors — stopping listener");
                                break;
                            }

                            try
                            {
                                await Task.Delay(ErrorDelayMs, _listeningCts.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[NFC] Fatal error in listener loop");
                }
                finally
                {
                    IsListening = false;
                    StopDeviceWatcher();
                    _logger.LogInformation("[NFC] Listener stopped");
                }
            }, _listeningCts.Token);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NFC] Error starting listener");
            IsListening = false;
            throw;
        }
    }

    public async Task StopListeningAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[NFC] Stopping listener");
            _listeningCts?.Cancel();

            if (_listeningTask is not null && !_listeningTask.IsCompleted)
            {
                try
                {
                    await _listeningTask.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning("[NFC] Listener did not stop within 2 s");
                }
                catch (OperationCanceledException) { }
                catch (AggregateException ex)
                    when (ex.InnerExceptions.All(e => e is OperationCanceledException or TaskCanceledException))
                {
                    // Expected cancellation — swallow
                }
            }

            _listeningCts?.Dispose();
            _listeningCts = null;
            _listeningTask = null;
            _reader = null;
            IsListening = false;
            _logger.LogInformation("[NFC] Listener stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NFC] Error stopping listener");
            IsListening = false;
        }
    }

    public async Task<string?> ScanSingleUidAsync(CancellationToken cancellationToken = default)
    {
        IsListening = true;
        try
        {
            if (_reader is null)
                await ResolveReaderAsync(cancellationToken);

            var uid = await TryReadUidFromReaderAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(uid) ? null : uid;
        }
        finally
        {
            IsListening = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _listeningCts?.Cancel();

        if (_listeningTask is not null)
        {
            try
            {
                await _listeningTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch { /* best-effort during disposal */ }
        }

        _listeningCts?.Dispose();
        _listeningCts = null;
        _listeningTask = null;
        StopDeviceWatcher();
        _reader = null;
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private async Task ResolveReaderAsync(CancellationToken cancellationToken)
    {
        try
        {
            var selector = SmartCardReader.GetDeviceSelector();
            var devices = await DeviceInformation.FindAllAsync(selector).AsTask(cancellationToken);

            foreach (var device in devices)
            {
                try
                {
                    var reader = await SmartCardReader.FromIdAsync(device.Id).AsTask(cancellationToken);
                    if (reader is not null &&
                        (reader.Name.Contains("ACR122", StringComparison.OrdinalIgnoreCase) ||
                         reader.Name.Contains("ACS", StringComparison.OrdinalIgnoreCase)))
                    {
                        _reader = reader;
                        _logger.LogInformation("[NFC] Reader resolved: {ReaderName}", _reader.Name);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[NFC] Error probing device");
                }
            }

            _logger.LogWarning("[NFC] No ACR122U/ACS reader found during discovery");
            _reader = null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NFC] Error in ResolveReaderAsync");
            _reader = null;
        }
    }

    private async Task<string?> TryReadUidFromReaderAsync(CancellationToken cancellationToken)
    {
        if (_reader is null)
            return null;

        try
        {
            var cards = await _reader.FindAllCardsAsync().AsTask(cancellationToken);
            if (cards.Count == 0)
                return null;

            var card = cards[0];
            using var connection = await card.ConnectAsync().AsTask(cancellationToken);

            var command = new byte[] { 0xFF, 0xCA, 0x00, 0x00, 0x00 };
            var requestBuffer = Windows.Security.Cryptography.CryptographicBuffer.CreateFromByteArray(command);
            var response = await connection.TransmitAsync(requestBuffer).AsTask(cancellationToken);
            Windows.Security.Cryptography.CryptographicBuffer.CopyToByteArray(response, out var responseBytes);

            if (responseBytes.Length >= 2)
            {
                var sw1 = responseBytes[^2];
                var sw2 = responseBytes[^1];
                if (sw1 == 0x90 && sw2 == 0x00 && responseBytes.Length > 2)
                {
                    var uidBytes = responseBytes[..^2];
                    var uid = Convert.ToHexString(uidBytes);
                    _logger.LogInformation("[NFC] Card detected - UID: {Uid}", uid);
                    return uid;
                }
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NFC] Error reading card");
            return null;
        }
    }

    private void StartDeviceWatcher()
    {
        var selector = SmartCardReader.GetDeviceSelector();
        _deviceWatcher = DeviceInformation.CreateWatcher(selector);
        _deviceWatcher.Removed += OnDeviceRemoved;
        _deviceWatcher.Start();
        _logger.LogInformation("[NFC] Device watcher started");
    }

    private void StopDeviceWatcher()
    {
        if (_deviceWatcher is null) return;
        try
        {
            _deviceWatcher.Removed -= OnDeviceRemoved;
            if (_deviceWatcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
                _deviceWatcher.Stop();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NFC] Error stopping device watcher");
        }
        finally
        {
            _deviceWatcher = null;
        }
    }

    private void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        _logger.LogWarning("[NFC] Device removed — clearing cached reader");
        _reader = null;
    }
}
