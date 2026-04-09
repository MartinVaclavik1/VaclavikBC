using Microsoft.EntityFrameworkCore;
using VaclavikBC.Data;
using VaclavikBC.Models;
using VaclavikBC.Services.Interfaces;

namespace VaclavikBC.Services
{
    public class SyncService: ISyncService
    {
        private readonly ILogger<SyncService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        public SyncService(IServiceScopeFactory scopeFactory,
            ILogger<SyncService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            
        }

        public async Task SyncCalendarDataAsync(Calendar calendarData)
        {
            _logger.LogInformation("Starting calendar sync...");
            
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<VaclavikBCContext>();
            using var transaction = await dbContext.Database.BeginTransactionAsync();
            try
            {
                // --- Process the Calendar ---
                // Look for an existing calendar by its Google ID (IDProvider)
                var existingCalendar = await dbContext.Calendar
                    .FirstOrDefaultAsync(c => c.IDProvider == calendarData.IDProvider);
                if (existingCalendar == null)
                {
                    calendarData.Id = 0; // mělo by být 0, ale pro jistotu => aby to db brala jako novou entitu
                    await dbContext.Calendar.AddAsync(calendarData);
                }
                else
                {
                    calendarData.Id = existingCalendar.Id;  //jinak v setValues dostaneme chybu, že nejde přepsat id
                    dbContext.Entry(existingCalendar).CurrentValues.SetValues(calendarData);
                }

                // Save changes to get a CalendarId for new calendars
                await dbContext.SaveChangesAsync();

                // --- Process the Events ---
                // Get the Calendar entity after potential save
                var calendar = await dbContext.Calendar
                    .FirstOrDefaultAsync(c => c.IDProvider == calendarData.IDProvider);

                foreach (var remoteEvent in calendarData.Events)
                {
                    // Look for an existing event by its Google ID
                    var existingEvent = await dbContext.CalendarEvent
                        .FirstOrDefaultAsync(e => e.ProviderId == remoteEvent.ProviderId);

                    if (existingEvent == null)
                    {
                        // New event: link it to the calendar
                        remoteEvent.CalendarId = calendar.Id;
                        await dbContext.CalendarEvent.AddAsync(remoteEvent);
                    }
                    else
                    {
                        // Existing event: update its fields
                        existingEvent.Title = remoteEvent.Title;
                        existingEvent.StartInfo = remoteEvent.StartInfo;
                        existingEvent.EndInfo = remoteEvent.EndInfo;
                        existingEvent.RecurrenceRules = remoteEvent.RecurrenceRules;
                        // ... map any other fields you want to update
                    }
                }

                // 4. Save all changes to the database
                await dbContext.SaveChangesAsync();

                // 5. Commit the transaction
                await transaction.CommitAsync();
                
                _logger.LogInformation("Calendar sync completed.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
