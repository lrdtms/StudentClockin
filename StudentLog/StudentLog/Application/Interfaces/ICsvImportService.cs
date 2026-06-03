using StudentLog.Application.Csv;

namespace StudentLog.Application.Interfaces;

public interface ICsvImportService
{
    Task<CsvImportResult?> PickAndParseAsync(CancellationToken cancellationToken = default);
}
