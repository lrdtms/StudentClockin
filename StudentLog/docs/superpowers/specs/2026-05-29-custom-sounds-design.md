# Custom Sound Upload — Design Spec

**Date:** 2026-05-29  
**Branch:** worktree-feature+sound-effects  
**Status:** Approved

## Overview

Replace the hardcoded `clockin.mp3` / `clockout.mp3` app-package sounds with a user-uploadable sound system. Sounds are configurable at two levels:

- **Cohort level** — a clock-in sound and a clock-out sound per cohort, set in the Cohorts tab
- **Student level** — optional override sounds per student, set inside the Edit Student modal

**Fallback chain:** student sound → cohort sound → silent (no error)

## Data Layer

### Database migrations

Two nullable `VARCHAR(500)` columns are added to the `cohort` and `student` tables via `ALTER TABLE … ADD COLUMN IF NOT EXISTS`. These run at the end of `DatabaseInitializer.InitializeAsync()`, after the existing `CREATE TABLE IF NOT EXISTS` blocks.

```sql
ALTER TABLE cohort
  ADD COLUMN IF NOT EXISTS ClockInSoundPath  VARCHAR(500) NULL,
  ADD COLUMN IF NOT EXISTS ClockOutSoundPath VARCHAR(500) NULL;

ALTER TABLE student
  ADD COLUMN IF NOT EXISTS ClockInSoundPath  VARCHAR(500) NULL,
  ADD COLUMN IF NOT EXISTS ClockOutSoundPath VARCHAR(500) NULL;
```

Both `ALTER TABLE` statements are idempotent — safe on first run and on subsequent app launches.

### Model changes

`Cohort` gains:
```csharp
public string? ClockInSoundPath  { get; set; }
public string? ClockOutSoundPath { get; set; }
```

`Student` gains the same two properties.

### Repository changes

- `CohortRepository`: all `SELECT` queries include the new columns; a new `UpdateAsync(Cohort)` method is added (cohorts currently have no update path).
- `StudentRepository`: existing `UpdateAsync` has its column list extended to include the two sound path columns.
- `ICohortRepository` gains `Task UpdateAsync(Cohort cohort)`.

### File storage

Uploaded sound files are copied into `FileSystem.AppDataDirectory/sounds/` on the local machine. The database stores only the full file path. Files are named with a GUID prefix to avoid collisions (e.g., `3f2a…-chime.mp3`).

## Service Layer

### ISoundService

```csharp
public interface ISoundService
{
    Task PlayAsync(Student student, Cohort cohort, SessionType sessionType);
    Task<string?> UploadSoundAsync();
}
```

`PlayAsync` resolves the fallback chain:
1. Select `student.ClockInSoundPath` or `student.ClockOutSoundPath` based on `sessionType`
2. If null, select the matching cohort path
3. If still null, return silently
4. Otherwise open a `FileStream` on the resolved path and play via `Plugin.Maui.Audio`

`UploadSoundAsync` calls `FilePicker.PickAsync` with an `PickOptions` allowing all audio extensions the plugin supports (`mp3`, `wav`, `m4a`, `ogg`, `flac`). On pick, the file is copied to the sounds folder. Returns the stored path, or `null` if the user cancelled or the pick failed.

### ISoundConfigService / SoundConfigService

```csharp
public interface ISoundConfigService
{
    Task SetCohortSoundAsync(int cohortId, SessionType type, string? path);
    Task SetStudentSoundAsync(int studentId, SessionType type, string? path);
}
```

`SoundConfigService` fetches the entity by ID, sets the appropriate path property, and calls the repository's `UpdateAsync`. Registered as `Singleton` in `MauiProgram`.

## ViewModel Layer

### CheckInSessionViewModel

No structural change. The sound call in the NFC scan callback changes from two separate `PlayClockInAsync`/`PlayClockOutAsync` calls to:

```csharp
_ = _soundService.PlayAsync(result.Student, SelectedCohort!, SelectedSessionType);
```

`result.Student` and `SelectedCohort` are already in scope at this call site.

### CohortsViewModel

New injected dependencies: `ISoundService`, `ISoundConfigService`.

Two new relay commands:

```csharp
[RelayCommand] async Task SetCohortClockInSoundAsync(Cohort cohort)
[RelayCommand] async Task SetCohortClockOutSoundAsync(Cohort cohort)
```

Each command:
1. Calls `_soundService.UploadSoundAsync()` — returns path or null
2. If path is non-null, calls `_soundConfigService.SetCohortSoundAsync(cohort.Id, type, path)`
3. Calls `LoadAsync()` to refresh the cohort list (so filename labels update)

### StudentsViewModel

New injected dependencies: `ISoundService`, `ISoundConfigService`.

Two new relay commands:

```csharp
[RelayCommand] async Task SetStudentClockInSoundAsync()
[RelayCommand] async Task SetStudentClockOutSoundAsync()
```

These operate on `SelectedStudentForEdit` (guaranteed non-null when the edit modal is open). Same upload → save → reload pattern.

## UI Layer

### CohortsPage.xaml — cohort row

The cohort card `Grid` column layout expands from `*,Auto` to `*,Auto,Auto,Auto`:

```
[ Name / sound labels ]  [ SOUND IN ]  [ SOUND OUT ]  [ DELETE ]
```

Below the cohort name, a small muted label shows current sound state:
- `"In: chime.mp3  Out: farewell.mp3"` when both are set
- `"In: chime.mp3"` / `"Out: ..."` individually
- Nothing shown when unset

### StudentsPage.xaml — edit modal

The existing edit modal (Name / Surname / UID / Save / Cancel) gains two rows below the UID entry:

```
[ UPLOAD CLOCK-IN SOUND ]    "chime.mp3"  (or empty if none)
[ UPLOAD CLOCK-OUT SOUND ]   "farewell.mp3"
```

Each row is a horizontal `Grid` with a button and a muted label showing `Path.GetFileName(storedPath)` — just the filename, not the full path. Modal height increases from `300` to `400` to accommodate.

## Registration (MauiProgram.cs)

```csharp
builder.Services.AddSingleton<ISoundConfigService, SoundConfigService>();
// ISoundService registration already present — only implementation changes
```

## Fallback Behaviour

| Student sound | Cohort sound | Result         |
|---------------|--------------|----------------|
| Set           | Any          | Student sound  |
| Not set       | Set          | Cohort sound   |
| Not set       | Not set      | Silent (no-op) |

## Out of Scope

- Deleting / clearing a previously uploaded sound (can be added later)
- Sound preview button in the UI
- Cross-machine portability of sound files (files live on local disk)
