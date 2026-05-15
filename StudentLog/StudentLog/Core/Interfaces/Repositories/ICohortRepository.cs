using StudentLog.Core.Models;

namespace StudentLog.Core.Interfaces.Repositories;

public interface ICohortRepository
{
    Task<IReadOnlyList<Cohort>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<int> AddAsync(Cohort cohort, CancellationToken cancellationToken = default);
    Task<Cohort?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
