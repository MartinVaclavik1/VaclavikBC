using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using VaclavikBC.Data;
using VaclavikBC.Hubs;
using VaclavikBC.Models;
using VaclavikBC.Services.Interfaces;

namespace VaclavikBC.Services
{
    public class SyncService : ISyncService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<CalendarSyncHub> _hubContext;
        public SyncService(IServiceScopeFactory scopeFactory, IHubContext<CalendarSyncHub> hubContext)
        {
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;

        }
        public async Task SyncCalendarConnectionAsync(CalendarConnection incomingConnection)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<VaclavikBCContext>();
            using var transaction = await dbContext.Database.BeginTransactionAsync();

            try
            {
                var existingConnection = await dbContext.CalendarConnection
                    .Include(c => c.Calendars)
                        .ThenInclude(cal => cal.Events)
                    .FirstOrDefaultAsync(c => c.Email == incomingConnection.Email &&
                                              c.Provider == incomingConnection.Provider &&
                                              c.UserId == incomingConnection.UserId);

                //nepotřebujeme dlouhý kód - pro aktuální kalendáře je potřeba smazat ukožené kalendáře/eventy
                //=> pokaždé smazat connection a nahrát ho => databáze sama načte odkazované objekty
                if (existingConnection != null)
                {
                    dbContext.CalendarConnection.Remove(existingConnection);
                    //existingConnection = null;
                }

                incomingConnection.Id = 0;
                foreach (var cal in incomingConnection.Calendars)
                {
                    cal.Id = 0;
                    foreach (var ev in cal.Events) ev.Id = 0;
                }
                dbContext.CalendarConnection.Add(incomingConnection);

                await dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                await _hubContext.Clients.All.SendAsync("ConnectionChanged", "Calendar data updated");
                await _hubContext.Clients.All.SendAsync("EventsChanged", "Calendar data updated");
            }
            catch { await transaction.RollbackAsync(); throw; }
        }
    }
}