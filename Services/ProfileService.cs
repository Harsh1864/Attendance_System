using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using project.Models;

namespace try_project_casepoint.Services
{
    public class ProfileService
    {
        public readonly NpgsqlConnection _connection;

        public ProfileService(NpgsqlConnection connection)
        {
            _connection = connection;
        }
        public async Task<EmployeeModel> GetProfileByID(int id)
        {
            var result = new EmployeeModel();
            try
            {
                await _connection.CloseAsync();
                var sql = "SELECT * FROM  t_aemployee WHERE  c_empid= @id";
                using var cmd = new NpgsqlCommand(sql, _connection);
                cmd.Parameters.AddWithValue("@id", id);
                await _connection.OpenAsync();
                var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    result.EmpId = (int)reader["c_empid"];
                    result.EmpName = (string)reader["c_name"];
                    result.EmpMail = (string)reader["c_email"];
                    result.EmpPassword = (string)reader["c_password"];
                    result.Gender = (string)reader["c_gender"];
                    result.Status = (string)reader["c_status"];
                    result.Profile = reader["c_profileimage"] == DBNull.Value
                        ? " "
                        : reader["c_profileimage"].ToString();
                    result.Role = (string)reader["c_role"];

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                await _connection.CloseAsync();
            }

            return result;
        }
    
    public async Task<bool> UpdateProfileImage(string email, string profileFileName)
{
    try
    {
        await _connection.CloseAsync();
        await _connection.OpenAsync();

        var sql = "UPDATE t_aemployee SET c_profileimage = @profile WHERE c_email = @mail";
        using var cmd = new NpgsqlCommand(sql, _connection);
        cmd.Parameters.AddWithValue("@profile", profileFileName);
        cmd.Parameters.AddWithValue("@mail", email);

        int rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }
    catch (Exception ex)
    {
        return false;
    }
    finally
    {
        await _connection.CloseAsync();
    }
}


    }
}