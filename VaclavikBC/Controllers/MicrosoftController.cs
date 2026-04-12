using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using VaclavikBC.Models;
using VaclavikBC.Services.Interfaces;

namespace VaclavikBC.Controllers
{
    public class MicrosoftController : Controller
    {
        private readonly ISyncService _syncService;

        public MicrosoftController(ISyncService syncService)
        {
            _syncService = syncService;
        }


        //TODO dodělat
        public async Task ZiskejData(CalendarConnection calendarConnection)
        {
            //if (calendarConnection == null)
            //    throw new ArgumentNullException(nameof(calendarConnection));

            //// 1. Get all calendars for the user
            //var calendarsPage = await _graphServiceClient.Me.Calendars
            //    .Request()
            //    .GetAsync();

            //var calendars = calendarsPage?.CurrentPage;
            //if (calendars == null || !calendars.Any())
            //    return; // No calendars to sync

            //// Store calendar metadata and events
            //var calendarModels = new List<VaclavikBC.Models.Calendar>();

            //foreach (var msCalendar in calendars)
            //{
            //    // 2. Get all events for this calendar (handle pagination)
            //    var eventsList = new List<Event>();
            //    var eventsPage = await _graphServiceClient.Me.Calendars[msCalendar.Id].Events
            //        .Request()
            //        .GetAsync();

            //    eventsList.AddRange(eventsPage.CurrentPage);
            //    while (eventsPage.NextPageRequest != null)
            //    {
            //        eventsPage = await eventsPage.NextPageRequest.GetAsync();
            //        eventsList.AddRange(eventsPage.CurrentPage);
            //    }

            //    // 3. Map Microsoft Graph objects to your domain models
            //    var customCalendar = MapToCalendar(msCalendar, eventsList);
            //    calendarModels.Add(customCalendar);
            //}

            //calendarConnection.Calendars = calendarModels;
            //calendarConnection.SetCalendarsReference(); // if needed
            //await _syncService.SyncCalendarConnectionAsync(calendarConnection);
        }

        private VaclavikBC.Models.Calendar MapToCalendar(Microsoft.Graph.Calendar source, List<Event> events)
        {
            // Adapt property names to match your VaclavikBC.Models.Calendar
            var calendar = new Models.Calendar
            {
                IDProvider = source.Id,
                Name = source.Name,

                // Add other properties if your model has them:
                // Color = source.Color,
                // Owner = source.Owner?.Name,
            };

            // Map events (assuming your Calendar has an Items collection of CalendarItem)
            if (events != null && events.Any())
            {
                calendar.Events = events.Select(e => new CalendarEvent
                {
                    ProviderId = e.Id,
                    Title = e.Subject,
                    StartInfo = new EventDateTime
                    {
                        DateTime = DateTime.Parse(e.Start?.DateTime)
                    },
                    EndInfo = new EventDateTime
                    {
                        DateTime = DateTime.Parse(e.End?.DateTime)
                    }
                    // Map other fields (location, attendees, etc.)
                }).ToList();
            }

            return calendar;
        }
    }
}