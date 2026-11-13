using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using attendancesystem.Models;
using Npgsql;
using project.Models;

namespace try_project_casepoint.Services
{
    public class AdminService
    {
        private readonly NpgsqlConnection _conn;

        public AdminService(NpgsqlConnection conn)
        {
            _conn = conn;
        }

        public async Task<List<AttendanceModel>> GetAllAttendance()
        {
            List<AttendanceModel> AttendanceList = new List<AttendanceModel>();
            try
            {
                string query = @"
                    SELECT 
                        a.c_attendid,
                        a.c_empid,
                        e.c_name AS empname,
                        a.c_attenddate,
                        a.c_clockinhour,
                        a.c_clockinmin,
                        a.c_clockouthour,
                        a.c_clockoutmin,
                        a.c_worktype,
                        a.c_tasktype,
                        a.c_workinghour,
                        a.c_attendstatus
                    FROM t_aattendance a
                    INNER JOIN t_aemployee e ON a.c_empid = e.c_empid
                    ORDER BY a.c_attenddate DESC;
                ";

                using (var cmd = new NpgsqlCommand(query, _conn))
                {
                    if (_conn.State != System.Data.ConnectionState.Open)
                        await _conn.OpenAsync();

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            AttendanceList.Add(new AttendanceModel
                            {
                                AttendId = Convert.ToInt32(reader["c_attendid"]),
                                EmpId = Convert.ToInt32(reader["c_empid"]),
                                EmpName = reader["empname"].ToString(),
                                AttendDate = reader["c_attenddate"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["c_attenddate"]),
                                ClockInHour = reader["c_clockinhour"] == DBNull.Value ? 0 : Convert.ToInt32(reader["c_clockinhour"]),
                                ClockInMin = reader["c_clockinmin"] == DBNull.Value ? 0 : Convert.ToInt32(reader["c_clockinmin"]),
                                ClockOutHour = reader["c_clockouthour"] == DBNull.Value ? 0 : Convert.ToInt32(reader["c_clockouthour"]),
                                ClockOutMin = reader["c_clockoutmin"] == DBNull.Value ? 0 : Convert.ToInt32(reader["c_clockoutmin"]),
                                WorkType = reader["c_worktype"] == DBNull.Value ? "N/A" : reader["c_worktype"].ToString(),
                                TaskType = reader["c_tasktype"] == DBNull.Value ? "N/A" : reader["c_tasktype"].ToString(),
                                WorkingHour = reader["c_workinghour"] == DBNull.Value ? 0 : Convert.ToInt32(reader["c_workinghour"]),
                                AttendStatus = reader["c_attendstatus"] == DBNull.Value ? "N/A" : reader["c_attendstatus"].ToString()
                            });
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in GetAllAttendance: " + ex.Message);
            }
            finally
            {
                if (_conn.State == System.Data.ConnectionState.Open)
                    await _conn.CloseAsync();
            }

            return AttendanceList;
        }

        public async Task<DashboardSummaryModel> GetDashboardSummary(DateTime? date = null)
        {
            var summary = new DashboardSummaryModel();

            try
            {
                await _conn.OpenAsync();

                // 🔹 Get latest attendance date if not selected
                if (!date.HasValue)
                {
                    using (var cmd = new NpgsqlCommand("SELECT MAX(c_attenddate) FROM t_aattendance", _conn))
                    {
                        var latestDateObj = await cmd.ExecuteScalarAsync();
                        if (latestDateObj != DBNull.Value)
                            date = Convert.ToDateTime(latestDateObj);
                        else
                            date = DateTime.Now.Date; // default fallback
                    }
                }

                summary.LastDate = date;

                string excludeAdmin = "AND e.c_empid != 1";

                using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM t_aemployee WHERE c_empid != 1", _conn))
                    summary.TotalEmployees = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);

                using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM t_aemployee WHERE c_status='Active' AND c_empid != 1", _conn))
                    summary.ActiveEmployees = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);

                using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM t_aemployee WHERE c_status='Inactive' AND c_empid != 1", _conn))
                    summary.InactiveEmployees = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);

                string dateFilter = date.HasValue ? $"AND a.c_attenddate = '{date.Value:yyyy-MM-dd}'" : "";

                using (var cmd = new NpgsqlCommand(
                    $"SELECT COUNT(*) FROM t_aattendance a INNER JOIN t_aemployee e ON a.c_empid=e.c_empid WHERE a.c_attendstatus IN ('LateIn','EarlyOut', 'LateEarly') {dateFilter} {excludeAdmin}",
                    _conn))
                {
                    var result = await cmd.ExecuteScalarAsync();
                    summary.LateOrEarly = result == DBNull.Value ? 0 : Convert.ToInt32(result);
                }

                using (var cmd = new NpgsqlCommand(
                    $"SELECT COALESCE(SUM(c_workinghour),0) FROM t_aattendance a INNER JOIN t_aemployee e ON a.c_empid=e.c_empid WHERE 1=1 {dateFilter} {excludeAdmin}",
                    _conn))
                {
                    var result = await cmd.ExecuteScalarAsync();
                    summary.TotalWorkingHours = result == DBNull.Value ? 0 : Convert.ToInt32(result);
                }

                using (var cmd = new NpgsqlCommand(
                    $@"SELECT COUNT(*) 
       FROM t_aattendance a 
       INNER JOIN t_aemployee e ON a.c_empid = e.c_empid 
       WHERE a.c_attendstatus = 'Absent' {dateFilter} {excludeAdmin}",
                    _conn))
                {
                    var result = await cmd.ExecuteScalarAsync();
                    summary.TotalAbsent = result == DBNull.Value ? 0 : Convert.ToInt32(result);
                }


                using (var cmd = new NpgsqlCommand($@"
            SELECT c_tasktype, COUNT(*) AS count 
            FROM t_aattendance a 
            INNER JOIN t_aemployee e ON a.c_empid = e.c_empid
            WHERE 1=1 {dateFilter} {excludeAdmin}
            GROUP BY c_tasktype", _conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        summary.TaskTypeData.Add(new
                        {
                            category = reader["c_tasktype"]?.ToString() ?? "Unknown",
                            value = reader["count"] == DBNull.Value ? 0 : Convert.ToInt32(reader["count"])
                        });
                    }
                }

                using (var cmd = new NpgsqlCommand($@"
            SELECT e.c_name, COALESCE(SUM(a.c_workinghour),0) AS totalhours
            FROM t_aattendance a
            INNER JOIN t_aemployee e ON a.c_empid = e.c_empid
            WHERE 1=1 {dateFilter} {excludeAdmin}
            GROUP BY e.c_name
            ORDER BY e.c_name", _conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        summary.WorkHoursPerEmployee.Add(new
                        {
                            empName = reader["c_name"]?.ToString() ?? "Unknown",
                            totalHours = reader["totalhours"] == DBNull.Value ? 0 : Convert.ToInt32(reader["totalhours"])
                        });
                    }
                }

                if (summary.WorkHoursPerEmployee.Count == 0 && summary.TaskTypeData.Count == 0)
                {
                    summary.TaskTypeData.Add(new { category = "No Data Found", value = 0 });
                    summary.WorkHoursPerEmployee.Add(new { empName = "No Data Found", totalHours = 0 });
                }
            }
            finally
            {
                if (_conn.State == System.Data.ConnectionState.Open)
                    await _conn.CloseAsync();
            }

            return summary;
        }
        public async Task<List<EmployeeModel>> GetAllEmployeesDetails()
        {
            List<EmployeeModel> employeeList = new List<EmployeeModel>();
            try
            {
                string query = @"SELECT c_empid, c_name, c_email, c_gender, c_status, c_profileimage, c_role 
                                 FROM t_aemployee ORDER BY c_name ASC";
                using (var cmd = new NpgsqlCommand(query, _conn))
                {
                    if (_conn.State != System.Data.ConnectionState.Open)
                        await _conn.OpenAsync();

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            employeeList.Add(new EmployeeModel
                            {
                                EmpId = Convert.ToInt32(reader["c_empid"]),
                                EmpName = reader["c_name"].ToString(),
                                EmpMail = reader["c_email"].ToString(),
                                Gender = reader["c_gender"].ToString(),
                                Status = reader["c_status"].ToString(),
                                Profile = reader["c_profileimage"] == DBNull.Value ? null : reader["c_profileimage"].ToString(),
                                Role = reader["c_role"].ToString()
                            });
                        }
                    }
                }
            }
            finally
            {
                if (_conn.State == System.Data.ConnectionState.Open)
                    await _conn.CloseAsync();
            }

            return employeeList;
        }

        public async Task<EmployeeModel> GetEmployeeDetail(int empId)
        {
            EmployeeModel employee = null;
            try
            {
                string query = @"SELECT c_empid, c_name, c_email, c_gender, c_status, c_profileimage, c_role 
                                 FROM t_aemployee WHERE c_empid = @id AND c_role != 'Admin'";
                using (var cmd = new NpgsqlCommand(query, _conn))
                {
                    cmd.Parameters.AddWithValue("id", empId);

                    if (_conn.State != System.Data.ConnectionState.Open)
                        await _conn.OpenAsync();

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            employee = new EmployeeModel
                            {
                                EmpId = Convert.ToInt32(reader["c_empid"]),
                                EmpName = reader["c_name"].ToString(),
                                EmpMail = reader["c_email"].ToString(),
                                Gender = reader["c_gender"].ToString(),
                                Status = reader["c_status"].ToString(),
                                Profile = reader["c_profileimage"] == DBNull.Value ? null : reader["c_profileimage"].ToString(),
                                Role = reader["c_role"].ToString()
                            };
                        }
                    }
                }
            }
            finally
            {
                if (_conn.State == System.Data.ConnectionState.Open)
                    await _conn.CloseAsync();
            }

            return employee;
        }

        public async Task MarkAbsenteesAsync()
        {
            try
            {
                string query = @"
            INSERT INTO t_aattendance (c_empid, c_attenddate, c_worktype, c_tasktype, c_attendstatus)
            SELECT e.c_empid, CURRENT_DATE, NULL, NULL, 'Absent'
            FROM t_aemployee e
            WHERE e.c_status = 'Active'
              AND e.c_empid != 1         
              AND e.c_empid NOT IN (
                  SELECT a.c_empid 
                  FROM t_aattendance a 
                  WHERE a.c_attenddate = CURRENT_DATE
              );";

                using (var cmd = new NpgsqlCommand(query, _conn))
                {
                    if (_conn.State != System.Data.ConnectionState.Open)
                        await _conn.OpenAsync();

                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in MarkAbsenteesAsync: " + ex.Message);
            }
            finally
            {
                if (_conn.State == System.Data.ConnectionState.Open)
                    await _conn.CloseAsync();
            }
        }

        public async Task<List<EmployeeModel>> GetAbsentEmployeesByDate(DateTime date)
        {
            List<EmployeeModel> absentEmployees = new List<EmployeeModel>();
            try
            {
                string query = @"
            SELECT e.c_empid, e.c_name, e.c_email, e.c_gender, e.c_status, e.c_profileimage, e.c_role
            FROM t_aemployee e
            WHERE e.c_empid != 1
              AND e.c_status = 'Active'
              AND e.c_empid IN (
                  SELECT a.c_empid
                  FROM t_aattendance a
                  WHERE a.c_attenddate = @date
                    AND a.c_attendstatus = 'Absent'
              )
            ORDER BY e.c_name ASC;
        ";

                using (var cmd = new NpgsqlCommand(query, _conn))
                {
                    cmd.Parameters.AddWithValue("date", date.Date);

                    if (_conn.State != System.Data.ConnectionState.Open)
                        await _conn.OpenAsync();

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            absentEmployees.Add(new EmployeeModel
                            {
                                EmpId = Convert.ToInt32(reader["c_empid"]),
                                EmpName = reader["c_name"].ToString(),
                                EmpMail = reader["c_email"].ToString(),
                                Gender = reader["c_gender"].ToString(),
                                Status = reader["c_status"].ToString(),
                                Profile = reader["c_profileimage"] == DBNull.Value ? null : reader["c_profileimage"].ToString(),
                                Role = reader["c_role"].ToString()
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in GetAbsentEmployeesByDate: " + ex.Message);
            }
            finally
            {
                if (_conn.State == System.Data.ConnectionState.Open)
                    await _conn.CloseAsync();
            }

            return absentEmployees;
        }

        public async Task<List<EmployeeModel>> GetEmployeesByStatus(string status)
        {
            List<EmployeeModel> employeeList = new List<EmployeeModel>();
            try
            {
                string query = @"SELECT c_empid, c_name, c_email, c_gender, c_status, c_profileimage, c_role 
                                 FROM t_aemployee 
                                 WHERE c_role != 'Admin' AND c_status = @status ORDER BY c_name ASC";

                using (var cmd = new NpgsqlCommand(query, _conn))
                {
                    cmd.Parameters.AddWithValue("status", status);

                    if (_conn.State != System.Data.ConnectionState.Open)
                        await _conn.OpenAsync();

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            employeeList.Add(new EmployeeModel
                            {
                                EmpId = Convert.ToInt32(reader["c_empid"]),
                                EmpName = reader["c_name"].ToString(),
                                EmpMail = reader["c_email"].ToString(),
                                Gender = reader["c_gender"].ToString(),
                                Status = reader["c_status"].ToString(),
                                Profile = reader["c_profileimage"] == DBNull.Value ? null : reader["c_profileimage"].ToString(),
                                Role = reader["c_role"].ToString()
                            });
                        }
                    }
                }
            }
            finally
            {
                if (_conn.State == System.Data.ConnectionState.Open)
                    await _conn.CloseAsync();
            }

            return employeeList;
        }

        public async Task<bool> UpdateEmployeeStatus(int empId, string newStatus)
        {
            bool updated = false;
            try
            {
                string query = @"UPDATE t_aemployee 
                                 SET c_status = @status 
                                 WHERE c_empid = @id AND c_role != 'Admin'";
                using (var cmd = new NpgsqlCommand(query, _conn))
                {
                    cmd.Parameters.AddWithValue("status", newStatus);
                    cmd.Parameters.AddWithValue("id", empId);

                    if (_conn.State != System.Data.ConnectionState.Open)
                        await _conn.OpenAsync();

                    updated = (await cmd.ExecuteNonQueryAsync()) > 0;
                }
            }
            finally
            {
                if (_conn.State == System.Data.ConnectionState.Open)
                    await _conn.CloseAsync();
            }

            return updated;
        }

        public async Task<bool> UpdateEmployeeDetails(int empId, string empName, string gender, string status, string? profileFileName)
        {
            bool updated = false;
            try
            {
                string query;

                if (!string.IsNullOrEmpty(profileFileName))
                {
                    query = @"UPDATE t_aemployee 
                              SET c_name = @name, 
                                  c_gender = @gender, 
                                  c_status = @status, 
                                  c_profileimage = @profile 
                              WHERE c_empid = @id AND c_role != 'Admin'";
                }
                else
                {
                    query = @"UPDATE t_aemployee 
                              SET c_name = @name, 
                                  c_gender = @gender, 
                                  c_status = @status 
                              WHERE c_empid = @id AND c_role != 'Admin'";
                }

                using (var cmd = new NpgsqlCommand(query, _conn))
                {
                    cmd.Parameters.AddWithValue("name", empName);
                    cmd.Parameters.AddWithValue("gender", gender);
                    cmd.Parameters.AddWithValue("status", status);
                    cmd.Parameters.AddWithValue("id", empId);

                    if (!string.IsNullOrEmpty(profileFileName))
                        cmd.Parameters.AddWithValue("profile", profileFileName);

                    if (_conn.State != System.Data.ConnectionState.Open)
                        await _conn.OpenAsync();

                    updated = (await cmd.ExecuteNonQueryAsync()) > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in UpdateEmployeeDetails: " + ex.Message);
            }
            finally
            {
                if (_conn.State == System.Data.ConnectionState.Open)
                    await _conn.CloseAsync();
            }

            return updated;
        }
    }
}