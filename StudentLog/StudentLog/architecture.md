# StudentLog — Architecture Document

> Generated: 2026-05-08  
> Based on direct source inspection of all `.cs`, `.csproj`, and `.xaml` files in the project.

---

## Table of Contents

1. [Project Overview and Purpose](#1-project-overview-and-purpose)
2. [Technology Stack](#2-technology-stack)
3. [Solution and Project Structure](#3-solution-and-project-structure)
4. [Key Architectural Patterns and Decisions](#4-key-architectural-patterns-and-decisions)
5. [Data Models / Domain Entities](#5-data-models--domain-entities)
6. [Database Schema](#6-database-schema)
7. [Key Components and Their Responsibilities](#7-key-components-and-their-responsibilities)
8. [Data Flow](#8-data-flow)
9. [Configuration and Infrastructure Concerns](#9-configuration-and-infrastructure-concerns)
10. [Notable Conventions](#10-notable-conventions)

---

## 1. Project Overview and Purpose

StudentLog is a **Windows desktop application** for recording and reviewing student attendance using NFC card scanning. It targets educational institutions or training environments that issue NFC cards (specifically compatible with ACR122U USB smart card readers) to students.

### Core Capabilities

| Capability | Description |
|---|---|
| Cohort management | Create named cohorts (classes/groups) and assign students to them |
| Student management | Register students with name, surname, and NFC card UID; edit and delete records |
| NFC attendance sessions | Start a timed ClockIn or ClockOut session for a selected cohort; the app continuously polls the ACR122U reader and records attendance automatically on each tap |
| Attendance history | View a per-student time-series of sign-in/sign-out pairs with computed duration |
| CSV export | Save a student's full attendance history to a CSV file via a native Windows Save dialog |

### Target Platform

The project is authored as a **.NET MAUI Single Project** targeting `net10.0-windows10.0.19041.0`. Although the MAUI project scaffolding retains Android, iOS, and macCatalyst platform stubs, the NFC integration (`NfcService`) and CSV export (`CsvExportService`) use Windows-only APIs (`Windows.Devices.SmartCards`, `Windows.Storage.Pickers`), so the application is **functionally Windows-only** in its current state.

---

## 2. Technology Stack

| Layer | Technology | Version / Notes |
|---|---|---|
| UI framework | .NET MAUI | net10.0, single-project model |
| Language | C# 13 | Nullable reference types enabled, implicit usings |
| MVVM toolkit | CommunityToolkit.Mvvm | 8.4.0 — `ObservableObject`, `IAsyncRelayCommand`, `WeakReferenceMessenger` |
| Database driver | MySqlConnector | 2.4.0 — async-first ADO.NET connector |
| Database server | MySQL | Default connection targets `127.0.0.1:3307`, database `student_logDb` |
| CSV serialisation | CsvHelper | 33.1.0 — class map, invariant culture, UTF-8 BOM |
| NFC hardware | ACR122U (ACS) USB reader | Accessed via `Windows.Devices.SmartCards` WinRT API |
| XAML compiler | MauiXamlInflator SourceGen | Compile-time XAML source generation (faster startup, no runtime inflation) |
| Logging | Microsoft.Extensions.Logging.Debug | 10.0.0 — Debug sink only; all tracing uses `System.Diagnostics.Debug.WriteLine` |
| DI container | Microsoft.Extensions.DependencyInjection | Provided by the MAUI host builder |

---

## 3. Solution and Project Structure

There is a single `.csproj` (`StudentLog.csproj`) with no separate solution file discovered at the scanned paths. The logical folder layout inside the project root is:

```
StudentLog/
│
├── MauiProgram.cs                  # App composition root / DI wiring
├── App.xaml / App.xaml.cs          # Application lifecycle, DB initialisation on startup
├── AppShell.xaml / AppShell.xaml.cs# Shell navigation, flyout menu, route registration
│
├── Core/                           # Domain layer — no external dependencies
│   ├── Models/
│   │   ├── Cohort.cs
│   │   ├── Student.cs
│   │   ├── AttendanceRecord.cs
│   │   ├── AttendanceScanResult.cs
│   │   └── SessionType.cs          # enum: ClockIn | ClockOut
│   └── Interfaces/
│       └── Repositories/
│           ├── ICohortRepository.cs
│           └── IStudentRepository.cs
│
├── Application/                    # Use-case / service layer
│   ├── Interfaces/
│   │   ├── IAttendanceService.cs
│   │   ├── ICohortService.cs
│   │   ├── ICsvExportService.cs
│   │   ├── INfcService.cs
│   │   ├── ISessionStateService.cs
│   │   └── IStudentService.cs
│   ├── Services/
│   │   ├── AttendanceService.cs
│   │   ├── CohortService.cs
│   │   ├── SessionStateService.cs
│   │   └── StudentService.cs
│   └── Csv/
│       └── AttendanceRecordMap.cs  # CsvHelper column mapping
│
├── Infrastructure/                 # External system adapters
│   ├── Data/
│   │   ├── IDbConnectionFactory.cs
│   │   ├── MySqlConnectionFactory.cs
│   │   ├── MySqlOptions.cs         # Connection string configuration
│   │   └── DatabaseInitializer.cs  # CREATE TABLE IF NOT EXISTS + seed data
│   ├── Repositories/
│   │   ├── CohortRepository.cs
│   │   └── StudentRepository.cs
│   └── Services/
│       └── NfcService.cs           # Windows WinRT smart card implementation
│
├── Platforms/
│   ├── Windows/
│   │   ├── App.xaml / App.xaml.cs  # WinUI application entry
│   │   └── CsvExportService.cs     # Windows FileSavePicker implementation
│   ├── Android/                    # Stub — not implemented
│   ├── iOS/                        # Stub — not implemented
│   └── MacCatalyst/                # Stub — not implemented
│
├── UI/
│   ├── Views/                      # XAML ContentPages (code-behind is minimal)
│   │   ├── CheckInSessionPage.xaml / .cs
│   │   ├── CohortsPage.xaml / .cs
│   │   ├── StudentsPage.xaml / .cs
│   │   └── StudentHistoryPage.xaml / .cs
│   ├── ViewModels/
│   │   ├── CheckInSessionViewModel.cs
│   │   ├── CohortsViewModel.cs
│   │   ├── StudentsViewModel.cs
│   │   └── StudentHistoryViewModel.cs
│   ├── Converters/
│   │   ├── SignInStatusConverter.cs # DateTime? → "✓ HH:mm" | "◯ Not signed in"
│   │   └── StringToBoolConverter.cs # string → bool (non-empty = true)
│   └── Messaging/
│       └── AttendanceRecordedMessage.cs  # WeakReferenceMessenger payload
│
└── Resources/
    ├── AppIcon/
    ├── Splash/
    ├── Fonts/
    ├── Images/
    ├── Raw/
    └── Styles/
        ├── Colors.xaml             # Colour palette tokens
        └── Styles.xaml             # Global MAUI control styles
```

---

## 4. Key Architectural Patterns and Decisions

### 4.1 Layered Architecture (Clean Architecture-inspired)

The codebase follows a dependency hierarchy where inner layers never reference outer layers:

```
UI  →  Application  →  Core
              ↑
       Infrastructure
```

- **Core** contains only plain domain models and repository interfaces. It has zero external NuGet dependencies.
- **Application** contains use-case services and their interfaces. It depends on Core interfaces, not on Infrastructure types.
- **Infrastructure** implements the Core repository interfaces and Application service interfaces using concrete technology (MySqlConnector, WinRT APIs, CsvHelper).
- **UI** depends on Application interfaces only; it never calls Infrastructure or repository types directly.

This means the Dependency Rule (from *Clean Architecture*, Robert C. Martin) is respected: source-code dependencies point inward.

### 4.2 MVVM Pattern

All four pages follow strict MVVM separation:

- **View** (`.xaml` + minimal code-behind): sets `BindingContext` in the constructor and calls `ViewModel.LoadAsync()` from `OnAppearing()`. No business logic resides in the code-behind.
- **ViewModel**: derives from `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`. Exposes `ObservableCollection<T>` properties and `IAsyncRelayCommand` commands. Interacts only with Application-layer interfaces.
- **Model**: plain C# classes in `Core.Models` — no framework references.

Compiled XAML bindings (`x:DataType`) are used on all pages, enabling binding errors to be caught at compile time rather than runtime.

### 4.3 Dependency Injection via MAUI Host Builder

All service registrations are centralised in `MauiProgram.CreateMauiApp()`. The lifetimes are:

| Registration | Lifetime | Rationale |
|---|---|---|
| `MySqlOptions` | Singleton | Immutable configuration |
| `IDbConnectionFactory` / `MySqlConnectionFactory` | Singleton | Stateless factory |
| `DatabaseInitializer` | Singleton | One-time startup concern |
| `ICohortRepository` / `CohortRepository` | Singleton | Stateless data access |
| `IStudentRepository` / `StudentRepository` | Singleton | Stateless data access |
| `ICohortService` / `CohortService` | Singleton | Stateless orchestration |
| `IStudentService` / `StudentService` | Singleton | Stateless orchestration |
| `IAttendanceService` / `AttendanceService` | Singleton | Depends on `ISessionStateService` singleton |
| `ISessionStateService` / `SessionStateService` | Singleton | Shared mutable session state |
| `INfcService` / `NfcService` | Singleton | Manages a shared hardware resource |
| `ICsvExportService` / `CsvExportService` | Transient (Windows only) | Stateless per-operation; guarded by `#if WINDOWS` |
| ViewModels | Transient | Each navigation creates a fresh VM |
| Views (Pages) | Transient | Each navigation creates a fresh page |
| `AppShell` | Singleton | Shell is a long-lived navigation host |

### 4.4 Repository Pattern

Repositories (`CohortRepository`, `StudentRepository`) implement interfaces defined in `Core.Interfaces.Repositories`. Each method opens its own connection via `IDbConnectionFactory`, executes a query, and closes the connection with `await using`. There is no unit-of-work or shared transaction scope.

### 4.5 Result Object Pattern

`AttendanceScanResult` is a sealed class with a private constructor and four static factory methods (`SessionInactive`, `NotInCohort`, `Recorded`, `Error`). This avoids throwing exceptions for expected business outcomes (unrecognised card, inactive session) and makes the caller's branching logic explicit.

### 4.6 Cross-ViewModel Messaging

`CohortsViewModel` subscribes to `AttendanceRecordedMessage` via `WeakReferenceMessenger` (CommunityToolkit.Mvvm). When `CheckInSessionViewModel` records a scan, it sends this message. `CohortsViewModel` responds by refreshing the attendance view to the scanned date. This decouples the two ViewModels without direct references.

### 4.7 Platform-Conditional Services

`ICsvExportService` is registered only under `#if WINDOWS`. On other platforms the interface has no registration, meaning `StudentHistoryViewModel` would fail to resolve at runtime. This is consistent with the current Windows-only hardware dependency.

### 4.8 Database Initialisation at Startup

`DatabaseInitializer.InitializeAsync()` is called from `App.CreateWindow()` (non-blocking fire-and-forget). It issues `CREATE TABLE IF NOT EXISTS` for all three tables and seeds two test cohorts and three students if the database is empty. Errors are caught and surfaced as an in-app alert rather than crashing the app.

### 4.9 NFC Polling Model

The `NfcService` does not use an event-driven WinRT subscription. Instead it runs a tight polling loop (`Task.Run`) at 200 ms intervals that:

1. Enumerates smart card reader devices via `DeviceInformation.FindAllAsync`.
2. Filters for a reader whose name contains "ACR122" or "ACS".
3. Calls `FindAllCardsAsync()` and, if a card is present, sends the APDU command `FF CA 00 00 00` (Get UID) and hex-encodes the response.
4. Fires the `onUidScanned` callback only when the UID changes (deduplication by `lastUid`).
5. Backs off 1 second on error; terminates after 10 consecutive errors.

For single-scan mode (student registration), the service attempts one scan and falls back to a manual `DisplayPromptAsync` if no card is detected within 10 seconds.

---

## 5. Data Models / Domain Entities

### 5.1 Cohort

```
Cohort
  Id        int       PK
  Name      string    Display name (e.g., "cohort 2026")
```

A named group of students. The root aggregate for organising attendance sessions.

### 5.2 Student

```
Student
  Id            int        PK
  UID           string     NFC card UID (hex, unique, upper-cased on write)
  CohortId      int        FK → Cohort
  Name          string
  Surname       string
  SignInTime    DateTime?  Most recent session sign-in timestamp (denormalised on student row)
  SignOutTime   DateTime?  Most recent session sign-out timestamp (denormalised on student row)
```

The `SignInTime` / `SignOutTime` on the student row act as a "current session" cache — they are updated on each scan and are separate from the `attendance` table rows.

### 5.3 AttendanceRecord (read model)

```
AttendanceRecord
  StudentId          int
  StudentName        string
  StudentSurname     string
  SignInTime         DateTime?
  SignOutTime        DateTime?
  Duration           TimeSpan?   Computed: SignOutTime - SignInTime
  FormattedSignInTime   string  "yyyy-MM-dd HH:mm:ss" or "N/A"
  FormattedSignOutTime  string  "yyyy-MM-dd HH:mm:ss" or "N/A"
  FormattedDuration     string  "Xh Ym" or "In Progress"
```

This is a **read model** (not a persisted entity). It is projected from the `attendance` table join in `StudentRepository.GetAttendanceHistoryAsync`.

### 5.4 SessionType (enum)

```
ClockIn  = 1
ClockOut = 2
```

### 5.5 AttendanceScanResult (domain result object)

Returned by `AttendanceService.RecordScanAsync`. Carries success flag, message, optional `Student`, `SessionType`, and `Timestamp`. Constructed only via static factory methods:

- `SessionInactive()` — no active session when scan was received
- `NotInCohort(uid)` — UID not found in the active cohort
- `Recorded(student, sessionType, timestamp)` — success
- `Error(message)` — unexpected exception caught

### 5.6 ISessionStateService (in-memory state)

Not a database entity. `SessionStateService` holds the transient session state:

```
ActiveCohortId   int?
SessionType      SessionType
ActiveDay        DateOnly?
IsSessionActive  bool
```

This singleton is the shared mutable object that connects `CheckInSessionViewModel` (which starts/stops the session) with `AttendanceService` (which reads it on every scan).

---

## 6. Database Schema

The schema is created at runtime by `DatabaseInitializer`. There is no migration tooling — tables are created idempotently with `CREATE TABLE IF NOT EXISTS`.

### cohort

| Column | Type | Constraints |
|---|---|---|
| Id | INT | PK, AUTO_INCREMENT |
| Name | VARCHAR(100) | NOT NULL |

### student

| Column | Type | Constraints |
|---|---|---|
| Id | INT | PK, AUTO_INCREMENT |
| UID | VARCHAR(100) | NOT NULL, UNIQUE |
| cohortId | INT | NOT NULL, FK → cohort(Id) |
| SignInTime | DATETIME | NULL |
| SignOutTime | DATETIME | NULL |
| name | VARCHAR(100) | NOT NULL |
| surname | VARCHAR(100) | NOT NULL |

### attendance

| Column | Type | Constraints |
|---|---|---|
| Id | INT | PK, AUTO_INCREMENT |
| StudentId | INT | NOT NULL, FK → student(Id) ON DELETE CASCADE |
| SessionDate | DATE | NOT NULL |
| SignInTime | DATETIME | NULL |
| SignOutTime | DATETIME | NULL |
| — | — | UNIQUE (StudentId, SessionDate) |

The unique constraint on `(StudentId, SessionDate)` enforces one attendance row per student per day. The repository uses MySQL `ON DUPLICATE KEY UPDATE` for upsert semantics.

### Relationship Diagram

```
cohort (1) ──< student (N) ──< attendance (N)
```

Each cohort has many students. Each student has many attendance rows (one per session day). Student deletion cascades to attendance rows.

---

## 7. Key Components and Their Responsibilities

### 7.1 MauiProgram (Composition Root)

`MauiProgram.CreateMauiApp()` is the single place all services, repositories, ViewModels, and pages are wired. No service locator or manual `new` construction occurs elsewhere. Platform-specific services are conditionally registered with `#if WINDOWS`.

### 7.2 App

- Constructs the MAUI `Window` with the injected `AppShell`.
- Fires `DatabaseInitializer.InitializeAsync()` on window creation (non-blocking).
- Registers global exception handlers for `TaskScheduler.UnobservedTaskException` and `AppDomain.CurrentDomain.UnhandledException`. Cancellation exceptions are suppressed; other exceptions are logged to the debug output.

### 7.3 AppShell

- Defines a flyout navigation menu with three top-level items: "Check-In Sessions", "Cohorts", "Students".
- Registers the `studenthistory` route pointing to `StudentHistoryPage` for Shell deep-link navigation from `StudentsViewModel`.

### 7.4 CheckInSessionPage / CheckInSessionViewModel

Orchestrates the real-time attendance scanning workflow:

- Displays pickers for cohort, session type (ClockIn / ClockOut), and date.
- "Start Session" calls `ISessionStateService.StartSession(...)` then `INfcService.StartListeningAsync(callback)`. The callback invokes `IAttendanceService.RecordScanAsync(...)` and updates the UI on `MainThread`.
- On success, fires `AttendanceRecordedMessage` via `WeakReferenceMessenger`.
- "Stop Session" calls `INfcService.StopListeningAsync()` then `ISessionStateService.StopSession()`.

### 7.5 CohortsPage / CohortsViewModel

Displays cohorts and the attendance status of their students:

- Allows adding new cohorts by name.
- On cohort selection, fetches students filtered by the selected scope (Day / Month / Year) and date.
- Listens for `AttendanceRecordedMessage` and refreshes the student list to the scan's date automatically.
- The filter logic (`HasAttendanceForFilter`) shows students with matching attendance **or** students with no attendance yet (both `SignInTime` and `SignOutTime` are null).

### 7.6 StudentsPage / StudentsViewModel

Full CRUD for students within a selected cohort:

- Add: form with Name, Surname, UID (manual or via NFC scan), cohort picker.
- Edit: opens an in-page modal overlay (implemented with `AbsoluteLayout` + `BoxView` darkening layer + `Frame`). Edit fields are bound to separate `EditStudent*` properties on the VM.
- Delete: calls `IStudentService.DeleteStudentAsync`.
- View History: navigates to `studenthistory?studentId={id}` using Shell route.
- Scan UID: calls `INfcService.ScanSingleUidAsync()`, falls back to manual prompt.

### 7.7 StudentHistoryPage / StudentHistoryViewModel

Read-only per-student attendance history with export:

- Receives `studentId` as a Shell `[QueryProperty]`.
- Loads student details and full `AttendanceRecord` history on `OnAppearing`.
- "Download CSV" is enabled only when history is non-empty and no export is in progress (`CanExportCsv`). Calls `ICsvExportService.ExportAttendanceAsync`.

### 7.8 AttendanceService

The core attendance business logic:

1. Guards against an inactive session.
2. Normalises the NFC UID to uppercase.
3. Looks up the student by UID; rejects if not in the active cohort.
4. Computes the timestamp: uses `ActiveDay` from session state combined with the current wall-clock time.
5. Updates the `student` row's `SignInTime`/`SignOutTime` via `UpdateAttendanceAsync`.
6. Upserts the `attendance` table row via `UpsertDailyAttendanceAsync` (`ON DUPLICATE KEY UPDATE`).
7. Refreshes the student from the database and returns `AttendanceScanResult.Recorded(...)`.

### 7.9 NfcService

Windows-only, polling-based ACR122U interface:

- Continuous listening mode: background `Task.Run` loop, 200 ms poll interval, UID deduplication.
- Single-scan mode: one attempt with 10-second timeout, fallback to manual input dialog.
- Communicates with the reader via `SmartCardReader` / `SmartCard.ConnectAsync()` / APDU `FF CA 00 00 00`.
- Error resilience: backs off 1 s per error, self-terminates after 10 consecutive errors.

### 7.10 CsvExportService (Windows)

- Opens a native `FileSavePicker` dialog (requires WinRT window handle initialisation via `WinRT.Interop.InitializeWithWindow`).
- Writes a UTF-8 BOM CSV with a `sep=,` header line for Excel compatibility.
- Uses `AttendanceRecordMap` (CsvHelper `ClassMap`) to define column names, order, and date formatting.

### 7.11 DatabaseInitializer

- Runs at startup via `App.InitializeDatabaseAsync`.
- Issues idempotent `CREATE TABLE IF NOT EXISTS` DDL for all three tables.
- Seeds two test cohorts and three students on first run (if tables are empty).

### 7.12 MySqlConnectionFactory / MySqlOptions

- `MySqlOptions` holds hard-coded connection parameters (server, port, database, credentials). These are instantiated directly with `new MySqlOptions()` in `MauiProgram`.
- `MySqlConnectionFactory.CreateOpenConnectionAsync()` opens and returns a new `MySqlConnection` per call. Each repository method owns its connection lifetime.

---

## 8. Data Flow

### 8.1 App Startup

```
MauiProgram.CreateMauiApp()
  → DI container wired
  → App(AppShell, DatabaseInitializer) constructed
  → App.CreateWindow()
    → DatabaseInitializer.InitializeAsync()   [fire-and-forget]
      → MySqlConnectionFactory.CreateOpenConnectionAsync()
      → CREATE TABLE IF NOT EXISTS (cohort, student, attendance)
      → Seed test data if empty
    → Window shown with AppShell (flyout nav)
```

### 8.2 NFC Attendance Scan (Happy Path)

```
User taps "Start Session"
  → CheckInSessionViewModel.StartSessionAsync()
    → ISessionStateService.StartSession(cohortId, sessionType, day)
    → INfcService.StartListeningAsync(callback)
      → Background Task.Run polling loop starts

[200 ms later — card tapped]
NfcService detects new UID
  → callback(uid) invoked on background thread
    → IAttendanceService.RecordScanAsync(sessionType, uid)
      → ISessionStateService.IsSessionActive check
      → IStudentRepository.GetByUidAsync(uid)    [MySQL SELECT]
      → Cohort membership check
      → IStudentRepository.UpdateAttendanceAsync(...)  [MySQL UPDATE student]
      → IStudentRepository.UpsertDailyAttendanceAsync(...) [MySQL INSERT ... ON DUPLICATE KEY UPDATE]
      → IStudentRepository.GetByUidAsync(uid)    [MySQL SELECT — refresh]
      → returns AttendanceScanResult.Recorded(...)
    → MainThread.BeginInvokeOnMainThread():
        StatusMessage = "Recorded ClockIn for ..."
        LastScannedStudentName = "Scanned: ..."
        WeakReferenceMessenger.Send(AttendanceRecordedMessage)

CohortsViewModel receives AttendanceRecordedMessage
  → Updates SelectedSessionDate to scan timestamp date
  → Reloads student list for selected cohort
```

### 8.3 Student Registration with NFC Scan

```
User on StudentsPage taps "Scan UID"
  → StudentsViewModel.ScanUidAsync()
    → INfcService.ScanSingleUidAsync()
      → TryReadUidFromAcr122uAsync(timeout=10s)
        → If card detected: returns hex UID string
        → If timeout: DisplayPromptAsync (manual entry)
      → returns UID string
    → Uid property updated → Entry field shows UID

User taps "Add Student"
  → StudentsViewModel.AddStudentAsync()
    → IStudentService.AddStudentAsync(name, surname, uid, cohortId)
      → Validation (non-empty fields, valid cohortId)
      → IStudentRepository.AddAsync(student)  [MySQL INSERT]
    → Form fields cleared, list refreshed
```

### 8.4 CSV Export

```
User on StudentHistoryPage taps "Download CSV"
  → StudentHistoryViewModel.ExportCsvAsync()
    → IsExporting = true  (button disabled)
    → ICsvExportService.ExportAttendanceAsync(records, suggestedFileName)
      → FileSavePicker dialog shown
      → User selects path and confirms
      → StreamWriter opened with UTF-8 BOM
      → "sep=," header written
      → CsvWriter with AttendanceRecordMap writes records
    → IsExporting = false  (button re-enabled)
```

---

## 9. Configuration and Infrastructure Concerns

### 9.1 Database Connection

Connection parameters are hard-coded in `MySqlOptions`:

```csharp
Server   = "127.0.0.1"
Port     = 3307
Database = "student_logDb"
UserId   = "root"
Password = "FIL2026"
SslMode  = Preferred
```

There is no appsettings file, environment variable support, or secrets management. The password is stored in plain text in source code. For a production deployment this would need to be replaced with a configuration file, environment variable, or Windows Credential Manager integration.

The application expects MySQL to already be running at the configured address before launch. Connection failures are caught at startup and shown as a non-fatal alert.

### 9.2 Schema Management

There is no migration framework (e.g., Flyway, EF Core Migrations). The `DatabaseInitializer` uses `CREATE TABLE IF NOT EXISTS`, meaning:

- New tables can be added by extending `InitializeAsync`.
- Existing table alterations (adding/removing columns) require manual SQL or a schema migration strategy to be introduced.

### 9.3 Platform Targeting

The project file declares `TargetFrameworks` (plural) to include Android, iOS, and macCatalyst TFMs, but:

- `NfcService` references `Windows.Devices.SmartCards` which is unavailable on non-Windows platforms.
- `CsvExportService` references `Windows.Storage.Pickers` and is conditionally compiled with `#if WINDOWS`.
- `INfcService` is registered unconditionally in `MauiProgram`, meaning a build targeting Android/iOS would fail to link unless the WinRT references are guarded.

### 9.4 XAML Source Generation

`<MauiXamlInflator>SourceGen</MauiXamlInflator>` is set in the project file. This generates C# from XAML at compile time, avoiding runtime XAML parsing and reducing startup time. Compiled bindings (`x:DataType`) complement this.

### 9.5 Error Handling Strategy

- **Repository layer**: exceptions propagate up (logged in `UpdateAttendanceAsync` before re-throwing).
- **Service layer**: `AttendanceService` catches all exceptions and returns `AttendanceScanResult.Error(message)` — no exception escapes to the ViewModel.
- **ViewModel layer**: commands catch `ArgumentException` for validation failures and generic `Exception` for infrastructure errors; both are surfaced as `StatusMessage` strings bound to the UI.
- **Application level**: `TaskScheduler.UnobservedTaskException` suppresses `OperationCanceledException` / `TaskCanceledException`; all others are logged.
- **Database startup**: failure shows a `DisplayAlert` and the app continues in a degraded (no-data) state.

### 9.6 Threading Model

- NFC polling runs on a background thread via `Task.Run`.
- All UI property updates from background threads use `MainThread.BeginInvokeOnMainThread()` or `MainThread.InvokeOnMainThreadAsync()`.
- `WeakReferenceMessenger` message handlers also marshal to the main thread where UI state is modified.
- `CancellationToken` is threaded through all async repository methods, enabling cooperative cancellation.

---

## 10. Notable Conventions

### Naming

| Convention | Applied To | Example |
|---|---|---|
| PascalCase | Public types, properties, methods | `AttendanceScanResult`, `RecordScanAsync` |
| _camelCase prefix | Private fields | `_sessionStateService`, `_listeningCts` |
| `Async` suffix | All async methods | `LoadAsync`, `StartSessionAsync` |
| Interface prefix `I` | All interfaces | `ICohortService`, `IDbConnectionFactory` |
| `ViewModel` suffix | All ViewModels | `CohortsViewModel` |
| `Page` suffix | All MAUI ContentPages | `CohortsPage` |
| `Repository` suffix | All repository implementations | `CohortRepository` |
| `Service` suffix | All service implementations | `AttendanceService` |
| `Map` suffix | CsvHelper ClassMap | `AttendanceRecordMap` |
| `Message` suffix | Messenger payloads | `AttendanceRecordedMessage` |
| `Converter` suffix | MAUI value converters | `SignInStatusConverter` |

### Async Discipline

- All I/O operations are async throughout (no `.Result` or `.Wait()` except in `StopListeningAsync` where a 2-second bounded wait is intentional for cleanup).
- `CancellationToken` is accepted as an optional parameter with `default` on every repository and service method.

### UID Normalisation

NFC UIDs are normalised to uppercase at two points:

1. `AttendanceService.RecordScanAsync`: `uid.Trim().ToUpperInvariant()` before database lookup.
2. `StudentService.UpdateStudentAsync`: `student.UID.Trim().ToUpperInvariant()` before persistence.

This ensures consistent matching regardless of how the UID is presented by different readers or manual entry.

### Compiled XAML Bindings

All XAML pages declare `x:DataType` pointing to their respective ViewModel. DataTemplates inside `CollectionView` declare `x:DataType` pointing to the model type (`Cohort`, `Student`, `AttendanceRecord`). This gives compile-time binding validation.

### In-Page Modal Pattern

`StudentsPage` implements an edit modal using `AbsoluteLayout` with a semi-transparent `BoxView` overlay and a `Frame` positioned at proportional coordinates. Visibility is controlled by the `IsEditingStudent` boolean property on the ViewModel. This avoids a separate navigation route for the edit form.

### Flyout Navigation + Shell Routing

Top-level pages use the Shell `FlyoutItem` / `ShellContent` model with `DataTemplate` (lazy page creation). `StudentHistoryPage` is a secondary page registered with a named route (`studenthistory`) and navigated to via `Shell.Current.GoToAsync($"studenthistory?studentId={id}")` with a query parameter received via `[QueryProperty]`.

### Debug Logging

All diagnostic output uses `System.Diagnostics.Debug.WriteLine` with bracketed prefixes that identify the subsystem:

- `[APP]` — application lifecycle
- `[DB]` — database initialisation and seeding
- `[NFC]` — NFC reader polling
- `[ATTENDANCE]` — attendance recording logic
- `[REPOSITORY]` — raw SQL execution
- `[VIEW MODEL]` / `[VIEW MODEL ERROR]` — ViewModel operations
- `[HISTORY]` — student history loading
- `[EXPORT]` — CSV export

This output is visible only in debug builds attached to a debugger or debug output window.
