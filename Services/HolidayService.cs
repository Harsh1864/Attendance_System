using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using project.Models;

namespace project.Services
{
    public static class HolidayRules
    {
        public const int MaxNameLength = 100;

        /// <summary>Returns an error message, or null when the holiday is valid.</summary>
        public static string? Validate(DateTime? date, string? name, DateTime today)
        {
            if (!date.HasValue) return "Date is required.";
            if (string.IsNullOrWhiteSpace(name)) return "Name is required.";
            if (name.Trim().Length > MaxNameLength) return $"Name cannot exceed {MaxNameLength} characters.";
            if (date.Value.Date < today.Date.AddYears(-1) || date.Value.Date > today.Date.AddYears(2))
                return "Date must be within one year in the past and two years in the future.";
            return null;
        }
    }

    public class HolidayService
    {
        private readonly DbConnectionFactory _db;

        public HolidayService(DbConnectionFactory db) => _db = db;

        public async Task<List<HolidayModel>> GetInRangeAsync(DateTime from, DateTime to)
        {
            var list = new List<HolidayModel>();
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT c_holidayid, c_date, c_name
                  FROM t_aholiday
                 WHERE c_date BETWEEN @from AND @to
                 ORDER BY c_date;", conn);
            cmd.Parameters.AddWithValue("from", NpgsqlDbType.Date, from.Date);
            cmd.Parameters.AddWithValue("to", NpgsqlDbType.Date, to.Date);

            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                list.Add(new HolidayModel
                {
                    HolidayId = rd.GetInt32(0),
                    Date = rd.GetDateTime(1),
                    Name = rd.GetString(2),
                });
            }
            return list;
        }

        public Task<List<HolidayModel>> GetYearAsync(int year) =>
            GetInRangeAsync(new DateTime(year, 1, 1), new DateTime(year, 12, 31));

        public async Task<HashSet<DateTime>> GetDatesAsync(DateTime from, DateTime to)
        {
            var set = new HashSet<DateTime>();
            foreach (var h in await GetInRangeAsync(from, to)) set.Add(h.Date.Date);
            return set;
        }

        /// <summary>Adds a holiday. Returns (id, null) on success or (0, error).</summary>
        public async Task<(int id, string? error)> AddAsync(DateTime date, string name, int? createdBy)
        {
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO t_aholiday (c_date, c_name, c_createdby)
                VALUES (@date, @name, @by)
                ON CONFLICT (c_date) DO NOTHING
                RETURNING c_holidayid;", conn);
            cmd.Parameters.AddWithValue("date", NpgsqlDbType.Date, date.Date);
            cmd.Parameters.AddWithValue("name", NpgsqlDbType.Varchar, name.Trim());
            cmd.Parameters.AddWithValue("by", NpgsqlDbType.Integer, (object?)createdBy ?? DBNull.Value);

            var result = await cmd.ExecuteScalarAsync();
            return result is int id ? (id, null) : (0, $"A holiday already exists on {date:dd-MMM-yyyy}.");
        }

        /// <summary>Deletes a holiday and returns it, or null if it did not exist.</summary>
        public async Task<HolidayModel?> RemoveAsync(int holidayId)
        {
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                DELETE FROM t_aholiday WHERE c_holidayid = @id
                RETURNING c_holidayid, c_date, c_name;", conn);
            cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, holidayId);

            await using var rd = await cmd.ExecuteReaderAsync();
            if (!await rd.ReadAsync()) return null;
            return new HolidayModel { HolidayId = rd.GetInt32(0), Date = rd.GetDateTime(1), Name = rd.GetString(2) };
        }
    }
}
