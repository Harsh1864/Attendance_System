using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using project.Models;

namespace project.Services
{
    public class LeaveService
    {
        private const string SelectColumns = @"
            l.c_leaveid, l.c_empid, e.c_name, l.c_fromdate, l.c_todate, l.c_leavetype,
            l.c_halfday, l.c_days, l.c_reason, l.c_status, l.c_reviewedby, r.c_name,
            l.c_reviewnote, l.c_reviewedat, l.c_createdat";

        private const string FromJoins = @"
            FROM t_aleave l
            INNER JOIN t_aemployee e ON e.c_empid = l.c_empid
            LEFT JOIN t_aemployee r ON r.c_empid = l.c_reviewedby";

        private readonly DbConnectionFactory _db;
        private readonly HolidayService _holidays;
        private readonly LeaveOptions _options;

        public LeaveService(DbConnectionFactory db, HolidayService holidays, IOptions<LeaveOptions> options)
        {
            _db = db;
            _holidays = holidays;
            _options = options.Value;
        }

        public LeaveOptions Options => _options;

        public async Task<List<LeaveModel>> GetForEmployeeAsync(int empId)
        {
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand($@"
                SELECT {SelectColumns} {FromJoins}
                 WHERE l.c_empid = @emp
                 ORDER BY l.c_fromdate DESC, l.c_leaveid DESC;", conn);
            cmd.Parameters.AddWithValue("emp", NpgsqlDbType.Integer, empId);
            return await ReadAllAsync(cmd);
        }

        /// <summary>All requests overlapping a date range, optionally filtered by status.</summary>
        public async Task<List<LeaveModel>> SearchAsync(DateTime from, DateTime to, string? status, int? empId = null)
        {
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand($@"
                SELECT {SelectColumns} {FromJoins}
                 WHERE l.c_fromdate <= @to AND l.c_todate >= @from
                   AND (@status IS NULL OR l.c_status = @status)
                   AND (@emp IS NULL OR l.c_empid = @emp)
                 ORDER BY (l.c_status = 'Pending') DESC, l.c_fromdate ASC, e.c_name ASC;", conn);
            cmd.Parameters.AddWithValue("from", NpgsqlDbType.Date, from.Date);
            cmd.Parameters.AddWithValue("to", NpgsqlDbType.Date, to.Date);
            cmd.Parameters.AddWithValue("status", NpgsqlDbType.Varchar, string.IsNullOrWhiteSpace(status) ? DBNull.Value : status);
            cmd.Parameters.AddWithValue("emp", NpgsqlDbType.Integer, (object?)empId ?? DBNull.Value);
            return await ReadAllAsync(cmd);
        }

        public async Task<int> CountPendingAsync()
        {
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM t_aleave WHERE c_status = 'Pending';", conn);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task<LeaveModel?> GetAsync(int leaveId)
        {
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand($"SELECT {SelectColumns} {FromJoins} WHERE l.c_leaveid = @id;", conn);
            cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, leaveId);
            var list = await ReadAllAsync(cmd);
            return list.Count > 0 ? list[0] : null;
        }

        public async Task<List<LeaveBalanceModel>> GetBalancesAsync(int empId, int year)
        {
            var leaves = await GetForEmployeeAsync(empId);
            return LeaveRules.Balances(leaves, year, _options);
        }

        public async Task<decimal> CountDaysAsync(DateTime from, DateTime to, bool halfDay)
        {
            if (to.Date < from.Date) return 0;
            var holidays = await _holidays.GetDatesAsync(from, to);
            return LeaveRules.CountDays(from, to, holidays, _options.SkipWeekends, halfDay);
        }

        /// <summary>Validates and saves a request. Returns (leaveId, null) or (0, error).</summary>
        public async Task<(int id, decimal days, string? error)> ApplyAsync(int empId, LeaveRequestInput input, DateTime today)
        {
            decimal days = 0;
            if (input.FromDate.HasValue && input.ToDate.HasValue)
                days = await CountDaysAsync(input.FromDate.Value, input.ToDate.Value, input.HalfDay);

            var existing = await GetForEmployeeAsync(empId);
            var year = input.FromDate?.Year ?? today.Year;
            var balances = LeaveRules.Balances(existing, year, _options);

            var error = LeaveRules.Validate(input, days, today, existing, balances, _options);
            if (error != null) return (0, days, error);

            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO t_aleave (c_empid, c_fromdate, c_todate, c_leavetype, c_halfday, c_days, c_reason)
                VALUES (@emp, @from, @to, @type, @half, @days, @reason)
                RETURNING c_leaveid;", conn);
            cmd.Parameters.AddWithValue("emp", NpgsqlDbType.Integer, empId);
            cmd.Parameters.AddWithValue("from", NpgsqlDbType.Date, input.FromDate!.Value.Date);
            cmd.Parameters.AddWithValue("to", NpgsqlDbType.Date, input.ToDate!.Value.Date);
            cmd.Parameters.AddWithValue("type", NpgsqlDbType.Varchar, input.LeaveType!);
            cmd.Parameters.AddWithValue("half", NpgsqlDbType.Boolean, input.HalfDay);
            cmd.Parameters.AddWithValue("days", NpgsqlDbType.Numeric, days);
            cmd.Parameters.AddWithValue("reason", NpgsqlDbType.Varchar,
                string.IsNullOrWhiteSpace(input.Reason) ? DBNull.Value : input.Reason.Trim());

            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return (id, days, null);
        }

        /// <summary>Cancels the employee's own request when the rules allow it.</summary>
        public async Task<string?> CancelAsync(int empId, int leaveId, DateTime today)
        {
            var leave = await GetAsync(leaveId);
            if (leave == null || leave.EmpId != empId) return "Leave request not found.";
            if (!LeaveRules.CanCancel(leave.Status, leave.FromDate, today))
                return leave.Status == LeaveRules.Approved
                    ? "Approved leave can only be cancelled before it starts."
                    : $"This request is already {leave.Status.ToLowerInvariant()}.";

            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                UPDATE t_aleave SET c_status = 'Cancelled'
                 WHERE c_leaveid = @id AND c_empid = @emp AND c_status IN ('Pending', 'Approved');", conn);
            cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, leaveId);
            cmd.Parameters.AddWithValue("emp", NpgsqlDbType.Integer, empId);
            return await cmd.ExecuteNonQueryAsync() > 0 ? null : "Leave request could not be cancelled.";
        }

        /// <summary>Approves or rejects a pending request. Returns (updated leave, null) or (null, error).</summary>
        public async Task<(LeaveModel? leave, string? error)> ReviewAsync(int leaveId, bool approve, int? reviewerId, string? note)
        {
            if (note != null && note.Trim().Length > LeaveRules.MaxReasonLength)
                return (null, $"Note cannot exceed {LeaveRules.MaxReasonLength} characters.");

            var leave = await GetAsync(leaveId);
            if (leave == null) return (null, "Leave request not found.");
            if (!LeaveRules.CanReview(leave.Status)) return (null, $"This request is already {leave.Status.ToLowerInvariant()}.");
            if (reviewerId.HasValue && reviewerId.Value == leave.EmpId) return (null, "You cannot review your own leave.");

            var newStatus = approve ? LeaveRules.Approved : LeaveRules.Rejected;

            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                UPDATE t_aleave
                   SET c_status = @status, c_reviewedby = @by, c_reviewnote = @note, c_reviewedat = NOW()
                 WHERE c_leaveid = @id AND c_status = 'Pending';", conn);
            cmd.Parameters.AddWithValue("status", NpgsqlDbType.Varchar, newStatus);
            cmd.Parameters.AddWithValue("by", NpgsqlDbType.Integer, (object?)reviewerId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("note", NpgsqlDbType.Varchar, string.IsNullOrWhiteSpace(note) ? DBNull.Value : note.Trim());
            cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, leaveId);

            if (await cmd.ExecuteNonQueryAsync() == 0)
                return (null, "This request was changed by someone else. Please refresh.");

            leave.Status = newStatus;
            leave.ReviewNote = note?.Trim();
            return (leave, null);
        }

        private static async Task<List<LeaveModel>> ReadAllAsync(NpgsqlCommand cmd)
        {
            var list = new List<LeaveModel>();
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                list.Add(new LeaveModel
                {
                    LeaveId = rd.GetInt32(0),
                    EmpId = rd.GetInt32(1),
                    EmpName = rd.GetString(2),
                    FromDate = rd.GetDateTime(3),
                    ToDate = rd.GetDateTime(4),
                    LeaveType = rd.GetString(5),
                    HalfDay = rd.GetBoolean(6),
                    Days = rd.GetDecimal(7),
                    Reason = rd.IsDBNull(8) ? null : rd.GetString(8),
                    Status = rd.GetString(9),
                    ReviewedBy = rd.IsDBNull(10) ? null : rd.GetInt32(10),
                    ReviewedByName = rd.IsDBNull(11) ? null : rd.GetString(11),
                    ReviewNote = rd.IsDBNull(12) ? null : rd.GetString(12),
                    ReviewedAt = rd.IsDBNull(13) ? null : rd.GetDateTime(13),
                    CreatedAt = rd.GetDateTime(14),
                });
            }
            return list;
        }
    }
}
