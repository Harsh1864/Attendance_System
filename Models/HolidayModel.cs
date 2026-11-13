using System;

namespace project.Models
{
    public class HolidayModel
    {
        public int HolidayId { get; set; }
        public DateTime Date { get; set; }
        public string Name { get; set; } = "";
        public string DayOfWeek => Date.DayOfWeek.ToString();
    }
}
