# StudentLog Architecture

## Source of Truth
- All implementation decisions must comply with `Reference/checklist.md`.
- This document is updated as features are added.

## Overview
**StudentLog** is a .NET MAUI attendance tracking application using NFC technology to scan student IDs. The application follows a **Layered MVVM Architecture** with clean separation of concerns across four main layers: Core, Application, Infrastructure, and UI.

**Technology Stack:**
- Framework: .NET MAUI 10
- Platform: Windows desktop only (`net10.0-windows10.0.19041.0`)
- Database: MySQL with MySqlConnector
- NFC Hardware: ACR122U reader
- UI Framework: MAUI XAML with MVVM pattern

---

## Architecture Style
Layered MVVM architecture for .NET MAUI:

Target runtime: **Windows desktop only** (`net10.0-windows10.0.19041.0`).

### 1. **Core Layer** - Domain Models and Abstractions
   - Domain models and repository interfaces.
   - **No external dependencies** - framework-agnostic
   - **Files:**
     - `Core/Models/Cohort.cs` - Group/class of students
     - `Core/Models/Student.cs` - Individual student with sign-in/out times
     - `Core/Models/SessionType.cs` - Enumeration (ClockIn, ClockOut)
     - `Core/Models/AttendanceRecord.cs` - Display model for attendance history
     - `Core/Models/AttendanceScanResult.cs` - Result of NFC scan operations
     - `Core/Interfaces/Repositories/ICohortRepository.cs`
     - `Core/Interfaces/Repositories/IStudentRepository.cs`
   - **Key Models:**
     - **Cohort:** `Id`, `Name`
     - **Student:** `Id`, `UID` (NFC tag), `CohortId`, `SignInTime`, `SignOutTime`, `Name`, `Surname`
     - **SessionType:** `ClockIn`, `ClockOut`
     - **AttendanceRecord:** Display model for attendance history; computed properties `Duration`, `FormattedSignInTime`, `FormattedSignOutTime`
     - **AttendanceScanResult:** Factory result object — `SessionInactive()`, `NotInCohort()`, `Recorded()`, `Error()`

### 2. **Application Layer** - Business Logic and Services
   - Service interfaces and business workflows - orchestration logic
   - **No UI dependencies** - framework-agnostic
   - **Files:**
     - `Application/Interfaces/IAttendanceService.cs` - Attendance recording
     - `Application/Interfaces/ICohortService.cs` - Cohort management
     - `Application/Interfaces/ISessionStateService.cs` - Session state tracking
     - `Application/Interfaces/IStudentService.cs` - Student management
     - `Application/Interfaces/INfcService.cs` - NFC hardware abstraction
     - `Application/Services/*.cs` - Concrete implementations
   - **Responsibilities:**
     - Cohort management (list, create)
     - Student management (list, create)
     - Session state (active cohort, active day, session flag)
     - Attendance recording from NFC UID scans
     - Business rule validation (session active, student in cohort)

### 3. **Infrastructure Layer** - Technical Implementation
   - MySQL data access and NFC hardware implementation
   - **Database Details:**
     - Host: `127.0.0.1:3307`
     - Database: `student_logDb`
     - Tables: `cohort`, `student`, `attendance`
   - **Files:**
     - `Infrastructure/Data/MySqlOptions.cs` - Connection configuration
     - `Infrastructure/Data/IDbConnectionFactory.cs` - Connection abstraction
     - `Infrastructure/Data/MySqlConnectionFactory.cs` - MySQL implementation
     - `Infrastructure/Data/DatabaseInitializer.cs` - Schema initialization
     - `Infrastructure/Repositories/CohortRepository.cs` - Implements `ICohortRepository`
     - `Infrastructure/Repositories/StudentRepository.cs` - Implements `IStudentRepository`
     - `Infrastructure/Services/NfcService.cs` - Implements `INfcService` for ACR122U
   - **NFC Service Details:**
     - Continuous polling mode: `StartListeningAsync()`
     - Single read mode: `ScanSingleUidAsync()`
     - ACR122U driver integration
     - Duplicate detection (prevents multiple readings of same card)
     - Error recovery with automatic stop after 10 consecutive errors
     - Configurable timeout and polling intervals

### 4. **UI Layer** - Presentation and User Interaction
   - MAUI Views + MVVM ViewModels
   - **MVVM Pattern:** Views bind to ViewModels via `BindingContext`
   - **Files:**
     - `UI/Views/CheckInSessionPage.xaml(.cs)` - NFC check-in/check-out operations
     - `UI/Views/CohortsPage.xaml(.cs)` - Cohort selection and attendance display with date/period filter
     - `UI/Views/StudentsPage.xaml(.cs)` - Student management (create, edit, delete, NFC UID scan)
     - `UI/Views/StudentHistoryPage.xaml(.cs)` - Per-student attendance history with duration
     - `UI/ViewModels/CheckInSessionViewModel.cs` - Scan logic and session management
     - `UI/ViewModels/CohortsViewModel.cs` - Cohort list, selection, and date-scoped attendance
     - `UI/ViewModels/StudentsViewModel.cs` - Student CRUD and NFC UID assignment
     - `UI/ViewModels/StudentHistoryViewModel.cs` - Attendance history for a single student (QueryProperty deep link)
     - `UI/Converters/*.cs` - Value converters for data binding
     - `UI/Messaging/*.cs` - Cross-component messaging
   - **Navigation:**
     - Shell-based flyout in `AppShell.xaml`
     - Routes: Check-In Sessions (home), Cohorts, Students, StudentHistory

## Dependency Injection
Configured in `MauiProgram.cs`:
- **Singleton services:** Stateful app services, repositories, infrastructure services, database initializer
  - Ensures single instance across application lifetime
  - Used for: database connection factory, repositories, business services
- **Transient ViewModels and Pages:** New instance per request
  - Fresh state for each navigation
  - Prevents state leakage between pages

**Service Registration Pattern:**
```
// Data Access
builder.Services.AddSingleton<IDbConnectionFactory, MySqlConnectionFactory>();
builder.Services.AddSingleton<DatabaseInitializer>();

// Repositories
builder.Services.AddSingleton<ICohortRepository, CohortRepository>();
builder.Services.AddSingleton<IStudentRepository, StudentRepository>();

// Application Services
builder.Services.AddSingleton<ICohortService, CohortService>();
builder.Services.AddSingleton<IStudentService, StudentService>();
builder.Services.AddSingleton<IAttendanceService, AttendanceService>();
builder.Services.AddSingleton<ISessionStateService, SessionStateService>();
builder.Services.AddSingleton<INfcService, NfcService>();

// UI (Transient)
builder.Services.AddTransient<CheckInSessionViewModel>();
builder.Services.AddTransient<CohortsViewModel>();
builder.Services.AddTransient<StudentsViewModel>();
builder.Services.AddTransient<StudentHistoryViewModel>();
builder.Services.AddTransient<CheckInSessionPage>();
builder.Services.AddTransient<CohortsPage>();
builder.Services.AddTransient<StudentsPage>();
builder.Services.AddTransient<StudentHistoryPage>();
```

---

## Data Flow

### Attendance Recording Flow
```
User scans NFC card
        ↓
NfcService detects UID
        ↓
UID passed to CheckInSessionViewModel
        ↓
ViewModel calls AttendanceService.RecordScanAsync()
        ↓
AttendanceService validates:
  - Session is active
  - Student exists in database
  - Student belongs to active cohort
        ↓
Determines SessionType (ClockIn or ClockOut)
        ↓
StudentRepository.UpdateAttendanceAsync() updates student row (SignInTime / SignOutTime)
        ↓
StudentRepository.UpsertDailyAttendanceAsync() inserts or updates attendance table by date
        ↓
Returns AttendanceScanResult (success/failure with details)
        ↓
ViewModel updates UI with result
        ↓
AttendanceRecordedMessage sent for cross-component notification
```

### Cohort and Student Loading
```
User navigates to Cohorts page
        ↓
CohortsViewModel.LoadAsync() called
        ↓
ICohortService retrieves all cohorts
        ↓
CohortRepository queries: SELECT * FROM cohort
        ↓
Cohorts displayed in list
        ↓
User selects cohort → SessionStateService.SetActiveCohort()
        ↓
IStudentRepository loads students for selected cohort
        ↓
Attendance records filtered by session date scope
```

---

## Dependency Graph

```
Presentation Layer (UI)
├─ CheckInSessionPage
│  └─ CheckInSessionViewModel
│     ├─ INfcService (Application)
│     ├─ IAttendanceService (Application)
│     └─ ISessionStateService (Application)
│
├─ CohortsPage
│  └─ CohortsViewModel
│     ├─ ICohortService (Application)
│     ├─ ISessionStateService (Application)
│     └─ IStudentService (Application)
│
├─ StudentsPage
│  └─ StudentsViewModel
│     ├─ IStudentService (Application)
│     └─ INfcService (Application)
│
└─ StudentHistoryPage
   └─ StudentHistoryViewModel
      └─ IStudentService (Application)

Application Layer (Services)
├─ AttendanceService
│  ├─ IStudentRepository (Core)
│  └─ ISessionStateService (Application)
│
├─ CohortService
│  └─ ICohortRepository (Core)
│
├─ StudentService
│  └─ IStudentRepository (Core)
│
└─ SessionStateService (stateful)

Infrastructure Layer
├─ CohortRepository (implements ICohortRepository)
│  └─ IDbConnectionFactory
│     └─ MySqlConnectionFactory
│
├─ StudentRepository (implements IStudentRepository)
│  └─ IDbConnectionFactory
│
└─ NfcService (implements INfcService)
   └─ Windows.Devices.SmartCards API

Core Layer (Models & Interfaces)
├─ Domain Models: Cohort, Student, SessionType, AttendanceRecord, AttendanceScanResult
└─ Interfaces: ICohortRepository, IStudentRepository
```

---

## Data Storage
- **Database Engine:** MySQL (version 8.0+)
- **Driver:** MySqlConnector for async-first operations
- **Connection Target:**
  - Host: `127.0.0.1`
  - Port: `3307`
  - Database: `student_logDb`
- **Tables:**
  - `cohort` - Stores cohort/class information
    - `Id` (INT, Primary Key, Auto-increment)
    - `Name` (VARCHAR)
  - `student` - Stores student information and current-session timestamps
    - `Id` (INT, Primary Key, Auto-increment)
    - `UID` (VARCHAR, unique NFC tag identifier)
    - `CohortId` (INT, Foreign Key to cohort)
    - `SignInTime` (DATETIME, nullable)
    - `SignOutTime` (DATETIME, nullable)
    - `Name` (VARCHAR)
    - `Surname` (VARCHAR)
  - `attendance` - Per-day attendance records (history)
    - `Id` (INT, Primary Key, Auto-increment)
    - `StudentId` (INT, Foreign Key to student)
    - `SessionDate` (DATE)
    - `SignInTime` (DATETIME, nullable)
    - `SignOutTime` (DATETIME, nullable)
    - Unique constraint on `(StudentId, SessionDate)`

**Initialization:**
- `DatabaseInitializer` runs asynchronously after main window creation
- Ensures tables exist and match schema during app startup
- App startup does **not** block on MySQL availability
- Connection pooling used for efficiency

---

## NFC Integration

**Interface:** `INfcService` (defined in Application layer)

**Implementation:** `NfcService` (Infrastructure layer)
- **Hardware Target:** ACR122U NFC reader
- **Platform:** Windows only (via `Windows.Devices.SmartCards` API)
- **UID Format:** Hexadecimal string (e.g., "0A1B2C3D")

**Key Features:**
- **Continuous Listening Mode:** `StartListeningAsync(onUidScanned, cancellationToken)`
  - Polls continuously for card presence
  - Invokes callback when new UID detected
  - Skips duplicate readings of same card
  - Graceful error handling with automatic stop after 10 consecutive errors

- **Single Read Mode:** `ScanSingleUidAsync(cancellationToken)`
  - Scans once and returns UID or null
  - Configurable timeout (currently 10 seconds)
  - Falls back to a manual UID prompt if the ACR122U scan fails

- **Status Property:** `IsListening` flag to track active state

**Implementation Details:**
- Async/await throughout for UI responsiveness
- Polling interval: 200ms (no card), 1000ms (after error)
- Binary UID extraction and formatting
- Cancellation token support for graceful shutdown
- Debug output with `[NFC]` prefix for diagnostics

**Extensibility:**
- ACR122U driver-specific implementation can be replaced without changing ViewModels
- Abstraction allows for mock implementations in tests

---

## Key Design Patterns

### 1. **Layered Architecture**
- Four distinct layers: Core, Application, Infrastructure, UI
- Unidirectional dependencies: UI → Application → Core
- Infrastructure implements interfaces from Core
- Clear separation of concerns

### 2. **Dependency Injection (DI)**
- All dependencies injected via constructor parameters
- MAUI's built-in service container
- Enables loose coupling and testability

### 3. **Repository Pattern**
- `ICohortRepository` and `IStudentRepository` abstract data access
- Core interfaces, Infrastructure implementations
- Allows swapping data sources (SQL, SQLite, test doubles)

### 4. **Service Layer Pattern**
- Application layer services orchestrate business logic
- Hides infrastructure complexity from ViewModels
- Promotes code reuse and centralized business rules

### 5. **MVVM Pattern**
- **Models:** Domain objects from Core layer
- **Views:** XAML pages in UI layer
- **ViewModels:** Bind to Views, call application services
- Two-way data binding for reactive updates

### 6. **Facade Pattern**
- `NfcService` simplifies complex Windows hardware APIs
- Hides ACR122U reader implementation details

### 7. **State Management**
- `SessionStateService` centralizes session state
- Single source of truth for active cohort, active day, session flag
- Prevents distributed state across components

---

## Navigation

**Shell-Based Navigation:**
- `AppShell.xaml` - Defines flyout navigation structure
- `AppShell.xaml.cs` - Route registration
- **Routes:**
  - `/checkinsession` - Check-in/check-out home page
  - `/cohorts` - Cohort selection and attendance view
  - `/students` - Student management (CRUD + NFC scan)
  - `/studenthistory` - Per-student attendance history (deep link via QueryProperty)

**Navigation Flow:**
```
App.xaml.cs
    ↓
AppShell (registered in DI)
    ↓
Flyout menu → Pages → ViewModels → Services
```

---

## Current Feature Coverage

### ✓ Implemented Features
1. **Cohort Management**
   - List all cohorts
   - Create new cohort
   - Select active cohort for session

2. **Student Management**
   - Create, edit, and delete students
   - Assign to cohort
   - Store NFC UID (via NFC scan or manual entry)

3. **Session Management**
   - Start/stop check-in sessions
   - Maintain active session state
   - Track active cohort and day

4. **Attendance Recording**
   - NFC UID scan detection
   - Automatic clock-in/clock-out determination
   - Update sign-in/sign-out timestamps
   - Handle duplicate scans

5. **Attendance Display**
   - View attendance by cohort
   - Filter by date scope (day, month, year)
   - Match on `SignInTime` and `SignOutTime`

6. **Student Attendance History**
   - Per-student history view navigated from StudentsPage
   - Lists all attendance records with `Duration`, `FormattedSignInTime`, `FormattedSignOutTime`

7. **NFC Integration**
   - Listening mode API: continuous polling
   - Single UID capture workflow
   - Error handling and recovery

---

## Cross-Cutting Concerns

### Error Handling
- **NfcService:** Consecutive error tracking, auto-stop after 10 errors
- **AttendanceService:** Business rule validation (session active, student in cohort)
- **Repositories:** Async operations with proper exception propagation
- **Database:** Connection retries via MySqlConnector

### Logging and Diagnostics
- Debug output with component prefix: `[COMPONENT] Message`
- Example: `[NFC] Starting listener`, `[ATTENDANCE] ClockIn update`
- Disabled in Release builds for performance

### Cancellation Support
- All async operations accept `CancellationToken`
- Enables graceful shutdown and resource cleanup
- Critical for responsive UI and background tasks

### Async/Await Patterns
- All I/O operations are asynchronous
- Prevents blocking UI thread
- Enables efficient resource utilization
- Used consistently across all layers

---

## Testability

The architecture supports unit testing through:
- **Dependency Injection:** Mock implementations injectable for testing
- **Interface-Based Design:** Services depend on abstractions
- **Separation of Concerns:** Business logic isolated from UI and infrastructure
- **Async Support:** Cancellation tokens enable test control

**Example Test Setup:**
```csharp
// Mock repository
var mockStudentRepository = new Mock<IStudentRepository>();
mockStudentRepository.Setup(r => r.GetByUidAsync("ABC123", default))
    .ReturnsAsync(new Student { Id = 1, Name = "John" });

// Inject mock into service under test
var attendanceService = new AttendanceService(
    mockStudentRepository.Object,
    mockSessionStateService.Object
);

// Test business logic in isolation
var result = await attendanceService.RecordScanAsync(
    SessionType.ClockIn, "ABC123", default);

// Assert result
Assert.True(result.Success);
```

---

## Performance Considerations

1. **Database Connection Pooling:** MySqlConnectionFactory reuses connections
2. **Async Operations:** Non-blocking I/O prevents thread starvation
3. **Lazy Loading:** Repositories load data on-demand, not preloaded
4. **Duplicate Detection:** NFC polling skips redundant scans
5. **Error Backoff:** Automatic stop after consecutive errors prevents resource exhaustion
6. **UI Thread Safety:** All long-running operations async to keep UI responsive

---

## Security Considerations

1. **UID Normalization:** UIDs normalized to uppercase for consistency
2. **Parameterized Queries:** All SQL uses parameterized queries to prevent injection
3. **Session Validation:** Attendance operations validate active session
4. **Cohort Isolation:** Students only scanned into assigned cohort
5. **Hardware Abstraction:** NFC details not exposed to business logic
6. **Database Credentials:** Connection string stored in `MySqlOptions` (externalize for production)

---

## Extension Points

### Adding New Features

**1. New Attendance Operation:**
- Add value to `SessionType` enum
- Update `AttendanceService.RecordScanAsync()` business logic
- Extend database schema if needed

**2. New Repository:**
- Define interface in `Core/Interfaces/Repositories/`
- Implement in `Infrastructure/Repositories/`
- Register in `MauiProgram.cs`

**3. New Business Service:**
- Create interface in `Application/Interfaces/`
- Implement in `Application/Services/`
- Inject into ViewModels as needed
- Register in `MauiProgram.cs`

**4. New UI Page:**
- Create `.xaml` and `.xaml.cs` in `UI/Views/`
- Create `ViewModel` in `UI/ViewModels/`
- Register in `MauiProgram.cs`
- Add route to `AppShell.xaml`

---

## Deployment and Startup

**Application Startup Sequence:**
1. `App.xaml.cs` - Creates MAUI app
2. `MauiProgram.cs` - Registers all services in DI container
3. `App.cs` - Creates window and resolves `AppShell`
4. `AppShell.xaml.cs` - Registers routes
5. `DatabaseInitializer` - Runs asynchronously, ensures schema exists
6. Main window displayed
7. App ready for user interaction (database initialization may still be in progress)

**Key Point:** Async database initialization does not block app startup

---

## Future Enhancement Opportunities

Potential improvements to the architecture:

1. **Caching Layer:** Cache cohorts/students to reduce database queries
2. **Audit Trail:** Log all attendance changes for compliance
3. **Report Generation:** Attendance analytics and export (CSV, PDF)
4. **Batch Operations:** Bulk student import/export
5. **Offline Support:** Local SQLite caching with sync when online
6. **Authentication:** User login and role-based access control
7. **Configuration Management:** Externalize settings to appsettings.json
8. **Database Abstraction:** Support multiple database providers
9. **Localization:** Multi-language UI support
10. **Advanced Filtering:** Complex queries on attendance data

---

## Summary

StudentLog follows a clean, layered architecture with strict separation of concerns. The four-layer design (Core, Application, Infrastructure, UI) promotes maintainability, testability, and extensibility. Dependencies flow unidirectionally, all services implement abstractions, and the dependency injection container ensures loose coupling. The use of async/await patterns, interface-based design, and comprehensive error handling makes the codebase resilient and professional-grade.
