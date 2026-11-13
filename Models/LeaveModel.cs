using System;

namespace project.Models
{
    public class LeaveModel
    {
        public int LeaveId { get; set; }
        public int EmpId { get; set; }
        public string EmpName { get; set; } = "";
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string LeaveType { get; set; } = "";
        public bool HalfDay { get; set; }
        public decimal Days { get; set; }
        public string? Reason { get; set; }
        public string Status { get; set; } = "";
        public int? ReviewedBy { get; set; }
        public string? ReviewedByName { get; set; }
        public string? ReviewNote { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>Form posted by an employee to request leave.</summary>
    public class LeaveRequestInput
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? LeaveType { get; set; }
        public bool HalfDay { get; set; }
        public string? Reason { get; set; }
    }

    public class LeaveBalanceModel
    {
        public string LeaveType { get; set; } = "";
        /// <summary>Yearly allowance; null means unlimited (Unpaid).</summary>
        public decimal? Allowance { get; set; }
        public decimal Approved { get; set; }
        public decimal Pending { get; set; }
        public decimal? Remaining => Allowance.HasValue ? Allowance.Value - Approved - Pending : null;
    }
}
