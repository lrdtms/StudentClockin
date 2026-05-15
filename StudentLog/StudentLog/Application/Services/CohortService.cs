using StudentLog.Application.Interfaces;
using StudentLog.Core.Interfaces.Repositories;
using StudentLog.Core.Models;

namespace StudentLog.Application.Services;

public class CohortService : ICohortService
{
    private readonly ICohortRepository _cohortRepository;

    public CohortService(ICohortRepository cohortRepository)
    {
        _cohortRepository = cohortRepository;
    }

    public Task<IReadOnlyList<Cohort>> GetCohortsAsync(CancellationToken cancellationToken = default)
    {
        return _cohortRepository.GetAllAsync(cancellationToken);
    }

    public async Task<int> AddCohortAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Cohort name is required.", nameof(name));
        }

        return await _cohortRepository.AddAsync(new Cohort { Name = name.Trim() }, cancellationToken);
    }

    public Task DeleteCohortAsync(int id, CancellationToken cancellationToken = default)
        => _cohortRepository.DeleteAsync(id, cancellationToken);
}
