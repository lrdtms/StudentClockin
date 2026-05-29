using StudentLog.Core.Models;

namespace StudentLog.Application.Interfaces;

public interface ISoundConfigService
{
    Task SetCohortSoundAsync(int cohortId, SessionType type, string? path, CancellationToken cancellationToken = default);
}
