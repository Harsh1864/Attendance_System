using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using project.Models;

namespace project.Services
{
    public class AuthServices
    {
        private readonly NpgsqlConnection _con;

        public AuthServices(NpgsqlConnection con)
        {
            _con = con;
        }

        public async Task<bool> Login(Vm_login login)
        {
            try
            {
                if (_con.State != System.Data.ConnectionState.Open)
                    await _con.OpenAsync();

                string s = "SELECT COUNT(*) FROM t_aemployee WHERE LOWER(c_email) = LOWER(@mail) AND c_password = @pass";
                using var cmd = new NpgsqlCommand(s, _con);
                cmd.Parameters.AddWithValue("@mail", login.EmpMail.Trim());
                cmd.Parameters.AddWithValue("@pass", login.EmpPassword);

                var counts = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return counts > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        public async Task<string> GetByEmail(string email)
        {
            try
            {
                if (_con.State != System.Data.ConnectionState.Open)
                    await _con.OpenAsync();

                string query = "SELECT c_role FROM t_aemployee WHERE LOWER(c_email) = LOWER(@Email) LIMIT 1";
                using var cmd = new NpgsqlCommand(query, _con);
                cmd.Parameters.AddWithValue("@Email", email.Trim());

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync() && !reader.IsDBNull(0))
                {
                    string role = reader.GetString(0);
                    return role;
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }
        public async Task<string> GetByEmailStatus(string email)
        {
            try
            {
                if (_con.State != System.Data.ConnectionState.Open)
                    await _con.OpenAsync();

                string query = "SELECT c_status FROM t_aemployee WHERE LOWER(c_email) = LOWER(@Email) LIMIT 1";
                using var cmd = new NpgsqlCommand(query, _con);
                cmd.Parameters.AddWithValue("@Email", email.Trim());

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync() && !reader.IsDBNull(0))
                {
                    string role = reader.GetString(0);
                    return role;
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }
        public async Task<int> GetByEmailId(string email)
        {
            try
            {
                if (_con.State != System.Data.ConnectionState.Open)
                    await _con.OpenAsync();

                string query = "SELECT c_empid FROM t_aemployee WHERE LOWER(c_email) = LOWER(@Email) LIMIT 1";
                using var cmd = new NpgsqlCommand(query, _con);
                cmd.Parameters.AddWithValue("@Email", email.Trim());

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync() && !reader.IsDBNull(0))
                {
                    int role = reader.GetInt32(0);
                    return role;
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return 0;
            }
        }
        public async Task<bool> RegisterAsync(EmployeeModel emp)
        {
            try
            {
                if (_con.State != System.Data.ConnectionState.Open)
                    await _con.OpenAsync();

                string query = @"
                    INSERT INTO t_aemployee 
                    (c_name, c_email, c_password, c_gender, c_profileimage)
                    VALUES (@name, @mail, @pass, @gender, @profile)
                ";

                using var cmd = new NpgsqlCommand(query, _con);
                cmd.Parameters.AddWithValue("@name", emp.EmpName);
                cmd.Parameters.AddWithValue("@mail", emp.EmpMail);
                cmd.Parameters.AddWithValue("@pass", emp.EmpPassword);
                cmd.Parameters.AddWithValue("@gender", emp.Gender);

                cmd.Parameters.AddWithValue("@profile", (object?)emp.Profile ?? DBNull.Value);


                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Register Error]: {ex.Message}");
                return false;
            }
            finally
            {
                await _con.CloseAsync();
            }
        }
        public async Task<bool> IsEmailExistsAsync(string email)
        {
            try
            {
                if (_con.State != System.Data.ConnectionState.Open)
                    await _con.OpenAsync();

                string query = "SELECT COUNT(*) FROM t_aemployee WHERE c_email = @mail";
                using var cmd = new NpgsqlCommand(query, _con);
                cmd.Parameters.AddWithValue("@mail", email);

                int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return count > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CheckEmail Error]: {ex.Message}");
                return false;
            }
            finally
            {
                await _con.CloseAsync();
            }
        }
    }
}