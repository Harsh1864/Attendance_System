using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using project.Models;

namespace project.Services
{
    public class NotificationModel
    {
        public int NotificationId { get; set; }
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public string? Link { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>A notification to send: title, message and optional link inside the app.</summary>
    public record NotificationText(string Title, string Message, string? Link = null);

    /// <summary>Wording for every notification the app sends, kept in one place and unit tested.</summary>
    public static class NotificationMessages
    {
        public static NotificationText LeaveDecision(LeaveModel leave, bool approved, string? note)
        {
            var range = leave.FromDate.Date == leave.ToDate.Date
                ? $"{leave.FromDate:dd MMM yyyy}"
                : $"{leave.FromDate:dd MMM} to {leave.ToDate:dd MMM yyyy}";
            var msg = $"Your {leave.LeaveType} leave for {range} was {(approved ? "approved" : "rejected")}.";
            if (!string.IsNullOrWhiteSpace(note)) msg += $" Note: {note.Trim()}";
            return new NotificationText(approved ? "Leave approved" : "Leave rejected", msg, "/Leave/Index");
        }

        public static NotificationText LeaveRequested(string employeeName, string summary) =>
            new("New leave request", $"{employeeName} requested {summary}.", "/Admin/Leave");

        public static NotificationText AttendanceChanged(string change, string reason) =>
            new("Attendance record updated", $"An admin updated your attendance: {change}. Reason: {reason.Trim()}", "/MyReport/Index");

        public static NotificationText CorrectionRequested(string employeeName, DateTime date) =>
            new("New correction request", $"{employeeName} asked to correct attendance for {date:dd MMM yyyy}.", "/Admin/Corrections");

        public static NotificationText CorrectionDecision(DateTime date, bool approved, string? note)
        {
            var msg = $"Your attendance correction for {date:dd MMM yyyy} was {(approved ? "approved" : "rejected")}.";
            if (!string.IsNullOrWhiteSpace(note)) msg += $" Note: {note.Trim()}";
            return new NotificationText(approved ? "Correction approved" : "Correction rejected", msg, "/Corrections/Index");
        }

        public static NotificationText Announcement(string title) =>
            new("Important announcement", title, null);
    }

    public class NotificationService
    {
        public const int MaxTitle = 100;
        public const int MaxMessage = 500;

        private readonly DbConnectionFactory _db;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(DbConnectionFactory db, ILogger<NotificationService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>Sends a notification. Never throws: a failed notification must not undo the action.</summary>
        public async Task SendAsync(int empId, NotificationText text)
        {
            try
            {
                await using var conn = await _db.OpenAsync();
                await using var cmd = new NpgsqlCommand(@"
                    INSERT INTO t_anotification (c_empid, c_title, c_message, c_link)
                    VALUES (@emp, @title, @msg, @link);", conn);
                AddText(cmd, text);
                cmd.Parameters.AddWithValue("emp", NpgsqlDbType.Integer, empId);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not send notification to {EmpId}", empId);
            }
        }

        /// <summary>Sends to every admin account.</summary>
        public Task SendToAdminsAsync(NotificationText text) =>
            SendToManyAsync("c_role = 'Admin'", text);

        /// <summary>Sends to every active non-admin employee.</summary>
        public Task SendToActiveEmployeesAsync(NotificationText text) =>
            SendToManyAsync("c_role != 'Admin' AND c_status = 'Active'", text);

        private async Task SendToManyAsync(string where, NotificationText text)
        {
            try
            {
                await using var conn = await _db.OpenAsync();
                await using var cmd = new NpgsqlCommand($@"
                    INSERT INTO t_anotification (c_empid, c_title, c_message, c_link)
                    SELECT c_empid, @title, @msg, @link FROM t_aemployee WHERE {where};", conn);
                AddText(cmd, text);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not send notification \"{Title}\"", text.Title);
            }
        }

        public async Task<List<NotificationModel>> ListAsync(int empId, int limit = 50)
        {
            var list = new List<NotificationModel>();
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT c_notificationid, c_title, c_message, c_link, c_isread, c_createdat
                  FROM t_anotification
                 WHERE c_empid = @emp
                 ORDER BY c_createdat DESC
                 LIMIT @limit;", conn);
            cmd.Parameters.AddWithValue("emp", NpgsqlDbType.Integer, empId);
            cmd.Parameters.AddWithValue("limit", NpgsqlDbType.Integer, Math.Clamp(limit, 1, 200));

            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                list.Add(new NotificationModel
                {
                    NotificationId = rd.GetInt32(0),
                    Title = rd.GetString(1),
                    Message = rd.GetString(2),
                    Link = rd.IsDBNull(3) ? null : rd.GetString(3),
                    IsRead = rd.GetBoolean(4),
                    CreatedAt = rd.GetDateTime(5),
                });
            }
            return list;
        }

        public async Task<int> UnreadCountAsync(int empId)
        {
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM t_anotification WHERE c_empid = @emp AND NOT c_isread;", conn);
            cmd.Parameters.AddWithValue("emp", NpgsqlDbType.Integer, empId);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        /// <summary>Marks one (id given) or all of the employee's notifications as read.</summary>
        public async Task<int> MarkReadAsync(int empId, int? notificationId)
        {
            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                UPDATE t_anotification SET c_isread = TRUE
                 WHERE c_empid = @emp AND NOT c_isread
                   AND (@id IS NULL OR c_notificationid = @id);", conn);
            cmd.Parameters.AddWithValue("emp", NpgsqlDbType.Integer, empId);
            cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, (object?)notificationId ?? DBNull.Value);
            return await cmd.ExecuteNonQueryAsync();
        }

        private static void AddText(NpgsqlCommand cmd, NotificationText text)
        {
            cmd.Parameters.AddWithValue("title", NpgsqlDbType.Varchar, AuditService.Truncate(text.Title, MaxTitle)!);
            cmd.Parameters.AddWithValue("msg", NpgsqlDbType.Varchar, AuditService.Truncate(text.Message, MaxMessage)!);
            cmd.Parameters.AddWithValue("link", NpgsqlDbType.Varchar, (object?)text.Link ?? DBNull.Value);
        }
    }
}
