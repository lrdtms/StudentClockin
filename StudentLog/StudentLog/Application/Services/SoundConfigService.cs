using StudentLog.Application.Interfaces;
using StudentLog.Core.Interfaces.Repositories;
using StudentLog.Core.Models;

namespace StudentLog.Application.Services;

public class SoundConfigService : ISoundConfigService
{
    private readonly ICohortRepository _cohortRepository;

    public SoundConfigService(ICohortRepository cohortRepository)
    {
        _cohortRepository = cohortRepository;
    }

    public async Task SetCohortSoundAsync(int cohortId, SessionType type, string? path, CancellationToken cancellationToken = default)
    {
        var cohort = await _cohortRepository.GetByIdAsync(cohortId, cancellationToken);
        if (cohort is null) return;

        if (type == SessionType.ClockIn)
            cohort.ClockInSoundPath = path;
        else
            cohort.ClockOutSoundPath = path;

        await _cohortRepository.UpdateAsync(cohort, cancellationToken);
    }
}
