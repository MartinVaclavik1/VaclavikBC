using VaclavikBC.Models;

namespace VaclavikBC.Services.Interfaces
{
    public interface ISyncService
    {
        Task SyncCalendarConnectionAsync(CalendarConnection calendarConnection);
    }
}
