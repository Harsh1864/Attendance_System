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
- **Leave**: request Casual / Sick / Earned / Unpaid leave (full or half
  day), see remaining balance per type, cancel requests, see upcoming holidays
- **My Report**: monthly attendance %, totals and a day-by-day calendar

**Admins**
- Dashboard: active/inactive employees, late or early count, absentees,
  total working hours, task-type and hours-per-employee charts
- Employee management: edit details, activate or deactivate accounts
- Attendance summary grid with date filter
- Export to PDF and to CSV (any date range up to one year)
- **Edit Attendance**: add missing records, correct times, mark absent or
  delete, with a required reason
- **Monthly Report**: per-employee working days, present, late, early out,
  absent, leave, hours and attendance %, a calendar per employee, CSV download
- **Leave Requests**: approve or reject (reason required to reject)
- **Holidays**: company holiday calendar
- **Audit Log**: every login, account change, attendance edit, leave
  decision and holiday change, searchable by date, action and text
- Live notifications (via RabbitMQ and Redis) when employees register,
  check in, check out or request leave
- Daily absentee job that marks active employees with no check-in as
  `Absent`, skipping weekends, holidays and approved leave

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
For an existing database, run the files in `Database/migrations/` in order instead:

| Migration | Adds |
|-----------|------|
| `001_widen_password_column.sql` | Room for password hashes |
| `002_audit_log.sql` | `t_aaudit` |
| `003_holidays.sql` | `t_aholiday` |
| `004_leave.sql` | `t_aleave` |

### 2. Configure
The connection string is **not** stored in `appsettings.json`. The app
refuses to start until you provide it in one of these ways:

**Development (recommended): user secrets**, stored outside the repo:
```bash
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=attendance;Username=postgres;Password=..."
```

**Server: environment variable**
```bash
set ConnectionStrings__Default=Host=localhost;Port=5432;Database=attendance;Username=postgres;Password=...
```

**Any environment: `appsettings.Local.json`**, which is git-ignored. Copy
`appsettings.Local.example.json` to `appsettings.Local.json` and fill it in.

Leave rules are configurable:

```json
"Leave": {
  "AnnualAllowance": { "Casual": 12, "Sick": 10, "Earned": 15 },  // Unpaid is unlimited
  "SickBackdateDays": 7,   // sick leave may start up to 7 days ago
  "MaxSpanDays": 30,       // longest single request
  "SkipWeekends": true     // weekends are not counted as leave days
}
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
- Database credentials are kept out of the repository (see Configure).
- Attendance corrections, leave decisions and account changes are
  recorded in the audit log with who, when, what and why.

## API endpoints (admin)

| Method | Path                                   | Description                    |
|--------|----------------------------------------|--------------------------------|
| GET    | `/Admin/GetAttendance?date=`           | Attendance rows (JSON)         |
| GET    | `/Admin/GetDashboardSummary?date=`     | Dashboard numbers (JSON)       |
| GET    | `/Admin/GetAllEmployeesDetails`        | All employees (JSON)           |
| GET    | `/Admin/ExportAttendanceCsv?from=&to=` | CSV download                   |
| POST   | `/Admin/RunAttendance`                 | Run the absentee job now       |
| POST   | `/Admin/UpdateEmployeeStatus`          | Activate / deactivate employee |
| GET    | `/Admin/Reports/Monthly?year=&month=`  | Monthly report (JSON)          |
| GET    | `/Admin/Reports/MonthlyCsv?year=&month=` | Monthly report CSV           |
| GET    | `/Admin/Leave/List?status=&from=&to=`  | Leave requests (JSON)          |
| POST   | `/Admin/Leave/Review`                  | Approve / reject leave         |
| GET    | `/Admin/Holidays/List?year=`           | Holidays (JSON)                |
| GET    | `/Admin/AttendanceEdit/Search`         | Attendance records to edit     |
| POST   | `/Admin/AttendanceEdit/Save`           | Add or correct a record        |
| GET    | `/Admin/GetAuditLog?from=&to=&action=` | Audit entries (JSON)           |
