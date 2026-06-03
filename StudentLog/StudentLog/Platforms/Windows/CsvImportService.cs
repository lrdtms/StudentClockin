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

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
            PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant(),
        };

        using var csv = new CsvReader(reader, config);
        csv.Context.RegisterClassMap<StudentImportRowMap>();

        var validRows = new List<StudentImportRow>();
        int invalidCount = 0;

        await foreach (var row in csv.GetRecordsAsync<StudentImportRow>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(row.Name) ||
                string.IsNullOrWhiteSpace(row.Surname) ||
                string.IsNullOrWhiteSpace(row.UID))
            {
                invalidCount++;
                continue;
            }

            validRows.Add(new StudentImportRow
            {
                Name = row.Name.Trim(),
                Surname = row.Surname.Trim(),
                UID = row.UID.Trim().ToUpperInvariant()
            });
        }

        return new CsvImportResult(validRows, invalidCount);
    }
}
