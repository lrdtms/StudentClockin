# Bulk Student Import via CSV

**Date:** 2026-06-03
**Status:** Approved

## Overview

Allow users to import multiple students at once from a CSV file on the Students page. The user selects a cohort from the existing picker, then clicks "IMPORT CSV" to open a file picker and load students in bulk.

## CSV Format

Headers are matched case-insensitively by name; column order does not matter. A header row is required.

```
Name,Surname,UID
John,Smith,04AB12CD
Jane,Doe,04EF34GH
```

Required columns: `Name`, `Surname`, `UID`. Rows where any of these are empty or whitespace are counted as invalid and skipped. Duplicate UIDs within the file (second occurrence) are also treated as duplicates and skipped.

## Architecture

### New files

| File | Purpose |
|---|---|
| `Application/Interfaces/ICsvImportService.cs` | Interface with one method: `Task<CsvImportResult?> PickAndParseAsync(CancellationToken)` |
| `Application/Csv/CsvImportResult.cs` | Result record: `IReadOnlyList<StudentImportRow> ValidRows`, `int InvalidRowCount` |
| `Application/Csv/StudentImportRowMap.cs` | CsvHelper `ClassMap<StudentImportRow>` mapping Name/Surname/UID headers |
| `Platforms/Windows/CsvImportService.cs` | Windows implementation: `FileOpenPicker` + WinRT init + CsvHelper parse |

### Modified files

| File | Change |
|---|---|
| `UI/ViewModels/StudentsViewModel.cs` | New `ImportCsvCommand` |
| `UI/Views/StudentsPage.xaml` | New "IMPORT CSV" button in the Add Student card |
| `MauiProgram.cs` | Register `ICsvImportService` → `CsvImportService` |

## Data Flow

1. User selects cohort from picker, clicks **IMPORT CSV**
2. ViewModel validates a cohort is selected; if not → `StatusMessage = "Please select a cohort first."`
3. Calls `ICsvImportService.PickAndParseAsync()` → opens `FileOpenPicker` filtered to `.csv`; returns `null` if user cancels (no-op)
4. ViewModel fetches all existing UIDs for the selected cohort into a `HashSet<string>` to detect duplicates without relying on DB constraints
5. For each valid row:
   - UID already in set → increment `skipCount`
   - Otherwise → call `StudentService.AddStudentAsync`, add UID to set, increment `importedCount`
6. Reload student list
7. Set `StatusMessage` e.g. `"Imported 8 students. Skipped 2 duplicates. 1 row invalid."`

## Error Handling

| Scenario | Behaviour |
|---|---|
| File picker cancelled | No-op, status unchanged |
| File read / parse exception | `StatusMessage = "Import failed: <message>"` |
| Row missing Name, Surname, or UID | Counted as invalid, skipped |
| UID duplicate (in DB or in file) | Counted as skipped duplicate |
| `ArgumentException` from `AddStudentAsync` | Counted as invalid (safety net) |

## UI

A secondary-styled **IMPORT CSV** button is added next to ADD STUDENT in the existing Add Student card:

```
[ ADD STUDENT ]  [ IMPORT CSV ]
```

No modal or progress indicator — imports for typical class sizes (10–50 students) complete instantly. The existing `StatusMessage` label reports the result.
