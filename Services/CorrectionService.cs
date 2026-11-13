using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;

namespace project.Services
{
    public class CorrectionModel
    {
        public int CorrectionId { get; set; }
        public int EmpId { get; set; }
        public string EmpName { get; set; } = "";
        public DateTime Date { get; set; }
        public string ClockIn { get; set; } = "";
        public string? ClockOut { get; set; }
        public string WorkType { get; set; } = "";
        public string? TaskType { get; set; }
        public string Reason { get; set; } = "";
        public string Status { get; set; } = "";
        public string? ReviewedByName { get; set; }
        public string? ReviewNote { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>The record currently stored for that day, for comparison ("09:00-17:00 Regular" or null).</summary>
        public string? CurrentRecord { get; set; }
    }

    public static class CorrectionRules
    {
        /// <summary>How far back an employee may ask for a correction.</summary>
        public const int MaxDaysBack = 30;

        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Cancelled = "Cancelled";

        /// <summary>
        /// Validates an employee's request using the same rules as an admin edit,
        /// plus a 30-day window. Employees cannot request "absent".
        /// </summary>
        public static (AttendanceEditResult? result, string? error) Validate(
            AttendanceEditInput input, DateTime today, TimeSpan shiftStart, TimeSpan shiftEnd)
        {
            if (input.Date.HasValue && input.Date.Value.Date < today.Date.AddDays(-MaxDaysBack))
                return (null, $"Corrections can only be requested for the last {MaxDaysBack} days.");

            input.Absent = false;
            return AttendanceEditRules.Validate(input, today, shiftStart, shiftEnd);
        }

        public static bool CanCancel(string status) => status == Pending;
        public static bool CanReview(string status) => status == Pending;
    }

    public class CorrectionService
    {
        private const string SelectSql = @"
            SELECT c.c_correctionid, c.c_empid, e.c_name, c.c_date, c.c_clockin, c.c_clockout,
                   c.c_worktype, c.c_tasktype, c.c_reason, c.c_status, r.c_name, c.c_reviewnote,
                   c.c_reviewedat, c.c_createdat,
                   a.c_clockinhour, a.c_clockinmin, a.c_clockouthour, a.c_clockoutmin, a.c_attendstatus
              FROM t_acorrection c
              INNER JOIN t_aemployee e ON e.c_empid = c.c_empid
              LEFT JOIN t_aemployee r ON r.c_empid = c.c_reviewedby
              LEFT JOIN t_aattendance a ON a.c_empid = c.c_empid AND a.c_attenddate = c.c_date";

        private readonly DbConnectionFactory _db;
        private readonly AttendanceEditService _edit;

        public CorrectionService(DbConnectionFactory db, AttendanceEditService edit)
        {
            _db = db;
            _edit = edit;
        }

        public async Task<List<CorrectionModel>> ListForEmployeeAsync(int empId)
        {
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(SelectSql + @"
                 WHERE c.c_empid = @emp
                 ORDER BY c.c_createdat DESC
                 LIMIT 200;", conn);
            cmd.Parameters.AddWithValue("emp", NpgsqlDbType.Integer, empId);
            return await ReadAsync(cmd);
        }

        public async Task<List<CorrectionModel>> SearchAsync(string? status, DateTime from, DateTime to)
        {
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(SelectSql + @"
                 WHERE c.c_date BETWEEN @from AND @to
                   AND (@status IS NULL OR c.c_status = @status)
                 ORDER BY (c.c_status = 'Pending') DESC, c.c_date DESC, e.c_name
                 LIMIT 1000;", conn);
            cmd.Parameters.AddWithValue("from", NpgsqlDbType.Date, from.Date);
            cmd.Parameters.AddWithValue("to", NpgsqlDbType.Date, to.Date);
            cmd.Parameters.AddWithValue("status", NpgsqlDbType.Varchar, string.IsNullOrWhiteSpace(status) ? DBNull.Value : status);
            return await ReadAsync(cmd);
        }

        public async Task<CorrectionModel?> GetAsync(int id)
        {
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(SelectSql + " WHERE c.c_correctionid = @id;", conn);
            cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, id);
            var list = await ReadAsync(cmd);
            return list.Count > 0 ? list[0] : null;
        }

        public async Task<int> CountPendingAsync()
        {
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM t_acorrection WHERE c_status = 'Pending';", conn);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task<(int id, string? error)> CreateAsync(int empId, AttendanceEditInput input, AttendanceEditResult r)
        {
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO t_acorrection (c_empid, c_date, c_clockin, c_clockout, c_worktype, c_tasktype, c_reason)
                VALUES (@emp, @date, @in, @out, @work, @task, @reason)
                ON CONFLICT DO NOTHING
                RETURNING c_correctionid;", conn);
            cmd.Parameters.AddWithValue("emp", NpgsqlDbType.Integer, empId);
            cmd.Parameters.AddWithValue("date", NpgsqlDbType.Date, r.Date);
            cmd.Parameters.AddWithValue("in", NpgsqlDbType.Varchar, $"{r.ClockIn!.Value:hh\\:mm}");
            cmd.Parameters.AddWithValue("out", NpgsqlDbType.Varchar, r.ClockOut.HasValue ? $"{r.ClockOut.Value:hh\\:mm}" : DBNull.Value);
            cmd.Parameters.AddWithValue("work", NpgsqlDbType.Varchar, r.WorkType!);
            cmd.Parameters.AddWithValue("task", NpgsqlDbType.Varchar, (object?)r.TaskType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("reason", NpgsqlDbType.Varchar, input.Reason!.Trim());

            var result = await cmd.ExecuteScalarAsync();
            return result is int id
                ? (id, null)
                : (0, $"You already have a pending correction for {r.Date:dd-MMM-yyyy}. Cancel it first to send a new one.");
        }

        public async Task<string?> CancelAsync(int empId, int id)
        {
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                UPDATE t_acorrection SET c_status = 'Cancelled'
                 WHERE c_correctionid = @id AND c_empid = @emp AND c_status = 'Pending';", conn);
            cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, id);
            cmd.Parameters.AddWithValue("emp", NpgsqlDbType.Integer, empId);
            return await cmd.ExecuteNonQueryAsync() > 0 ? null : "This request can no longer be cancelled.";
        }

        /// <summary>
        /// Approves (and applies to attendance) or rejects a pending request.
        /// The request is claimed first so two admins cannot both act on it.
        /// </summary>
        public async Task<(CorrectionModel? correction, AttendanceEditResult? applied, string? error)> ReviewAsync(
            int id, bool approve, int? reviewerId, string? note, DateTime today, TimeSpan shiftStart, TimeSpan shiftEnd)
        {
            if (note != null && note.Trim().Length > AttendanceEditRules.MaxReasonLength)
                return (null, null, $"Note cannot exceed {AttendanceEditRules.MaxReasonLength} characters.");

            var c = await GetAsync(id);
            if (c == null) return (null, null, "Correction request not found.");
            if (!CorrectionRules.CanReview(c.Status)) return (null, null, $"This request is already {c.Status.ToLowerInvariant()}.");
            if (reviewerId.HasValue && reviewerId.Value == c.EmpId) return (null, null, "You cannot review your own request.");

            AttendanceEditResult? result = null;
            if (approve)
            {
                // Re-check with today's date and current shift settings before applying.
                var (r, error) = AttendanceEditRules.Validate(new AttendanceEditInput
                {
                    EmpId = c.EmpId, Date = c.Date, ClockIn = c.ClockIn, ClockOut = c.ClockOut,
                    WorkType = c.WorkType, TaskType = c.TaskType, Reason = c.Reason,
                }, today, shiftStart, shiftEnd);
                if (error != null) return (null, null, "Cannot apply this request: " + error);
                result = r;
            }

            var newStatus = approve ? CorrectionRules.Approved : CorrectionRules.Rejected;
            if (!await SetStatusAsync(id, CorrectionRules.Pending, newStatus, reviewerId, note))
                return (null, null, "This request was changed by someone else. Please refresh.");

            if (approve)
            {
                var existingId = await _edit.FindIdAsync(c.EmpId, c.Date);
                var (attendId, _, saveError) = await _edit.SaveAsync(existingId, c.EmpId, result!);
                if (saveError != null)
                {
                    // Put the request back so it can be reviewed again.
                    await SetStatusAsync(id, newStatus, CorrectionRules.Pending, null, null);
                    return (null, null, saveError);
                }
                await LinkAttendanceAsync(id, attendId);
            }

            c.Status = newStatus;
            c.ReviewNote = note?.Trim();
            return (c, result, null);
        }

        private async Task<bool> SetStatusAsync(int id, string from, string to, int? reviewerId, string? note)
        {
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                UPDATE t_acorrection
                   SET c_status = @to,
                       c_reviewedby = @by,
                       c_reviewnote = @note,
                       c_reviewedat = CASE WHEN @to = 'Pending' THEN NULL ELSE NOW() END
                 WHERE c_correctionid = @id AND c_status = @from;", conn);
            cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, id);
            cmd.Parameters.AddWithValue("from", NpgsqlDbType.Varchar, from);
            cmd.Parameters.AddWithValue("to", NpgsqlDbType.Varchar, to);
            cmd.Parameters.AddWithValue("by", NpgsqlDbType.Integer, (object?)reviewerId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("note", NpgsqlDbType.Varchar, string.IsNullOrWhiteSpace(note) ? DBNull.Value : note.Trim());
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        private async Task LinkAttendanceAsync(int id, int attendId)
        {
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "UPDATE t_acorrection SET c_attendid = @a WHERE c_correctionid = @id;", conn);
            cmd.Parameters.AddWithValue("a", NpgsqlDbType.Integer, attendId);
            cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, id);
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task<List<CorrectionModel>> ReadAsync(NpgsqlCommand cmd)
        {
            var list = new List<CorrectionModel>();
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                string? current = null;
                if (!rd.IsDBNull(18))
                {
                    var status = rd.GetString(18);
                    if (status == AttendanceRules.Absent) current = "Absent";
                    else
                    {
                        var cin = rd.IsDBNull(14) || rd.IsDBNull(15) ? "--:--" : CsvExport.FormatTime(rd.GetInt32(14), rd.GetInt32(15));
                        var cout = rd.IsDBNull(16) || rd.IsDBNull(17) ? "--:--" : CsvExport.FormatTime(rd.GetInt32(16), rd.GetInt32(17));
                        current = $"{cin}-{cout} {status}";
                    }
                }

                list.Add(new CorrectionModel
                {
                    CorrectionId = rd.GetInt32(0),
                    EmpId = rd.GetInt32(1),
                    EmpName = rd.GetString(2),
                    Date = rd.GetDateTime(3),
                    ClockIn = rd.GetString(4),
                    ClockOut = rd.IsDBNull(5) ? null : rd.GetString(5),
                    WorkType = rd.GetString(6),
                    TaskType = rd.IsDBNull(7) ? null : rd.GetString(7),
                    Reason = rd.GetString(8),
                    Status = rd.GetString(9),
                    ReviewedByName = rd.IsDBNull(10) ? null : rd.GetString(10),
                    ReviewNote = rd.IsDBNull(11) ? null : rd.GetString(11),
                    ReviewedAt = rd.IsDBNull(12) ? null : rd.GetDateTime(12),
                    CreatedAt = rd.GetDateTime(13),
                    CurrentRecord = current,
                });
            }
            return list;
        }
    }
}
