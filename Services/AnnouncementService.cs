using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;

namespace project.Services
{
    public class AnnouncementModel
    {
        public int AnnouncementId { get; set; }
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public string Priority { get; set; } = AnnouncementRules.Normal;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        /// <summary>Upcoming, Active or Expired, relative to today.</summary>
        public string State { get; set; } = "";
    }

    public class AnnouncementInput
    {
        public int? AnnouncementId { get; set; }
        public string? Title { get; set; }
        public string? Body { get; set; }
        public string? Priority { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public static class AnnouncementRules
    {
        public const string Normal = "Normal";
        public const string Important = "Important";
        public static readonly string[] Priorities = { Normal, Important };

        public const int MaxTitle = 120;
        public const int MaxBody = 2000;

        public const string Upcoming = "Upcoming";
        public const string Active = "Active";
        public const string Expired = "Expired";

        public static string? Validate(AnnouncementInput input, DateTime today)
        {
            if (string.IsNullOrWhiteSpace(input.Title)) return "Title is required.";
            if (input.Title.Trim().Length > MaxTitle) return $"Title cannot exceed {MaxTitle} characters.";
            if (string.IsNullOrWhiteSpace(input.Body)) return "Message is required.";
            if (input.Body.Trim().Length > MaxBody) return $"Message cannot exceed {MaxBody} characters.";
            if (string.IsNullOrWhiteSpace(input.Priority) || !Priorities.Contains(input.Priority))
                return "Priority must be Normal or Important.";
            if (!input.StartDate.HasValue) return "Start date is required.";
            if (input.EndDate.HasValue && input.EndDate.Value.Date < input.StartDate.Value.Date)
                return "End date must be on or after the start date.";
            if (input.EndDate.HasValue && input.EndDate.Value.Date < today.Date && !input.AnnouncementId.HasValue)
                return "A new announcement cannot end in the past.";
            return null;
        }

        public static string StateOn(DateTime start, DateTime? end, DateTime today)
        {
            if (start.Date > today.Date) return Upcoming;
            if (end.HasValue && end.Value.Date < today.Date) return Expired;
            return Active;
        }

        /// <summary>Important first, then newest start date, then newest update.</summary>
        public static List<AnnouncementModel> OrderForDisplay(IEnumerable<AnnouncementModel> items) =>
            items.OrderBy(a => a.Priority == Important ? 0 : 1)
                 .ThenByDescending(a => a.StartDate)
                 .ThenByDescending(a => a.UpdatedAt)
                 .ToList();
    }

    public class AnnouncementService
    {
        private const string SelectSql = @"
            SELECT a.c_announcementid, a.c_title, a.c_body, a.c_priority, a.c_startdate, a.c_enddate,
                   e.c_name, a.c_createdat, a.c_updatedat
              FROM t_aannouncement a
              LEFT JOIN t_aemployee e ON e.c_empid = a.c_createdby";

        private readonly DbConnectionFactory _db;

        public AnnouncementService(DbConnectionFactory db) => _db = db;

        public async Task<List<AnnouncementModel>> ListAllAsync(DateTime today)
        {
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(SelectSql + " ORDER BY a.c_startdate DESC, a.c_announcementid DESC LIMIT 500;", conn);
            return await ReadAsync(cmd, today);
        }

        public async Task<List<AnnouncementModel>> ActiveAsync(DateTime today)
        {
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(SelectSql + @"
                 WHERE a.c_startdate <= @today AND (a.c_enddate IS NULL OR a.c_enddate >= @today);", conn);
            cmd.Parameters.AddWithValue("today", NpgsqlDbType.Date, today.Date);
            return AnnouncementRules.OrderForDisplay(await ReadAsync(cmd, today));
        }

        public async Task<(int id, string? error)> SaveAsync(AnnouncementInput input, int? createdBy)
        {
            await using var conn = await _db.OpenAsync();
            await using var cmd = input.AnnouncementId.HasValue
                ? new NpgsqlCommand(@"
                    UPDATE t_aannouncement
                       SET c_title = @title, c_body = @body, c_priority = @priority,
                           c_startdate = @start, c_enddate = @end, c_updatedat = NOW()
                     WHERE c_announcementid = @id
                    RETURNING c_announcementid;", conn)
                : new NpgsqlCommand(@"
                    INSERT INTO t_aannouncement (c_title, c_body, c_priority, c_startdate, c_enddate, c_createdby)
                    VALUES (@title, @body, @priority, @start, @end, @by)
                    RETURNING c_announcementid;", conn);

            cmd.Parameters.AddWithValue("title", NpgsqlDbType.Varchar, input.Title!.Trim());
            cmd.Parameters.AddWithValue("body", NpgsqlDbType.Varchar, input.Body!.Trim());
            cmd.Parameters.AddWithValue("priority", NpgsqlDbType.Varchar, input.Priority!);
            cmd.Parameters.AddWithValue("start", NpgsqlDbType.Date, input.StartDate!.Value.Date);
            cmd.Parameters.AddWithValue("end", NpgsqlDbType.Date, input.EndDate.HasValue ? input.EndDate.Value.Date : DBNull.Value);
            if (input.AnnouncementId.HasValue)
                cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, input.AnnouncementId.Value);
            else
                cmd.Parameters.AddWithValue("by", NpgsqlDbType.Integer, (object?)createdBy ?? DBNull.Value);

            var result = await cmd.ExecuteScalarAsync();
            return result is int id ? (id, null) : (0, "Announcement not found.");
        }

        public async Task<string?> DeleteAsync(int id)
        {
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "DELETE FROM t_aannouncement WHERE c_announcementid = @id RETURNING c_title;", conn);
            cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, id);
            return await cmd.ExecuteScalarAsync() as string;
        }

        private static async Task<List<AnnouncementModel>> ReadAsync(NpgsqlCommand cmd, DateTime today)
        {
            var list = new List<AnnouncementModel>();
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                var start = rd.GetDateTime(4);
                DateTime? end = rd.IsDBNull(5) ? null : rd.GetDateTime(5);
                list.Add(new AnnouncementModel
                {
                    AnnouncementId = rd.GetInt32(0),
                    Title = rd.GetString(1),
                    Body = rd.GetString(2),
                    Priority = rd.GetString(3),
                    StartDate = start,
                    EndDate = end,
                    CreatedByName = rd.IsDBNull(6) ? null : rd.GetString(6),
                    CreatedAt = rd.GetDateTime(7),
                    UpdatedAt = rd.GetDateTime(8),
                    State = AnnouncementRules.StateOn(start, end, today),
                });
            }
            return list;
        }
    }
}
