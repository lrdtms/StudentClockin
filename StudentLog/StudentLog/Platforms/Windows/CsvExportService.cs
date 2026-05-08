using CsvHelper;
using CsvHelper.Configuration;
using StudentLog.Application.Csv;
using StudentLog.Application.Interfaces;
using StudentLog.Core.Models;
using System.Globalization;
using Windows.Storage.Pickers;

namespace StudentLog.Platforms.Windows;

public class CsvExportService : ICsvExportService
{
    public async Task<bool> ExportAttendanceAsync(
        IReadOnlyList<AttendanceRecord> records,
        string suggestedFileName,
        CancellationToken cancellationToken = default)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedFileName,
        };
        picker.FileTypeChoices.Add("CSV File", [".csv"]);

        var nativeWindow = (Microsoft.Maui.MauiWinUIWindow)Microsoft.Maui.Controls.Application.Current!.Windows[0].Handler!.PlatformView!;
        WinRT.Interop.InitializeWithWindow.Initialize(picker, nativeWindow.WindowHandle);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
            return false;

        cancellationToken.ThrowIfCancellationRequested();

        await using var stream = await file.OpenStreamForWriteAsync();
        stream.SetLength(0);

        // UTF-8 BOM so Excel recognises the encoding on double-click
        await using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        // sep= hint tells Excel which delimiter to use, overriding regional settings
        await writer.WriteLineAsync("sep=,");

        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { NewLine = "\r\n" };
        await using var csv = new CsvWriter(writer, config);

        csv.Context.RegisterClassMap<AttendanceRecordMap>();
        await csv.WriteRecordsAsync(records, cancellationToken);

        return true;
    }
}
