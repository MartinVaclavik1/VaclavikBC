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
        private readonly ILogger<SyncService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<CalendarSyncHub> _hubContext;
        public SyncService(IServiceScopeFactory scopeFactory,
            ILogger<SyncService> logger, IHubContext<CalendarSyncHub> hubContext)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
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

                //if (existingConnection == null)
                //{
                //    incomingConnection.Id = 0;
                //    foreach (var cal in incomingConnection.Calendars)
                //    {
                //        cal.Id = 0;
                //        foreach (var ev in cal.Events) ev.Id = 0;
                //    }
                //    dbContext.CalendarConnection.Add(incomingConnection);
                //}
                //else
                //{
                //    incomingConnection.Id = existingConnection.Id;
                //    dbContext.Entry(existingConnection).CurrentValues.SetValues(incomingConnection);
                //    UpdateCalendarCollection(existingConnection.Calendars, incomingConnection.Calendars, dbContext, existingConnection.Id);
                //}

                await dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                await _hubContext.Clients.All.SendAsync("ConnectionChanged", "Calendar data updated");
            }
            catch { await transaction.RollbackAsync(); throw; }
        }

        private void UpdateCalendarCollection(ICollection<Calendar> existingCalendars,
                                      ICollection<Calendar> incomingCalendars,
                                      VaclavikBCContext dbContext,
                                      int connectionId)
        {
            foreach (var incomingCal in incomingCalendars)
            {
                var existingCal = existingCalendars.FirstOrDefault(c => c.IDProvider == incomingCal.IDProvider);
                if (existingCal == null)
                {
                    incomingCal.Id = 0;
                    incomingCal.CalendarConnectionId = connectionId;
                    foreach (var ev in incomingCal.Events) ev.Id = 0;
                    existingCalendars.Add(incomingCal);
                }
                else
                {
                    incomingCal.Id = existingCal.Id;
                    incomingCal.CalendarConnectionId = existingCal.CalendarConnectionId;
                    dbContext.Entry(existingCal).CurrentValues.SetValues(incomingCal);
                    UpdateEventCollection(existingCal.Events, incomingCal.Events, dbContext, existingCal.Id);
                }
            }
        }

        private void UpdateEventCollection(ICollection<CalendarEvent> existingEvents,
                                      ICollection<CalendarEvent> incomingEvents,
                                      VaclavikBCContext dbContext,
                                      int calendarId)
        {
            foreach (var incomingEv in incomingEvents)
            {
                var existingEv = existingEvents.FirstOrDefault(c => c.ProviderId == incomingEv.ProviderId);
                if (existingEv == null)
                {
                    incomingEv.Id = 0;
                    incomingEv.CalendarId = calendarId;
                    existingEvents.Add(incomingEv);
                }
                else
                {
                    incomingEv.Id = existingEv.Id;
                    incomingEv.CalendarId = existingEv.CalendarId;
                    dbContext.Entry(existingEv).CurrentValues.SetValues(incomingEv);
                }
            }
        }
    }
}