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

        // Strip UTF-8 BOM if StreamReader didn't already remove it
        if (content.Length > 0 && content[0] == '﻿')
            content = content[1..];

        // Detect delimiter and skip Excel's sep= hint line if present
        var firstNewline = content.IndexOf('\n');
        var firstLine    = (firstNewline >= 0 ? content[..firstNewline] : content).TrimEnd('\r');
        string delimiter;

        if (firstLine.StartsWith("sep=", StringComparison.OrdinalIgnoreCase))
        {
            var sep = firstLine["sep=".Length..].Trim();
            delimiter = sep.Length > 0 ? sep : ",";
            content   = firstNewline >= 0 ? content[(firstNewline + 1)..] : string.Empty;
        }
        else
        {
            var commas     = firstLine.Count(c => c == ',');
            var semicolons = firstLine.Count(c => c == ';');
            var tabs       = firstLine.Count(c => c == '\t');
            delimiter = semicolons >= commas && semicolons >= tabs ? ";"
                      : tabs >= commas                             ? "\t"
                      : ",";
        }

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

        var nameIdx    = TryGetIndex(headers, "name", "first name", "firstname");
        var surnameIdx = TryGetIndex(headers, "surname", "last name", "lastname", "family name");
        var uidIdx     = TryGetIndex(headers, "uid");

        if (nameIdx < 0 || surnameIdx < 0 || uidIdx < 0)
        {
            var found = string.Join(", ", csv.HeaderRecord!.Select(h => $"'{h}'"));
            throw new InvalidOperationException(
                $"CSV must have Name/First Name, Surname/Last Name and UID columns (found: {found})");
        }

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

    private static int TryGetIndex(Dictionary<string, int> headers, params string[] candidates)
    {
        foreach (var name in candidates)
            if (headers.TryGetValue(name, out int idx))
                return idx;
        return -1;
    }
}
