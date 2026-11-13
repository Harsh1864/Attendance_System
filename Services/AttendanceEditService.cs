using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;

namespace project.Services
{
    /// <summary>Attendance row as shown on the admin edit page.</summary>
    public class AttendanceRecordDto
    {
        public int AttendId { get; set; }
        public int EmpId { get; set; }
        public string EmpName { get; set; } = "";
        public DateTime Date { get; set; }
        public string? ClockIn { get; set; }
        public string? ClockOut { get; set; }
        public string? WorkType { get; set; }
        public string? TaskType { get; set; }
        public int? WorkingHours { get; set; }
        public string Status { get; set; } = "";
    }

    public class AttendanceEditService
    {
        private const string SelectSql = @"
            SELECT a.c_attendid, a.c_empid, e.c_name, a.c_attenddate,
                   a.c_clockinhour, a.c_clockinmin, a.c_clockouthour, a.c_clockoutmin,
                   a.c_worktype, a.c_tasktype, a.c_workinghour, a.c_attendstatus
              FROM t_aattendance a
              INNER JOIN t_aemployee e ON e.c_empid = a.c_empid";

        private readonly DbConnectionFactory _db;

        public AttendanceEditService(DbConnectionFactory db) => _db = db;

        public async Task<List<AttendanceRecordDto>> SearchAsync(int? empId, DateTime from, DateTime to)
        {
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(SelectSql + @"
                 WHERE a.c_attenddate BETWEEN @from AND @to
                   AND (@emp IS NULL OR a.c_empid = @emp)
                   AND e.c_empid != 1
                 ORDER BY a.c_attenddate DESC, e.c_name ASC
                 LIMIT 2000;", conn);
            cmd.Parameters.AddWithValue("from", NpgsqlDbType.Date, from.Date);
            cmd.Parameters.AddWithValue("to", NpgsqlDbType.Date, to.Date);
            cmd.Parameters.AddWithValue("emp", NpgsqlDbType.Integer, (object?)empId ?? DBNull.Value);
            return await ReadAsync(cmd);
        }

        public async Task<AttendanceRecordDto?> GetAsync(int attendId)
        {
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(SelectSql + " WHERE a.c_attendid = @id;", conn);
            cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, attendId);
            var list = await ReadAsync(cmd);
            return list.Count > 0 ? list[0] : null;
        }

        public async Task<bool> IsEditableEmployeeAsync(int empId)
        {
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM t_aemployee WHERE c_empid = @id AND c_empid != 1 AND c_role != 'Admin';", conn);
            cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, empId);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        /// <summary>
        /// Inserts or updates a record. Returns (attendId, previous record or null, error).
        /// An employee can only have one record per day.
        /// </summary>
        public async Task<(int id, AttendanceRecordDto? before, string? error)> SaveAsync(
            int? attendId, int empId, AttendanceEditResult r)
        {
            await using var conn = await _db.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();

            AttendanceRecordDto? before = null;
            if (attendId.HasValue)
            {
                await using var get = new NpgsqlCommand(SelectSql + " WHERE a.c_attendid = @id FOR UPDATE OF a;", conn, tx);
                get.Parameters.AddWithValue("id", NpgsqlDbType.Integer, attendId.Value);
                var rows = await ReadAsync(get);
                if (rows.Count == 0) return (0, null, "Attendance record not found.");
                before = rows[0];
                if (before.EmpId != empId) return (0, null, "A record cannot be moved to another employee.");
            }

            await using (var dup = new NpgsqlCommand(@"
                SELECT c_attendid FROM t_aattendance
                 WHERE c_empid = @emp AND c_attenddate = @date AND (@id IS NULL OR c_attendid != @id)
                 LIMIT 1;", conn, tx))
            {
                dup.Parameters.AddWithValue("emp", NpgsqlDbType.Integer, empId);
                dup.Parameters.AddWithValue("date", NpgsqlDbType.Date, r.Date);
                dup.Parameters.AddWithValue("id", NpgsqlDbType.Integer, (object?)attendId ?? DBNull.Value);
                if (await dup.ExecuteScalarAsync() != null)
                    return (0, null, $"This employee already has a record on {r.Date:dd-MMM-yyyy}. Edit that one instead.");
            }

            var sql = attendId.HasValue
                ? @"UPDATE t_aattendance SET
                        c_attenddate = @date, c_clockinhour = @inh, c_clockinmin = @inm,
                        c_clockouthour = @outh, c_clockoutmin = @outm, c_worktype = @work,
                        c_tasktype = @task, c_workinghour = @hours, c_attendstatus = @status
                    WHERE c_attendid = @id
                    RETURNING c_attendid;"
                : @"INSERT INTO t_aattendance
                        (c_empid, c_attenddate, c_clockinhour, c_clockinmin, c_clockouthour, c_clockoutmin,
                         c_worktype, c_tasktype, c_workinghour, c_attendstatus)
                    VALUES (@emp, @date, @inh, @inm, @outh, @outm, @work, @task, @hours, @status)
                    RETURNING c_attendid;";

            await using var save = new NpgsqlCommand(sql, conn, tx);
            if (attendId.HasValue)
                save.Parameters.AddWithValue("id", NpgsqlDbType.Integer, attendId.Value);
            else
                save.Parameters.AddWithValue("emp", NpgsqlDbType.Integer, empId);
            save.Parameters.AddWithValue("date", NpgsqlDbType.Date, r.Date);
            save.Parameters.AddWithValue("inh", NpgsqlDbType.Integer, r.ClockIn.HasValue ? r.ClockIn.Value.Hours : DBNull.Value);
            save.Parameters.AddWithValue("inm", NpgsqlDbType.Integer, r.ClockIn.HasValue ? r.ClockIn.Value.Minutes : DBNull.Value);
            save.Parameters.AddWithValue("outh", NpgsqlDbType.Integer, r.ClockOut.HasValue ? r.ClockOut.Value.Hours : DBNull.Value);
            save.Parameters.AddWithValue("outm", NpgsqlDbType.Integer, r.ClockOut.HasValue ? r.ClockOut.Value.Minutes : DBNull.Value);
            save.Parameters.AddWithValue("work", NpgsqlDbType.Varchar, (object?)r.WorkType ?? DBNull.Value);
            save.Parameters.AddWithValue("task", NpgsqlDbType.Varchar, (object?)r.TaskType ?? DBNull.Value);
            save.Parameters.AddWithValue("hours", NpgsqlDbType.Integer, (object?)r.WorkingHours ?? DBNull.Value);
            save.Parameters.AddWithValue("status", NpgsqlDbType.Varchar, r.Status);

            var id = Convert.ToInt32(await save.ExecuteScalarAsync());
            await tx.CommitAsync();
            return (id, before, null);
        }

        public async Task<AttendanceRecordDto?> DeleteAsync(int attendId)
        {
            var existing = await GetAsync(attendId);
            if (existing == null) return null;

            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand("DELETE FROM t_aattendance WHERE c_attendid = @id;", conn);
            cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, attendId);
            return await cmd.ExecuteNonQueryAsync() > 0 ? existing : null;
        }

        public static string Describe(AttendanceRecordDto r) =>
            r.Status == AttendanceRules.Absent
                ? $"{r.Date:yyyy-MM-dd} Absent"
                : $"{r.Date:yyyy-MM-dd} {r.ClockIn ?? "--:--"}-{r.ClockOut ?? "--:--"} {r.WorkType} {r.Status}" +
                  (r.WorkingHours.HasValue ? $" {r.WorkingHours}h" : "");

        private static async Task<List<AttendanceRecordDto>> ReadAsync(NpgsqlCommand cmd)
        {
            var list = new List<AttendanceRecordDto>();
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                string? Time(int h, int m) =>
                    rd.IsDBNull(h) || rd.IsDBNull(m) ? null : CsvExport.FormatTime(rd.GetInt32(h), rd.GetInt32(m));

                list.Add(new AttendanceRecordDto
                {
                    AttendId = rd.GetInt32(0),
                    EmpId = rd.GetInt32(1),
                    EmpName = rd.GetString(2),
                    Date = rd.GetDateTime(3),
                    ClockIn = Time(4, 5),
                    ClockOut = Time(6, 7),
                    WorkType = rd.IsDBNull(8) ? null : rd.GetString(8),
                    TaskType = rd.IsDBNull(9) ? null : rd.GetString(9),
                    WorkingHours = rd.IsDBNull(10) ? null : rd.GetInt32(10),
                    Status = rd.IsDBNull(11) ? AttendanceRules.Regular : rd.GetString(11),
                });
            }
            return list;
        }
    }
}
