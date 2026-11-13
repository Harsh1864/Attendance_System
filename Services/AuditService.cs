using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using project.Models;

namespace project.Services
{
    /// <summary>Well-known audit action names.</summary>
    public static class AuditActions
    {
        public const string LoginSucceeded = "LoginSucceeded";
        public const string LoginFailed = "LoginFailed";
        public const string Registered = "Registered";
        public const string PasswordChanged = "PasswordChanged";
        public const string ProfileUpdated = "ProfileUpdated";
        public const string EmployeeUpdated = "EmployeeUpdated";
        public const string EmployeeStatusChanged = "EmployeeStatusChanged";
        public const string AbsenteeJobRun = "AbsenteeJobRun";
        public const string AttendanceCreated = "AttendanceCreated";
        public const string AttendanceEdited = "AttendanceEdited";
        public const string AttendanceDeleted = "AttendanceDeleted";
        public const string HolidayAdded = "HolidayAdded";
        public const string HolidayRemoved = "HolidayRemoved";
        public const string LeaveRequested = "LeaveRequested";
        public const string LeaveCancelled = "LeaveCancelled";
        public const string LeaveApproved = "LeaveApproved";
        public const string LeaveRejected = "LeaveRejected";
        public const string CorrectionRequested = "CorrectionRequested";
        public const string CorrectionCancelled = "CorrectionCancelled";
        public const string CorrectionApproved = "CorrectionApproved";
        public const string CorrectionRejected = "CorrectionRejected";
    }

    public class AuditService
    {
        private const int MaxDetailsLength = 2000;

        private readonly DbConnectionFactory _db;
        private readonly ILogger<AuditService> _logger;

        public AuditService(DbConnectionFactory db, ILogger<AuditService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>Builds an Actor from the current session.</summary>
        public static Actor ActorFrom(HttpContext context)
        {
            int? id = int.TryParse(context.Session.GetString("Ids"), out var v) && v > 0 ? v : null;
            return new Actor(id, context.Session.GetString("Mail"), context.Connection.RemoteIpAddress?.ToString());
        }

        /// <summary>
        /// Writes an audit entry. Never throws: a failure to audit is logged
        /// but must not break the action being audited.
        /// </summary>
        public async Task LogAsync(Actor actor, string action, string entity, int? entityId = null, string? details = null)
        {
            try
            {
                await using var conn = await _db.OpenAsync();
                await using var cmd = new NpgsqlCommand(@"
                    INSERT INTO t_aaudit (c_actorid, c_actoremail, c_action, c_entity, c_entityid, c_details, c_ipaddress)
                    VALUES (@actor, @email, @action, @entity, @entityid, @details, @ip);", conn);
                cmd.Parameters.AddWithValue("actor", NpgsqlDbType.Integer, (object?)actor.Id ?? DBNull.Value);
                cmd.Parameters.AddWithValue("email", NpgsqlDbType.Varchar, (object?)Truncate(actor.Email, 100) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("action", NpgsqlDbType.Varchar, Truncate(action, 50)!);
                cmd.Parameters.AddWithValue("entity", NpgsqlDbType.Varchar, Truncate(entity, 30)!);
                cmd.Parameters.AddWithValue("entityid", NpgsqlDbType.Integer, (object?)entityId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("details", NpgsqlDbType.Text, (object?)Truncate(details, MaxDetailsLength) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("ip", NpgsqlDbType.Varchar, (object?)Truncate(actor.IpAddress, 45) ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not write audit entry {Action} {Entity} {EntityId}", action, entity, entityId);
            }
        }

        public async Task<List<AuditEntryModel>> SearchAsync(DateTime from, DateTime to, string? action, string? search, int limit = 500)
        {
            var list = new List<AuditEntryModel>();
            limit = Math.Clamp(limit, 1, 5000);

            await using var conn = await _db.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT c_auditid, c_actorid, c_actoremail, c_action, c_entity, c_entityid,
                       c_details, c_ipaddress, c_createdat
                  FROM t_aaudit
                 WHERE c_createdat >= @from AND c_createdat < @to
                   AND (@action IS NULL OR c_action = @action)
                   AND (@search IS NULL
                        OR c_actoremail ILIKE '%' || @search || '%'
                        OR c_details    ILIKE '%' || @search || '%')
                 ORDER BY c_createdat DESC
                 LIMIT @limit;", conn);
            cmd.Parameters.AddWithValue("from", NpgsqlDbType.Timestamp, from.Date);
            cmd.Parameters.AddWithValue("to", NpgsqlDbType.Timestamp, to.Date.AddDays(1));
            cmd.Parameters.AddWithValue("action", NpgsqlDbType.Varchar, string.IsNullOrWhiteSpace(action) ? DBNull.Value : action.Trim());
            cmd.Parameters.AddWithValue("search", NpgsqlDbType.Varchar, string.IsNullOrWhiteSpace(search) ? DBNull.Value : EscapeLike(search.Trim()));
            cmd.Parameters.AddWithValue("limit", NpgsqlDbType.Integer, limit);

            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                list.Add(new AuditEntryModel
                {
                    AuditId = rd.GetInt32(0),
                    ActorId = rd.IsDBNull(1) ? null : rd.GetInt32(1),
                    ActorEmail = rd.IsDBNull(2) ? null : rd.GetString(2),
                    Action = rd.GetString(3),
                    Entity = rd.GetString(4),
                    EntityId = rd.IsDBNull(5) ? null : rd.GetInt32(5),
                    Details = rd.IsDBNull(6) ? null : rd.GetString(6),
                    IpAddress = rd.IsDBNull(7) ? null : rd.GetString(7),
                    CreatedAt = rd.GetDateTime(8),
                });
            }
            return list;
        }

        internal static string? Truncate(string? value, int max) =>
            value is null ? null : value.Length <= max ? value : value[..max];

        /// <summary>Escapes % _ and \ so user input is matched literally by ILIKE.</summary>
        internal static string EscapeLike(string value) =>
            value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    }
}
