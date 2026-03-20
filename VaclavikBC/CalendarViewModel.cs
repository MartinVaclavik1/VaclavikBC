using VaclavikBC.Models;

namespace VaclavikBC
{
    public class CalendarViewModel
    {
        public List<CalendarEvent> Events { get; set; } = new();
        public DateTime CurrentDate { get; set; } = DateTime.Today;
    }
}
