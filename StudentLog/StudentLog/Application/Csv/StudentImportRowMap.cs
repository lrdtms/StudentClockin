using CsvHelper.Configuration;

namespace StudentLog.Application.Csv;

public sealed class StudentImportRowMap : ClassMap<StudentImportRow>
{
    public StudentImportRowMap()
    {
        Map(r => r.Name).Name("name");
        Map(r => r.Surname).Name("surname");
        Map(r => r.UID).Name("uid");
    }
}
