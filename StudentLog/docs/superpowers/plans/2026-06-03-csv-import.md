# Bulk Student CSV Import Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a button to the Students page that lets users bulk-import students from a CSV file (Name, Surname, UID columns) into the currently selected cohort.

**Architecture:** Mirrors the existing `ICsvExportService` / `CsvExportService` pattern exactly. A new `ICsvImportService` interface lives in `Application/Interfaces`; the Windows implementation in `Platforms/Windows` uses `FileOpenPicker` + WinRT + CsvHelper to parse the file and return a `CsvImportResult`. The `StudentsViewModel` deduplicates against existing UIDs and calls `StudentService.AddStudentAsync` per valid row.

**Tech Stack:** .NET MAUI 10, CsvHelper 33.1.0 (already referenced), CommunityToolkit.Mvvm 8.4.0, Windows `FileOpenPicker` / `WinRT.Interop`.

---

## File Map

| Action | Path |
|---|---|
| Create | `StudentLog/Application/Csv/CsvImportResult.cs` |
| Create | `StudentLog/Application/Csv/StudentImportRowMap.cs` |
| Create | `StudentLog/Application/Interfaces/ICsvImportService.cs` |
| Create | `StudentLog/Platforms/Windows/CsvImportService.cs` |
| Modify | `StudentLog/MauiProgram.cs` |
| Modify | `StudentLog/UI/ViewModels/StudentsViewModel.cs` |
| Modify | `StudentLog/UI/Views/StudentsPage.xaml` |

---

### Task 1: Add StudentImportRow and CsvImportResult types

**Files:**
- Create: `StudentLog/Application/Csv/CsvImportResult.cs`

- [ ] **Step 1: Create the file**

```csharp
namespace StudentLog.Application.Csv;

public sealed class StudentImportRow
{
    public string Name { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public string UID { get; set; } = string.Empty;
}

public sealed record CsvImportResult(
    IReadOnlyList<StudentImportRow> ValidRows,
    int InvalidRowCount);
```

`StudentImportRow` must be a mutable class (not a positional record) so CsvHelper can populate it via property setters. `CsvImportResult` is a record because it is only ever constructed once by the service and read by the ViewModel.

- [ ] **Step 2: Build to verify**

```
dotnet build StudentLog/StudentLog.csproj -c Debug
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add StudentLog/Application/Csv/CsvImportResult.cs
git commit -m "feat: add StudentImportRow and CsvImportResult types"
```

---

### Task 2: Add CsvHelper ClassMap for import rows

**Files:**
- Create: `StudentLog/Application/Csv/StudentImportRowMap.cs`

- [ ] **Step 1: Create the file**

```csharp
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
```

The names are lowercase because the `CsvImportService` (Task 4) configures `PrepareHeaderForMatch` to lowercase both sides of the header comparison, making matching case-insensitive.

- [ ] **Step 2: Build to verify**

```
dotnet build StudentLog/StudentLog.csproj -c Debug
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add StudentLog/Application/Csv/StudentImportRowMap.cs
git commit -m "feat: add StudentImportRowMap CsvHelper class map"
```

---

### Task 3: Add ICsvImportService interface

**Files:**
- Create: `StudentLog/Application/Interfaces/ICsvImportService.cs`

- [ ] **Step 1: Create the file**

```csharp
using StudentLog.Application.Csv;

namespace StudentLog.Application.Interfaces;

public interface ICsvImportService
{
    Task<CsvImportResult?> PickAndParseAsync(CancellationToken cancellationToken = default);
}
```

Returns `null` when the user cancels the file picker (no-op). Throws on unrecoverable file/parse errors.

- [ ] **Step 2: Build to verify**

```
dotnet build StudentLog/StudentLog.csproj -c Debug
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add StudentLog/Application/Interfaces/ICsvImportService.cs
git commit -m "feat: add ICsvImportService interface"
```

---

### Task 4: Add Windows CsvImportService implementation

**Files:**
- Create: `StudentLog/Platforms/Windows/CsvImportService.cs`

- [ ] **Step 1: Create the file**

```csharp
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
```

Notes:
- `HeaderValidated = null` suppresses errors when the CSV has extra columns not in the map.
- `MissingFieldFound = null` suppresses errors when expected columns are absent (the row will just have empty strings, caught by the whitespace check).
- `PrepareHeaderForMatch` lowercases both CSV headers and the ClassMap names before comparing, enabling case-insensitive header matching.
- UIDs are uppercased to match the normalisation applied by `UpdateStudentAsync`.

- [ ] **Step 2: Build to verify**

```
dotnet build StudentLog/StudentLog.csproj -c Debug
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add StudentLog/Platforms/Windows/CsvImportService.cs
git commit -m "feat: add Windows CsvImportService implementation"
```

---

### Task 5: Register ICsvImportService in DI

**Files:**
- Modify: `StudentLog/MauiProgram.cs`

- [ ] **Step 1: Add the registration**

The existing `#if WINDOWS` block in `MauiProgram.cs` (around line 55) currently reads:

```csharp
#if WINDOWS
    builder.Services.AddTransient<ICsvExportService, CsvExportService>();
#endif
```

Add one line to become:

```csharp
#if WINDOWS
    builder.Services.AddTransient<ICsvExportService, CsvExportService>();
    builder.Services.AddTransient<ICsvImportService, CsvImportService>();
#endif
```

`using StudentLog.Application.Interfaces;` is already at the top of `MauiProgram.cs` — no new using needed.

- [ ] **Step 2: Build to verify**

```
dotnet build StudentLog/StudentLog.csproj -c Debug
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add StudentLog/MauiProgram.cs
git commit -m "feat: register ICsvImportService in DI"
```

---

### Task 6: Add ImportCsvCommand to StudentsViewModel

**Files:**
- Modify: `StudentLog/UI/ViewModels/StudentsViewModel.cs`

- [ ] **Step 1: Add using directive**

At the top of `StudentsViewModel.cs`, after the existing using statements, add:

```csharp
using StudentLog.Application.Csv;
```

- [ ] **Step 2: Add field**

After the existing `private readonly IDatabaseInitializationTracker _dbTracker;` field declaration, add:

```csharp
private readonly ICsvImportService _csvImportService;
```

- [ ] **Step 3: Update constructor**

Replace the existing constructor signature and body:

```csharp
public StudentsViewModel(
    IStudentService studentService,
    ICohortService cohortService,
    INfcService nfcService,
    IDialogService dialogService,
    ISoundService soundService,
    IDatabaseInitializationTracker dbTracker,
    ICsvImportService csvImportService)
{
    _studentService = studentService;
    _cohortService = cohortService;
    _nfcService = nfcService;
    _dialogService = dialogService;
    _soundService = soundService;
    _dbTracker = dbTracker;
    _csvImportService = csvImportService;
}
```

- [ ] **Step 4: Add ImportCsvCommand**

Add the following method after `AddStudentAsync`:

```csharp
[RelayCommand]
private async Task ImportCsvAsync()
{
    if (SelectedCohort is null)
    {
        StatusMessage = "Please select a cohort first.";
        return;
    }

    CsvImportResult? result;
    try
    {
        result = await _csvImportService.PickAndParseAsync();
    }
    catch (Exception ex)
    {
        StatusMessage = $"Import failed: {ex.Message}";
        return;
    }

    if (result is null)
        return;

    var existing = await _studentService.GetStudentsAsync(SelectedCohort.Id);
    var existingUids = new HashSet<string>(existing.Select(s => s.UID), StringComparer.OrdinalIgnoreCase);

    int imported = 0;
    int skipped = 0;
    int invalid = result.InvalidRowCount;

    foreach (var row in result.ValidRows)
    {
        if (existingUids.Contains(row.UID))
        {
            skipped++;
            continue;
        }

        try
        {
            await _studentService.AddStudentAsync(row.Name, row.Surname, row.UID, SelectedCohort.Id);
            existingUids.Add(row.UID);
            imported++;
        }
        catch (ArgumentException)
        {
            invalid++;
        }
    }

    await LoadAllStudentsForCohortAsync();

    var parts = new List<string>();
    if (imported > 0) parts.Add($"Imported {imported} student{(imported == 1 ? "" : "s")}");
    if (skipped > 0) parts.Add($"{skipped} duplicate{(skipped == 1 ? "" : "s")} skipped");
    if (invalid > 0) parts.Add($"{invalid} invalid row{(invalid == 1 ? "" : "s")}");
    StatusMessage = parts.Count > 0 ? string.Join(". ", parts) + "." : "Nothing to import.";
}
```

- [ ] **Step 5: Build to verify**

```
dotnet build StudentLog/StudentLog.csproj -c Debug
```

Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add StudentLog/UI/ViewModels/StudentsViewModel.cs
git commit -m "feat: add ImportCsvCommand to StudentsViewModel"
```

---

### Task 7: Add IMPORT CSV button to StudentsPage.xaml

**Files:**
- Modify: `StudentLog/UI/Views/StudentsPage.xaml`

- [ ] **Step 1: Replace the single ADD STUDENT button**

In `StudentsPage.xaml`, find this line (around line 32):

```xml
<Button Text="ADD STUDENT" Command="{Binding AddStudentCommand}" HorizontalOptions="Start"/>
```

Replace it with:

```xml
<Grid ColumnDefinitions="Auto,Auto" ColumnSpacing="8" HorizontalOptions="Start">
    <Button Grid.Column="0" Text="ADD STUDENT" Command="{Binding AddStudentCommand}" />
    <Button Grid.Column="1" Text="IMPORT CSV" Command="{Binding ImportCsvCommand}" Style="{StaticResource SecondaryButton}" />
</Grid>
```

- [ ] **Step 2: Build to verify**

```
dotnet build StudentLog/StudentLog.csproj -c Debug
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add StudentLog/UI/Views/StudentsPage.xaml
git commit -m "feat: add IMPORT CSV button to StudentsPage"
```

---

### Task 8: Smoke-test the full flow

- [ ] **Step 1: Create a test CSV file**

Save a file named `test_students.csv` on the Desktop with this content:

```
Name,Surname,UID
Alice,Smith,04AA11BB
Bob,Jones,04CC22DD
Charlie,Brown,
```

Row 3 (Charlie) has an empty UID and should be counted as invalid (not imported).

- [ ] **Step 2: Run the app**

```
dotnet run --project StudentLog/StudentLog.csproj -f net10.0-windows10.0.19041.0
```

- [ ] **Step 3: Test happy path**

1. Go to the **Students** page
2. Select a cohort from the picker
3. Click **IMPORT CSV** — Windows file picker should open
4. Select `test_students.csv`
5. Expected `StatusMessage`: `Imported 2 students. 1 invalid row.`
6. Alice Smith (UID `04AA11BB`) and Bob Jones (UID `04CC22DD`) should appear in the list

- [ ] **Step 4: Test duplicate handling**

1. Click **IMPORT CSV** again and select the same `test_students.csv`
2. Expected `StatusMessage`: `2 duplicates skipped. 1 invalid row.`
3. No new students added

- [ ] **Step 5: Test no-cohort guard**

1. Clear cohort selection (if the picker allows it) or navigate away and back without selecting a cohort
2. Click **IMPORT CSV** without a cohort selected
3. Expected `StatusMessage`: `Please select a cohort first.`
4. File picker should NOT open

- [ ] **Step 6: Test cancel**

1. Click **IMPORT CSV**, then cancel the file picker without selecting a file
2. Expected: `StatusMessage` unchanged, no students added
