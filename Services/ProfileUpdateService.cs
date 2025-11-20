using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using project.Models;

namespace try_project_casepoint.Services
{
    public class ProfileUpdateService
    {
        private readonly NpgsqlConnection _conn;
        public ProfileUpdateService(NpgsqlConnection conn)
        {
            _conn = conn;
        }

        public async Task<EmployeeModel> GetEmployeeById(int empId)
        {
            
            try
            {
               if (_conn.State != ConnectionState.Closed)
                    await _conn.CloseAsync();

                if (_conn.State != ConnectionState.Open)
                    await _conn.OpenAsync();

            const string query = "SELECT * FROM t_aemployee WHERE c_empid=@c_empid";
            await using var cmd = new NpgsqlCommand(query, _conn);
            cmd.Parameters.AddWithValue("c_empid", empId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new EmployeeModel
                {
                    EmpId = reader.GetInt32(reader.GetOrdinal("c_empid")),
                    EmpName = reader.GetString(reader.GetOrdinal("c_name")),
                    EmpMail = reader.GetString(reader.GetOrdinal("c_email")),
                    EmpPassword = reader.GetString(reader.GetOrdinal("c_password")),
                    Gender = reader.GetString(reader.GetOrdinal("c_gender")),
                    Status = reader.GetString(reader.GetOrdinal("c_status")),
                    Role = reader.GetString(reader.GetOrdinal("c_role"))
                };
            } 
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {

            }
            return null;
        }

        // Update Profile 

        public async Task UpdateProfile(EmployeeModel employee)
        {
            try
            {

                if (_conn.State != ConnectionState.Closed)
                    await _conn.CloseAsync();

                if (_conn.State != ConnectionState.Open)
                    await _conn.OpenAsync();

                const string query = @"UPDATE t_aemployee
                                SET c_name = @c_name, c_gender= @c_gender
                                WHERE c_empid = @c_empid";

                await using var cmd = new NpgsqlCommand(query, _conn);

                cmd.Parameters.AddWithValue("c_name", employee.EmpName);
                cmd.Parameters.AddWithValue("c_gender", employee.Gender);
                cmd.Parameters.AddWithValue("c_empid", employee.EmpId);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

        }

        // FOR PASSWORD UPDATE
        public async Task UpdatePassword(int empId, string newPassword)
        {
            
            try
            {

                if (_conn.State != ConnectionState.Closed)
                    await _conn.CloseAsync();

                if (_conn.State != ConnectionState.Open)
                    await _conn.OpenAsync();

                const string query = "UPDATE t_aemployee SET c_password= @c_password WHERE c_empid=@c_empid";
                await using var cmd = new NpgsqlCommand(query, _conn);
                cmd.Parameters.AddWithValue("c_password", newPassword);
                cmd.Parameters.AddWithValue("c_empid", empId);

                await cmd.ExecuteNonQueryAsync();

            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine(ex.Message);
            }
        }

    }
}