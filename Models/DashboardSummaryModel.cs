using System;
using System.Collections.Generic;

namespace attendancesystem.Models
{
    public class DashboardSummaryModel
    {
        public int TotalEmployees { get; set; }
        public int ActiveEmployees { get; set; }
        public int InactiveEmployees { get; set; }
        public int LateOrEarly { get; set; }
        public int TotalWorkingHours { get; set; }
        public List<object> TaskTypeData { get; set; } = new List<object>();
        public List<object> WorkHoursPerEmployee { get; set; } = new List<object>();
        public DateTime? LastDate { get; set; }

        public int TotalAbsent { get; set; } = 0;
    }
}
