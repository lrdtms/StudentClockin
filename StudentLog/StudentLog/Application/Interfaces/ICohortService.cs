using StudentLog.Core.Models;

namespace StudentLog.Application.Interfaces;

public interface ICohortService
{
    Task<IReadOnlyList<Cohort>> GetCohortsAsync(CancellationToken cancellationToken = default);
    Task<int> AddCohortAsync(string name, CancellationToken cancellationToken = default);
    Task DeleteCohortAsync(int id, CancellationToken cancellationToken = default);
}
