using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;

namespace project.Services
{
    public class DepartmentModel
    {
        public int DepartmentId { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public int MemberCount { get; set; }
        public int ActiveCount { get; set; }
    }

    public class DepartmentMemberModel
    {
        public int EmpId { get; set; }
        public string EmpName { get; set; } = "";
        public string EmpMail { get; set; } = "";
        public string Status { get; set; } = "";
        public int? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
    }

    public static class DepartmentRules
    {
        public const int MaxName = 50;
        public const int MaxDescription = 200;

        public static string? Validate(string? name, string? description)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Department name is required.";
            if (name.Trim().Length > MaxName) return $"Name cannot exceed {MaxName} characters.";
            if (description != null && description.Trim().Length > MaxDescription)
                return $"Description cannot exceed {MaxDescription} characters.";
            return null;
        }

        /// <summary>Removes duplicates and invalid ids from a posted list of employees.</summary>
        public static int[] CleanIds(IEnumerable<int>? ids) =>
            ids == null ? Array.Empty<int>() : ids.Where(i => i > 0).Distinct().ToArray();
    }

    public class DepartmentService
    {
        private readonly DbConnectionFactory _db;

        public DepartmentService(DbConnectionFactory db) => _db = db;

        public async Task<List<DepartmentModel>> ListAsync()
        {
            var list = new List<DepartmentModel>();
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT d.c_departmentid, d.c_name, d.c_description,
                       COUNT(e.c_empid) AS members,
                       COUNT(e.c_empid) FILTER (WHERE e.c_status = 'Active') AS active
                  FROM t_adepartment d
                  LEFT JOIN t_aemployee e ON e.c_departmentid = d.c_departmentid AND e.c_role != 'Admin'
                 GROUP BY d.c_departmentid, d.c_name, d.c_description
                 ORDER BY LOWER(d.c_name);", conn);

            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                list.Add(new DepartmentModel
                {
                    DepartmentId = rd.GetInt32(0),
                    Name = rd.GetString(1),
                    Description = rd.IsDBNull(2) ? null : rd.GetString(2),
                    MemberCount = Convert.ToInt32(rd.GetInt64(3)),
                    ActiveCount = Convert.ToInt32(rd.GetInt64(4)),
                });
            }
            return list;
        }

        /// <summary>All non-admin employees with their department (for the assignment screen).</summary>
        public async Task<List<DepartmentMemberModel>> EmployeesAsync()
        {
            var list = new List<DepartmentMemberModel>();
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT e.c_empid, e.c_name, e.c_email, e.c_status, e.c_departmentid, d.c_name
                  FROM t_aemployee e
                  LEFT JOIN t_adepartment d ON d.c_departmentid = e.c_departmentid
                 WHERE e.c_role != 'Admin' AND e.c_empid != 1
                 ORDER BY LOWER(e.c_name);", conn);

            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                list.Add(new DepartmentMemberModel
                {
                    EmpId = rd.GetInt32(0),
                    EmpName = rd.GetString(1),
                    EmpMail = rd.GetString(2),
                    Status = rd.IsDBNull(3) ? "" : rd.GetString(3),
                    DepartmentId = rd.IsDBNull(4) ? null : rd.GetInt32(4),
                    DepartmentName = rd.IsDBNull(5) ? null : rd.GetString(5),
                });
            }
            return list;
        }

        public async Task<(int id, string? error)> SaveAsync(int? departmentId, string name, string? description)
        {
            await using var conn = await _db.OpenAsync();

            await using (var dup = new NpgsqlCommand(@"
                SELECT 1 FROM t_adepartment
                 WHERE LOWER(c_name) = LOWER(@name) AND (@id IS NULL OR c_departmentid != @id);", conn))
            {
                dup.Parameters.AddWithValue("name", NpgsqlDbType.Varchar, name.Trim());
                dup.Parameters.AddWithValue("id", NpgsqlDbType.Integer, (object?)departmentId ?? DBNull.Value);
                if (await dup.ExecuteScalarAsync() != null)
                    return (0, $"A department called \"{name.Trim()}\" already exists.");
            }

            await using var cmd = departmentId.HasValue
                ? new NpgsqlCommand(@"
                    UPDATE t_adepartment SET c_name = @name, c_description = @desc
                     WHERE c_departmentid = @id RETURNING c_departmentid;", conn)
                : new NpgsqlCommand(@"
                    INSERT INTO t_adepartment (c_name, c_description) VALUES (@name, @desc)
                    RETURNING c_departmentid;", conn);
            cmd.Parameters.AddWithValue("name", NpgsqlDbType.Varchar, name.Trim());
            cmd.Parameters.AddWithValue("desc", NpgsqlDbType.Varchar,
                string.IsNullOrWhiteSpace(description) ? DBNull.Value : description.Trim());
            if (departmentId.HasValue) cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, departmentId.Value);

            var result = await cmd.ExecuteScalarAsync();
            return result is int id ? (id, null) : (0, "Department not found.");
        }

        /// <summary>Deletes a department; its members become unassigned. Returns the deleted name, or null.</summary>
        public async Task<string?> DeleteAsync(int departmentId)
        {
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "DELETE FROM t_adepartment WHERE c_departmentid = @id RETURNING c_name;", conn);
            cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, departmentId);
            return await cmd.ExecuteScalarAsync() as string;
        }

        /// <summary>Moves employees into a department, or out of any department when departmentId is null.</summary>
        public async Task<int> AssignAsync(int[] empIds, int? departmentId)
        {
            if (empIds.Length == 0) return 0;
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                UPDATE t_aemployee SET c_departmentid = @dept
                 WHERE c_empid = ANY(@ids) AND c_role != 'Admin'
                   AND (@dept IS NULL OR EXISTS (SELECT 1 FROM t_adepartment WHERE c_departmentid = @dept));", conn);
            cmd.Parameters.AddWithValue("dept", NpgsqlDbType.Integer, (object?)departmentId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("ids", NpgsqlDbType.Array | NpgsqlDbType.Integer, empIds);
            return await cmd.ExecuteNonQueryAsync();
        }
    }
}
