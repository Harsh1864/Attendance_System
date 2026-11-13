# Attendance System

An ASP.NET Core 8 MVC application for tracking employee attendance.
Employees check in and out each day. Admins see a dashboard, manage
employees, review attendance and export reports.

## Features

**Employees**
- Register and log in
- Daily check-in (work mode: Office / Remote / Field, plus task types) and check-out
- Automatic status: `Regular`, `LateIn`, `EarlyOut`, `LateEarly`
- Last 7 days of attendance, editable task list
- Profile page with picture upload and password change

**Admins**
- Dashboard: active/inactive employees, late or early count, absentees,
  total working hours, task-type and hours-per-employee charts
- Employee management: edit details, activate or deactivate accounts
- Attendance summary grid with date filter
- Export to PDF and to CSV (any date range up to one year)
- Live notifications (via RabbitMQ and Redis) when employees register, check in or check out
- Daily absentee job that marks active employees with no check-in as `Absent`

## Tech stack

| Layer      | Technology                                    |
|------------|-----------------------------------------------|
| Web        | ASP.NET Core 8 MVC, Razor views               |
| UI         | Bootstrap 5, Kendo UI for jQuery              |
| Database   | PostgreSQL via Npgsql                         |
| Sessions   | Redis (StackExchange.Redis)                   |
| Messaging  | RabbitMQ and Redis lists for admin notifications |
| Tests      | xUnit                                         |

## Project layout

```
Controllers/      MVC controllers (Admin, EmployeeView, Home, Profile, ProfileUpdate)
Services/         Database access, attendance rules, password hashing, CSV export,
                  background absentee job, RabbitMQ and Redis messaging
Middleware/       AuthMiddleware: session-based access control
Models/           View and data models
Views/            Razor views (Admin, EmployeeView, Home, Profile, Shared layouts)
Database/         schema.sql and migrations/
tests/            xUnit test project
wwwroot/          Static files and uploaded images
```

## Getting started

### Requirements
- .NET 8 SDK
- PostgreSQL 13+
- Redis (sessions and notifications)
- RabbitMQ (notifications)

### 1. Create the database
```bash
createdb attendance
psql -d attendance -f Database/schema.sql
```
For an existing database, run the files in `Database/migrations/` in order instead.

### 2. Configure
Settings live in `appsettings.json`. Keep real credentials out of git by
using user secrets or environment variables for the connection string:

```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=attendance;Username=postgres;Password=..."
```
or
```bash
set ConnectionStrings__Default=Host=localhost;Port=5432;Database=attendance;Username=postgres;Password=...
```

Shift rules are configurable:

```json
"Shift": {
  "Start": "09:15",          // check-in after this is LateIn
  "End": "17:00",            // check-out before this is EarlyOut
  "AbsenteeRunTime": "18:00",// when the daily absentee job runs
  "SkipWeekends": true       // don't mark absentees on Saturday/Sunday
}
```

### 3. Run
```bash
dotnet run
```
Log in with the admin account created by `schema.sql`
(`admin@example.com` / `ChangeMe123`) and change the password right away.

### 4. Test
```bash
dotnet test tests/AttendanceSystem.Tests
```

## Security notes
- Passwords are stored as PBKDF2-SHA256 hashes. Older plain-text
  passwords still work and are re-hashed on the next successful login.
- Every page except login, register and static files requires a session.
  `/Admin/*` requires the Admin role, and inactive accounts cannot use
  employee pages.
- Profile edits always apply to the logged-in user, never to an id or
  email sent from the browser.

## API endpoints (admin)

| Method | Path                                   | Description                    |
|--------|----------------------------------------|--------------------------------|
| GET    | `/Admin/GetAttendance?date=`           | Attendance rows (JSON)         |
| GET    | `/Admin/GetDashboardSummary?date=`     | Dashboard numbers (JSON)       |
| GET    | `/Admin/GetAllEmployeesDetails`        | All employees (JSON)           |
| GET    | `/Admin/ExportAttendanceCsv?from=&to=` | CSV download                   |
| POST   | `/Admin/RunAttendance`                 | Run the absentee job now       |
| POST   | `/Admin/UpdateEmployeeStatus`          | Activate / deactivate employee |
