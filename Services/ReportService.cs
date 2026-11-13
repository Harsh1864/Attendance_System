using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using project.Models;

namespace project.Services
{
    public class ReportService
    {
        private readonly DbConnectionFactory _db;
        private readonly HolidayService _holidays;
        private readonly LeaveService _leave;
        private readonly ShiftOptions _shift;

        public ReportService(DbConnectionFactory db, HolidayService holidays, LeaveService leave, IOptions<ShiftOptions> shift)
        {
            _db = db;
            _holidays = holidays;
            _leave = leave;
            _shift = shift.Value;
        }

        public static string? ValidateMonth(int year, int month, DateTime today)
        {
            if (month < 1 || month > 12) return "Month must be between 1 and 12.";
            if (year < 2000 || new DateTime(year, month, 1) > today.Date) return "Choose a month that has started.";
            return null;
        }

        public async Task<MonthlyReportModel> GetMonthlyAsync(int year, int month, DateTime today)
        {
            var (start, end) = MonthlyReportBuilder.MonthRange(year, month);
            var employees = await GetEmployeesAsync(null);
            var attendance = await GetAttendanceAsync(start, end, null);
            var leave = await _leave.SearchAsync(start, end, LeaveRules.Approved);
            var holidays = await GetHolidayMapAsync(start, end);

            return MonthlyReportBuilder.Build(year, month, today, employees, attendance, leave, holidays, _shift.SkipWeekends);
        }

        public async Task<EmployeeMonthModel?> GetEmployeeMonthAsync(int empId, int year, int month, DateTime today)
        {
            var (start, end) = MonthlyReportBuilder.MonthRange(year, month);
            var emp = (await GetEmployeesAsync(empId)).FirstOrDefault();
            if (emp == null) return null;

            var attendance = await GetAttendanceAsync(start, end, empId);
            var leave = await _leave.SearchAsync(start, end, LeaveRules.Approved, empId);
            var holidays = await GetHolidayMapAsync(start, end);

            var days = MonthlyReportBuilder.BuildDays(year, month, today, attendance, leave, holidays, _shift.SkipWeekends);
            return new EmployeeMonthModel
            {
                Summary = MonthlyReportBuilder.Summarise(emp, days, today, leave),
                Days = days,
            };
        }

        private async Task<Dictionary<DateTime, string>> GetHolidayMapAsync(DateTime from, DateTime to)
        {
            var list = await _holidays.GetInRangeAsync(from, to);
            return list.ToDictionary(h => h.Date.Date, h => h.Name);
        }

        private async Task<List<MonthlyReportBuilder.EmployeeInfo>> GetEmployeesAsync(int? empId)
        {
            var list = new List<MonthlyReportBuilder.EmployeeInfo>();
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT c_empid, c_name, c_status
                  FROM t_aemployee
                 WHERE c_empid != 1 AND c_role != 'Admin'
                   AND (@emp IS NULL OR c_empid = @emp)
                 ORDER BY c_name;", conn);
            cmd.Parameters.AddWithValue("emp", NpgsqlDbType.Integer, (object?)empId ?? DBNull.Value);

            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
                list.Add(new MonthlyReportBuilder.EmployeeInfo(rd.GetInt32(0), rd.GetString(1), rd.IsDBNull(2) ? "" : rd.GetString(2)));
            return list;
        }

        private async Task<List<AttendanceModel>> GetAttendanceAsync(DateTime from, DateTime to, int? empId)
        {
            var list = new List<AttendanceModel>();
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT c_attendid, c_empid, c_attenddate, c_clockinhour, c_clockinmin,
                       c_clockouthour, c_clockoutmin, c_worktype, c_tasktype, c_workinghour, c_attendstatus
                  FROM t_aattendance
                 WHERE c_attenddate BETWEEN @from AND @to
                   AND (@emp IS NULL OR c_empid = @emp);", conn);
            cmd.Parameters.AddWithValue("from", NpgsqlDbType.Date, from.Date);
            cmd.Parameters.AddWithValue("to", NpgsqlDbType.Date, to.Date);
            cmd.Parameters.AddWithValue("emp", NpgsqlDbType.Integer, (object?)empId ?? DBNull.Value);

            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                list.Add(new AttendanceModel
                {
                    AttendId = rd.GetInt32(0),
                    EmpId = rd.GetInt32(1),
                    EmpName = "",
                    AttendDate = rd.GetDateTime(2),
                    ClockInHour = rd.IsDBNull(3) ? 0 : rd.GetInt32(3),
                    ClockInMin = rd.IsDBNull(4) ? 0 : rd.GetInt32(4),
                    ClockOutHour = rd.IsDBNull(5) ? 0 : rd.GetInt32(5),
                    ClockOutMin = rd.IsDBNull(6) ? 0 : rd.GetInt32(6),
                    WorkType = rd.IsDBNull(7) ? "" : rd.GetString(7),
                    TaskType = rd.IsDBNull(8) ? null : rd.GetString(8),
                    WorkingHour = rd.IsDBNull(9) ? 0 : rd.GetInt32(9),
                    AttendStatus = rd.IsDBNull(10) ? AttendanceRules.Regular : rd.GetString(10),
                });
            }
            return list;
        }
    }
}
