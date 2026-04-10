using VaclavikBC.Models;

namespace VaclavikBC.Services.Interfaces
{
    public interface ISyncService
    {
        //Task SyncCalendarDataAsync(Calendar calendarData);
        Task SyncCalendarConnectionAsync(CalendarConnection calendarConnection);
    }
}
