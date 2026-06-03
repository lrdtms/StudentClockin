namespace StudentLog.Application.Csv;

public sealed record CsvImportResult(
    IReadOnlyList<StudentImportRow> ValidRows,
    int InvalidRowCount);
