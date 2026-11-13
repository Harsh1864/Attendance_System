using System;

namespace project.Models
{
    public class AuditEntryModel
    {
        public int AuditId { get; set; }
        public int? ActorId { get; set; }
        public string? ActorEmail { get; set; }
        public string Action { get; set; } = "";
        public string Entity { get; set; } = "";
        public int? EntityId { get; set; }
        public string? Details { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>Who is performing an action, taken from the session.</summary>
    public record Actor(int? Id, string? Email, string? IpAddress)
    {
        public static readonly Actor System = new(null, "system", null);
    }
}
