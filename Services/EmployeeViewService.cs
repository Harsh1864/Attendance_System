using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;

namespace try_project_casepoint.Services
{
    public class EmployeeViewService
    {
        private readonly NpgsqlConnection _con;
        public EmployeeViewService(NpgsqlConnection con) => _con = con;

        private static readonly TimeSpan ShiftStart = new(9, 15, 0); // 09:15
        private static readonly TimeSpan ShiftEnd   = new(17, 0, 0); // 17:00

       

        public async Task<(bool ok, string code)> ClockInAsync(int empId, string workType, string taskCsv)
        {
            try
            {
                if (_con.State != System.Data.ConnectionState.Open)
            {
                await _con.OpenAsync();
            }

                int? attendId = null;
                int? inH = null, inM = null;
                int? outH = null, outM = null;

                await using (var check = new NpgsqlCommand(@"
                    SELECT c_attendid, c_clockinhour, c_clockinmin, c_clockouthour, c_clockoutmin
                      FROM t_aattendance
                     WHERE c_empid=@e AND c_attenddate=CURRENT_DATE
                     LIMIT 1;", _con))
                {
                    check.Parameters.AddWithValue("@e", empId);
                    await using var rd = await check.ExecuteReaderAsync();
                    if (await rd.ReadAsync())
                    {
                        attendId = rd.GetInt32(0);
                        inH  = rd.IsDBNull(1) ? null : rd.GetInt32(1);
                        inM  = rd.IsDBNull(2) ? null : rd.GetInt32(2);
                        outH = rd.IsDBNull(3) ? null : rd.GetInt32(3);
                        outM = rd.IsDBNull(4) ? null : rd.GetInt32(4);
                    }
                }

                if (attendId.HasValue && inH.HasValue)
                {
                    if (outH.HasValue) return (false, "COMPLETED_TODAY");
                    return (false, "ALREADY_CHECKED_IN");
                }

                var now = DateTime.Now;
                var isLate = now.TimeOfDay > ShiftStart;
                var status = isLate ? "LateIn" : "Regular";

                if (!attendId.HasValue)
                {
                    await using var ins = new NpgsqlCommand(@"
                        INSERT INTO t_aattendance
                          (c_empid, c_attenddate, c_clockinhour, c_clockinmin, c_worktype, c_tasktype, c_attendstatus)
                        VALUES
                          (@e, CURRENT_DATE, @h, @m, @work, @task, @status);", _con);
                    ins.Parameters.AddWithValue("@e", empId);
                    ins.Parameters.AddWithValue("@h", now.Hour);
                    ins.Parameters.AddWithValue("@m", now.Minute);
                    ins.Parameters.AddWithValue("@work", workType ?? "");
                    ins.Parameters.AddWithValue("@task", taskCsv ?? "");
                    ins.Parameters.AddWithValue("@status", status);
                    await ins.ExecuteNonQueryAsync();
                }
                else
                {
                    await using var upd = new NpgsqlCommand(@"
                        UPDATE t_aattendance SET
                          c_clockinhour=@h, c_clockinmin=@m,
                          c_worktype=@work, c_tasktype=@task, c_attendstatus=@status
                        WHERE c_attendid=@id;", _con);
                    upd.Parameters.AddWithValue("@h", now.Hour);
                    upd.Parameters.AddWithValue("@m", now.Minute);
                    upd.Parameters.AddWithValue("@work", workType ?? "");
                    upd.Parameters.AddWithValue("@task", taskCsv ?? "");
                    upd.Parameters.AddWithValue("@status", status);
                    upd.Parameters.AddWithValue("@id", attendId.Value);
                    await upd.ExecuteNonQueryAsync();
                }

                return (true, "OK");
            }
            catch
            {
                return (false, "ERROR");
            }
            finally { await _con.CloseAsync(); }
        }

        public async Task<(bool ok, int workedHours, string status, string code)> ClockOutAsync(int empId)
        {
            try
            {
                if (_con.State != System.Data.ConnectionState.Open)
            {
                await _con.OpenAsync();
            }

                int? attendId = null;
                int? inH = null, inM = null;
                int? outH = null, outM = null;

                await using (var get = new NpgsqlCommand(@"
                    SELECT c_attendid, c_clockinhour, c_clockinmin, c_clockouthour, c_clockoutmin
                      FROM t_aattendance
                     WHERE c_empid=@e AND c_attenddate=CURRENT_DATE
                     LIMIT 1;", _con))
                {
                    get.Parameters.AddWithValue("@e", empId);
                    await using var rd = await get.ExecuteReaderAsync();
                    if (!await rd.ReadAsync())
                        return (false, 0, "Regular", "NO_TODAY_ROW");

                    attendId = rd.GetInt32(0);
                    inH  = rd.IsDBNull(1) ? null : rd.GetInt32(1);
                    inM  = rd.IsDBNull(2) ? null : rd.GetInt32(2);
                    outH = rd.IsDBNull(3) ? null : rd.GetInt32(3);
                    outM = rd.IsDBNull(4) ? null : rd.GetInt32(4);
                }

                if (outH.HasValue) return (false, 0, "Regular", "ALREADY_CHECKED_OUT");

                var now = DateTime.Now;

                int workedHours = 1; // to satisfy CHECK (>0)
                bool late = false, early = now.TimeOfDay < ShiftEnd;

                if (inH.HasValue && inM.HasValue)
                {
                    var cin = new DateTime(now.Year, now.Month, now.Day, inH.Value, inM.Value, 0);
                    var totalMinutes = (now - cin).TotalMinutes;
                    workedHours = (int)Math.Floor(totalMinutes / 60.0);
                    if (workedHours < 1) workedHours = 1;
                    late = cin.TimeOfDay > ShiftStart;
                }

                var status = (late, early) switch
                {
                    (true, true)  => "LateEarly",
                    (true, false) => "LateIn",
                    (false, true) => "EarlyOut",
                    _             => "Regular"
                };

                await using (var upd = new NpgsqlCommand(@"
                    UPDATE t_aattendance SET
                      c_clockouthour=@h, c_clockoutmin=@m,
                      c_workinghour=@hr,
                      c_attendstatus=@status
                    WHERE c_attendid=@id;", _con))
                {
                    upd.Parameters.AddWithValue("@h", now.Hour);
                    upd.Parameters.AddWithValue("@m", now.Minute);
                    upd.Parameters.AddWithValue("@hr", workedHours);
                    upd.Parameters.AddWithValue("@status", status);
                    upd.Parameters.AddWithValue("@id", attendId!.Value);
                    await upd.ExecuteNonQueryAsync();
                }

                return (true, workedHours, status, "OK");
            }
            catch
            {
                return (false, 0, "Regular", "ERROR");
            }
            finally { await _con.CloseAsync(); }
        }
    }
}