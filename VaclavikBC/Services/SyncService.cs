using Microsoft.EntityFrameworkCore;
using VaclavikBC.Data;
using VaclavikBC.Models;
using VaclavikBC.Services.Interfaces;

namespace VaclavikBC.Services
{
    public class SyncService : ISyncService
    {
        private readonly ILogger<SyncService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        public SyncService(IServiceScopeFactory scopeFactory,
            ILogger<SyncService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;

        }
        //public async Task SyncCalendarConnectionAsync(CalendarConnection calendarConnection)
        //{
        //    _logger.LogInformation("Starting calendarConnection sync...");
        //    using var scope = _scopeFactory.CreateScope();
        //    var dbContext = scope.ServiceProvider.GetRequiredService<VaclavikBCContext>();
        //    using var transaction = await dbContext.Database.BeginTransactionAsync();

        //    try
        //    {
        //        var existingCalendarConn = await dbContext.CalendarConnection
        //            .FirstOrDefaultAsync(c => c.Email == calendarConnection.Email &&
        //                                    c.Provider == c.Provider);
        //        if (existingCalendarConn == null)
        //        {
        //            calendarConnection.Id = 0; // mělo by být 0, ale pro jistotu => aby to db brala jako novou entitu
        //            await dbContext.CalendarConnection.AddAsync(calendarConnection);
        //        }
        //        else
        //        {
        //            calendarConnection.Id = existingCalendarConn.Id;  //jinak v setValues dostaneme chybu, že nejde přepsat id
        //            dbContext.Entry(existingCalendarConn).CurrentValues.SetValues(calendarConnection);
        //        }

        //        await dbContext.SaveChangesAsync();

        //        _logger.LogInformation("Calendar sync completed.");

        //        //var calendarConn = await dbContext.CalendarConnection
        //        //    .FirstOrDefaultAsync(c => c.Email == calendarConnection.Email &&
        //        //                            c.Provider == c.Provider);

        //        foreach (var calendarData in calendarConnection.Calendars)
        //        {
        //            calendarData.CalendarConnectionId = calendarConnection.Id;
        //            calendarData.CalendarConnection = null;

        //            var existingCalendar = await dbContext.Calendar
        //            .FirstOrDefaultAsync(c => c.IDProvider == calendarData.IDProvider);

        //            if (existingCalendar == null)
        //            {
        //                calendarData.Id = 0;        // mělo by být 0, ale pro jistotu => aby to db brala jako novou entitu
        //                await dbContext.Calendar.AddAsync(calendarData);
        //            }
        //            else
        //            {
        //                calendarData.Id = existingCalendar.Id;  //jinak v setValues dostaneme chybu, že nejde přepsat id
        //                dbContext.Entry(existingCalendar).CurrentValues.SetValues(calendarData);
        //            }

        //            // Save changes to get a CalendarId for new calendars
        //            await dbContext.SaveChangesAsync();

        //            await dbContext.Entry(calendarData).ReloadAsync();
        //            //// --- Process the Events ---
        //            //// Get the Calendar entity after potential save
        //            //var calendar = await dbContext.Calendar
        //            //    .FirstOrDefaultAsync(c => c.IDProvider == calendarData.IDProvider);

        //            var eventsSnapshot = calendarData.Events.ToList();
        //            foreach (var remoteEvent in eventsSnapshot)
        //            {
        //                remoteEvent.Calendar = null;
        //                _logger.LogInformation("Using CalendarId: {CalendarId}", calendarData.Id);
        //                remoteEvent.CalendarId = existingCalendar.Id;

        //                var existingEvent = await dbContext.CalendarEvent
        //                    .FirstOrDefaultAsync(e => e.ProviderId == remoteEvent.ProviderId &&
        //                                            e.CalendarId == remoteEvent.CalendarId);

        //                if (existingEvent == null)
        //                {
        //                    remoteEvent.Id = 0;
        //                    await dbContext.CalendarEvent.AddAsync(remoteEvent);
        //                }
        //                else
        //                {
        //                    // Existing event: update its fields
        //                    remoteEvent.Id = existingEvent.Id;
        //                    dbContext.Entry(existingEvent).CurrentValues.SetValues(remoteEvent);
        //                }

        //                await dbContext.SaveChangesAsync();
        //            }

        //        }


        //        await transaction.CommitAsync();
        //    }
        //    catch (Exception)
        //    {
        //        await transaction.RollbackAsync();
        //        throw;
        //    }


        //}

        public async Task SyncCalendarConnectionAsync(CalendarConnection incomingConnection)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<VaclavikBCContext>();
            using var transaction = await dbContext.Database.BeginTransactionAsync();

            try
            {
                // Load existing connection with its calendars and events
                var existingConnection = await dbContext.CalendarConnection
                    .Include(c => c.Calendars)
                        .ThenInclude(cal => cal.Events)
                    .FirstOrDefaultAsync(c => c.Email == incomingConnection.Email &&
                                              c.Provider == incomingConnection.Provider);

                if (existingConnection == null)
                {
                    // New connection – just add the whole graph (ensure IDs are 0/default)
                    incomingConnection.Id = 0;
                    foreach (var cal in incomingConnection.Calendars)
                    {
                        cal.Id = 0;
                        foreach (var ev in cal.Events) ev.Id = 0;
                    }
                    dbContext.CalendarConnection.Add(incomingConnection);
                }
                else
                {
                    // Update existing connection
                    dbContext.Entry(existingConnection).CurrentValues.SetValues(incomingConnection);
                    // Update calendars and events (custom merge logic)
                    UpdateCalendarCollection(existingConnection.Calendars, incomingConnection.Calendars, dbContext);
                }

                await dbContext.SaveChangesAsync(); // ✅ One save for everything
                await transaction.CommitAsync();
            }
            catch { await transaction.RollbackAsync(); throw; }
        }

        private void UpdateCalendarCollection(ICollection<Calendar> existingCalendars,
                                      ICollection<Calendar> incomingCalendars,
                                      VaclavikBCContext dbContext)
        {
            // Simple upsert: match by IDProvider
            foreach (var incomingCal in incomingCalendars)
            {
                var existingCal = existingCalendars.FirstOrDefault(c => c.IDProvider == incomingCal.IDProvider);
                if (existingCal == null)
                {
                    incomingCal.Id = 0;
                    foreach (var ev in incomingCal.Events) ev.Id = 0;
                    existingCalendars.Add(incomingCal); // New calendar
                }
                else
                {
                    dbContext.Entry(existingCal).CurrentValues.SetValues(incomingCal);
                    UpdateEventCollection(existingCal.Events, incomingCal.Events, dbContext);
                }
            }
        }

        private void UpdateEventCollection(ICollection<CalendarEvent> existingEvents,
                                      ICollection<CalendarEvent> incomingEvents,
                                      VaclavikBCContext dbContext)
        {
            foreach (var incomingEv in incomingEvents)
            {
                var existingEv = existingEvents.FirstOrDefault(c => c.ProviderId == incomingEv.ProviderId);
                if (existingEv == null)
                {
                    existingEvents.Add(incomingEv);
                }
                else
                {
                    dbContext.Entry(existingEv).CurrentValues.SetValues(incomingEv);
                }
            }
        }


        //private async Task SyncCalendarDataAsync(Calendar calendarData)
        //{
        //    _logger.LogInformation("Starting calendar sync...");

        //    using var scope = _scopeFactory.CreateScope();
        //    var dbContext = scope.ServiceProvider.GetRequiredService<VaclavikBCContext>();
        //    using var transaction = await dbContext.Database.BeginTransactionAsync();
        //    //calendarData.CalendarConnection = null;
        //    try
        //    {//TODO tady to padá
        //        // --- Process the Calendar ---
        //        // Look for an existing calendar by its Google ID (IDProvider)
        //        var existingCalendar = await dbContext.Calendar
        //            .FirstOrDefaultAsync(c => c.Id == calendarData.Id || c.IDProvider == calendarData.IDProvider);
        //        if (existingCalendar == null)
        //        {
        //            calendarData.Id = 0;        // mělo by být 0, ale pro jistotu => aby to db brala jako novou entitu
        //            calendarData.CalendarConnection = null;
        //            await dbContext.Calendar.AddAsync(calendarData);
        //        }
        //        else
        //        {
        //            calendarData.Id = existingCalendar.Id;  //jinak v setValues dostaneme chybu, že nejde přepsat id
        //            calendarData.CalendarConnectionId = existingCalendar.CalendarConnectionId;
        //            dbContext.Entry(existingCalendar).CurrentValues.SetValues(calendarData);
        //        }

        //        // Save changes to get a CalendarId for new calendars
        //        await dbContext.SaveChangesAsync();

        //        // --- Process the Events ---
        //        // Get the Calendar entity after potential save
        //        var calendar = await dbContext.Calendar
        //            .FirstOrDefaultAsync(c => c.IDProvider == calendarData.IDProvider);

        //        foreach (var remoteEvent in calendarData.Events)
        //        {
        //            remoteEvent.Calendar = null;
        //            remoteEvent.CalendarId = calendar.Id;
        //            // Look for an existing event by its Google ID
        //            var existingEvent = await dbContext.CalendarEvent
        //                .FirstOrDefaultAsync(e => e.ProviderId == remoteEvent.ProviderId);

        //            if (existingEvent == null)
        //            {
        //                await dbContext.CalendarEvent.AddAsync(remoteEvent);
        //            }
        //            else
        //            {
        //                // Existing event: update its fields
        //                remoteEvent.Id = existingEvent.Id;
        //                dbContext.Entry(existingEvent).CurrentValues.SetValues(remoteEvent);
        //            }
        //        }

        //        // 4. Save all changes to the database
        //        await dbContext.SaveChangesAsync();

        //        // 5. Commit the transaction
        //        await transaction.CommitAsync();

        //        _logger.LogInformation("Calendar sync completed.");
        //    }
        //    catch (Exception)
        //    {
        //        await transaction.RollbackAsync();
        //        throw;
        //    }
        //}
    }
}