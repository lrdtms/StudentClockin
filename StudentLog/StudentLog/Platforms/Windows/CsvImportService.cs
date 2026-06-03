using CsvHelper;
using CsvHelper.Configuration;
using StudentLog.Application.Csv;
using StudentLog.Application.Interfaces;
using System.Globalization;
using Windows.Storage.Pickers;

namespace StudentLog.Platforms.Windows;

public class CsvImportService : ICsvImportService
{
    public async Task<CsvImportResult?> PickAndParseAsync(CancellationToken cancellationToken = default)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add(".csv");

        var nativeWindow = (Microsoft.Maui.MauiWinUIWindow)Microsoft.Maui.Controls.Application.Current!.Windows[0].Handler!.PlatformView!;
        WinRT.Interop.InitializeWithWindow.Initialize(picker, nativeWindow.WindowHandle);

        var file = await picker.PickSingleFileAsync();
        if (file is null)
            return null;

        cancellationToken.ThrowIfCancellationRequested();

        await using var stream = await file.OpenStreamForReadAsync();
        using var reader = new StreamReader(stream);

        var content = await reader.ReadToEndAsync(cancellationToken);

        // Excel saves CSVs with semicolons in many locales — detect from first line
        var firstNewline = content.IndexOf('\n');
        var firstLine = firstNewline >= 0 ? content[..firstNewline] : content;
        var delimiter = firstLine.Contains(';') ? ";" : ",";

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            HeaderValidated = null,
            MissingFieldFound = null,
        };

        using var stringReader = new StringReader(content);
        using var csv = new CsvReader(stringReader, config);

        await csv.ReadAsync();
        csv.ReadHeader();

        var headers = csv.HeaderRecord!
            .Select((h, i) => (Header: h.Trim().ToLowerInvariant(), Index: i))
            .ToDictionary(x => x.Header, x => x.Index);

        if (!headers.TryGetValue("name", out int nameIdx) ||
            !headers.TryGetValue("surname", out int surnameIdx) ||
            !headers.TryGetValue("uid", out int uidIdx))
            return new CsvImportResult([], 0);

        var validRows = new List<StudentImportRow>();
        int invalidCount = 0;

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var name    = csv.GetField(nameIdx)    ?? string.Empty;
            var surname = csv.GetField(surnameIdx) ?? string.Empty;
            var uid     = csv.GetField(uidIdx)     ?? string.Empty;

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(surname) ||
                string.IsNullOrWhiteSpace(uid))
            {
                invalidCount++;
                continue;
            }

            validRows.Add(new StudentImportRow
            {
                Name    = name.Trim(),
                Surname = surname.Trim(),
                UID     = uid.Trim().ToUpperInvariant()
            });
        }

        return new CsvImportResult(validRows, invalidCount);
    }
}
