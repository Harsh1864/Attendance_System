using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MVC.Services;
using Npgsql;
using try_project_casepoint.Services;

namespace try_project_casepoint.Controllers
{
    public class EmployeeViewController : Controller
    {
        private readonly EmployeeViewService _svc;
        private readonly NpgsqlConnection _con;

        private readonly RabbitMqService _mq;
        private readonly IChatService _chat;

        public EmployeeViewController(EmployeeViewService svc, NpgsqlConnection con, RabbitMqService mq, IChatService chat)
        {
            _svc = svc;
            _con = con;
            _mq = mq;
            _chat = chat;
        }

        [HttpGet("/EmployeeView/index/{empId}")]
        public IActionResult Index(int empId)
        {
            ViewBag.EmpId = empId;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployee(int empId)
        {
            if (_con.State != System.Data.ConnectionState.Open)
            {
                await _con.OpenAsync();
            }
            try
            {
                using var cmd = new NpgsqlCommand(@"
                    SELECT c_empid, c_name, c_email, c_status, c_role
                      FROM t_aemployee
                     WHERE c_empid = @id;", _con);
                cmd.Parameters.AddWithValue("@id", empId);
                using var rd = await cmd.ExecuteReaderAsync();
                if (!await rd.ReadAsync()) return NotFound();

                return Json(new {
                    empId = rd.GetInt32(0),
                    name = rd.GetString(1),
                    email = rd.GetString(2),
                    status = rd.GetString(3),
                    role = rd.GetString(4)
                });
            }
            finally { await _con.CloseAsync(); }
        }

        [HttpGet]
        public async Task<IActionResult> AttendanceList(int empId)
        {
            var list = new List<object>();
            if (_con.State != System.Data.ConnectionState.Open)
            {
                await _con.OpenAsync();
            }
            try
            {
                using var cmd = new NpgsqlCommand(@"
                    SELECT c_attendid, c_attenddate,
                           c_clockinhour, c_clockinmin,
                           c_clockouthour, c_clockoutmin,
                           c_worktype, c_tasktype,
                           c_workinghour, c_attendstatus
                      FROM t_aattendance
                     WHERE c_empid = @e
                       AND c_attenddate >= CURRENT_DATE - INTERVAL '7 days'
                     ORDER BY c_attenddate DESC;", _con);
                cmd.Parameters.AddWithValue("@e", empId);
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    var date = (System.DateTime)rd["c_attenddate"];
                    list.Add(new {
                        c_attendid = rd.GetInt32(0),
                        c_attenddate = date.ToString("yyyy-MM-dd"),
                        clockin = (rd.IsDBNull(2) || rd.IsDBNull(3)) ? "" : $"{rd.GetInt32(2):00}:{rd.GetInt32(3):00}",
                        clockout = (rd.IsDBNull(4) || rd.IsDBNull(5)) ? "" : $"{rd.GetInt32(4):00}:{rd.GetInt32(5):00}",
                        c_worktype = rd.IsDBNull(6) ? "" : rd.GetString(6),
                        c_tasktype = rd.IsDBNull(7) ? "" : rd.GetString(7),
                        c_workinghour = rd.IsDBNull(8) ? 0 : rd.GetInt32(8),
                        c_attendstatus = rd.IsDBNull(9) ? "Regular" : rd.GetString(9)
                    });
                }
            }
            finally { await _con.CloseAsync(); }

            return Json(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateTasks(int attendId, string[] taskTypes)
        {
            var csv = (taskTypes == null || taskTypes.Length == 0) ? "" : string.Join(",", taskTypes);

            if (_con.State != System.Data.ConnectionState.Open)
            {
                await _con.OpenAsync();
            }
            try
            {
                using var cmd = new NpgsqlCommand(@"
                    UPDATE t_aattendance
                       SET c_tasktype = @t
                     WHERE c_attendid = @id;", _con);
                cmd.Parameters.AddWithValue("@t", csv);
                cmd.Parameters.AddWithValue("@id", attendId);
                var rows = await cmd.ExecuteNonQueryAsync();

                return Json(new { ok = rows > 0, tasks = csv, rowsAffected = rows });
            }
            catch (System.Exception ex)
            {
                return Json(new { ok = false, error = ex.Message });
            }
            finally { await _con.CloseAsync(); }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckIn(string workMode, string[] taskTypes, int empId)
        {
            var csv = (taskTypes == null || taskTypes.Length == 0) ? "" : string.Join(",", taskTypes);
            var (ok, code) = await _svc.ClockInAsync(empId, workMode, csv);

            string message = code switch
            {
                "ALREADY_CHECKED_IN" => "You have already checked in today.",
                "COMPLETED_TODAY" => "You already completed today's attendance. Please check in tomorrow.",
                "ERROR" => "Could not save check-in.",
                _ => "Checked in successfully."
            };
            var eid = empId.ToString();
            string mess = $"User {eid} checked in today.";
                    _mq.SendToUser(eid, "admin", mess);
                    _chat.SendToUser(eid, "admin", mess);
            return Json(new { ok, code, message });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckOut(int empId)
        {
            var (ok, workedHours, status, code) = await _svc.ClockOutAsync(empId);

            string message = code switch
            {
                "ALREADY_CHECKED_OUT" => "You have already checked out today.",
                "NO_TODAY_ROW" => "No check-in found for today.",
                "ERROR" => "Could not save check-out.",
                _ => "Checked out successfully."
            };
            
            var eid = empId.ToString();
            string mess = $"User {eid} checked Out today.";
                    _mq.SendToUser(eid, "admin", mess);
                    _chat.SendToUser(eid, "admin", mess);

            return Json(new { ok, workedHours, status, code, message });
        }
    }
}