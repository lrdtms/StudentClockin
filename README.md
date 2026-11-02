# StudentClockin (StudentLog)

A Windows desktop app that takes student attendance by NFC card tap. Staff pick a cohort and a session type (Clock In or Clock Out), start a session, and students tap their cards on an ACR122U reader. Each tap is matched to a student and written to a MySQL database.

## Features
- NFC card scanning with an ACR122U reader (polled every 200 ms)
- Cohort management and manual student registration, with an optional NFC UID scan
- Clock In / Clock Out sessions
- Per-student attendance history with filters
- CSV export of attendance records

## Tech
- .NET MAUI 10 (Windows target), Shell navigation
- MVVM with CommunityToolkit.Mvvm
- MySQL via MySqlConnector (plain ADO.NET, no ORM)
- Windows smart-card API for the NFC reader
- CsvHelper for exports
- Layered structure: `Core` (models, repository interfaces), `Application` (services), `Infrastructure` (data access, NFC), `UI` (views and view models)

## Requirements
- Windows 10 or later
- Visual Studio with the .NET MAUI workload and the .NET 10 SDK
- An ACR122U NFC reader
- A MySQL database the app can connect to

## Getting started
Open `StudentLog/StudentLog.slnx` in Visual Studio and run the `StudentLog` project on Windows. The data-access code lives in `StudentLog/StudentLog/Infrastructure`.

## Docs
Design notes and an architecture review are in [`StudentLog/ARCHITECTURE.md`](StudentLog/ARCHITECTURE.md).
