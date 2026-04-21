using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text.Json;
using VaclavikBC.Models;
using VaclavikBC.Services.Interfaces;

namespace VaclavikBC.Controllers
{
    public class CalendlyController
    {
        private readonly ISyncService _syncService;

        public CalendlyController(ISyncService syncService)
        {
            _syncService = syncService;
        }

        public async void ZiskejData(CalendarConnection calendarConnection)
        {
            using var client = new HttpClient();

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", calendarConnection.AccessToken);

            // 1. Get current user's URI
            var userResponse = await client.GetAsync("https://api.calendly.com/users/me");
            userResponse.EnsureSuccessStatusCode();
            var userContent = await userResponse.Content.ReadAsStringAsync();
            using var userDoc = JsonDocument.Parse(userContent);
            var userUri = userDoc.RootElement.GetProperty("resource").GetProperty("uri").GetString();

            // 2. Get all event types (these act as calendars)
            var eventTypesResponse = await client.GetAsync(
                $"https://api.calendly.com/event_types?user={Uri.EscapeDataString(userUri)}");
            eventTypesResponse.EnsureSuccessStatusCode();
            var eventTypesContent = await eventTypesResponse.Content.ReadAsStringAsync();
            using var eventTypesDoc = JsonDocument.Parse(eventTypesContent);
            Console.WriteLine(eventTypesContent.ToString());
            if (!eventTypesDoc.RootElement.TryGetProperty("collection", out var eventTypes))
                return;
            Console.WriteLine(eventTypes.ToString());
            // 3. Process each event type as a calendar
            foreach (var eventType in eventTypes.EnumerateArray())
            {
                // Create a Calendar object from the event type data
                var calendar = new Calendar
                {
                    IDProvider = eventType.GetProperty("uri").GetString(),
                    Name = eventType.GetProperty("name").GetString(),
                    TimeZone = eventType.TryGetProperty("timezone", out var tz) ? tz.GetString() : "UTC",
                    BackgroundColor = eventType.TryGetProperty("color", out var color) ? color.GetString() : "#000000",
                    Selected = true //calendly nemá selected, tak v základu zobrazíme
                };

                // 4. Fetch all scheduled events for this event type
                string pageToken = null;
                do
                {
                    //TODO?
                    var eventsUrl = $"https://api.calendly.com/scheduled_events?user={Uri.EscapeDataString(userUri)}&status=active"; if (!string.IsNullOrEmpty(pageToken))
                        eventsUrl += $"&page_token={pageToken}";

                    var eventsResponse = await client.GetAsync(eventsUrl);
                    eventsResponse.EnsureSuccessStatusCode();
                    var eventsJson = await eventsResponse.Content.ReadAsStringAsync();
                    using var eventsDoc = JsonDocument.Parse(eventsJson);

                    if (eventsDoc.RootElement.TryGetProperty("collection", out var eventsCollection))
                    {
                        foreach (var ev in eventsCollection.EnumerateArray())
                        {
                            // Map Calendly event to CalendarEvent
                            var calendarEvent = new CalendarEvent
                            {
                                ProviderId = ev.GetProperty("uri").GetString(),
                                Title = ev.GetProperty("name").GetString(),
                                StartInfo = new EventDateTime
                                {
                                    DateTime = DateTimeOffset.Parse(ev.GetProperty("start_time").GetString()),
                                    TimeZone = "UTC"
                                },
                                EndInfo = new EventDateTime
                                {
                                    DateTime = DateTimeOffset.Parse(ev.GetProperty("end_time").GetString()),
                                    TimeZone = "UTC"
                                }
                                // Add other properties if needed (status, location, etc.)
                            };
                            calendar.Events.Add(calendarEvent);
                        }
                    }

                    // Pagination
                    pageToken = eventsDoc.RootElement.TryGetProperty("pagination", out var pagination) &&
                                pagination.TryGetProperty("next_page_token", out var token) &&
                                token.ValueKind != JsonValueKind.Null
                                ? token.GetString()
                                : null;

                } while (pageToken != null);

                // 5. Set reference and add to connection
                calendar.SetEventsReference();
                calendarConnection.Calendars.Add(calendar);
            }

            calendarConnection.SetCalendarsReference();
            await _syncService.SyncCalendarConnectionAsync(calendarConnection);
        }
    }
}

