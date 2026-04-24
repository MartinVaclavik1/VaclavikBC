using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using VaclavikBC.Models;
using VaclavikBC.Services.Interfaces;

namespace VaclavikBC.Controllers
{
    public class CalendlyController
    {
        private readonly ISyncService _syncService;
        private readonly IConfiguration _config;
        const string BaseUrl = "https://api.calendly.com";
        public CalendlyController(ISyncService syncService, IConfiguration config)
        {
            _syncService = syncService;
            _config = config;
        }

        public async Task<bool> ZiskejData(CalendarConnection calendarConnection)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", calendarConnection.AccessToken);

            var userResponse = await client.GetAsync($"{BaseUrl}/users/me");
            if (!userResponse.IsSuccessStatusCode)
                return false;
            var userContent = await userResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(userContent);
            var userUri = doc.RootElement.GetProperty("resource").GetProperty("uri").GetString();

            var eventTypesResponse = await client.GetAsync(
                $"{BaseUrl}/event_types?user={Uri.EscapeDataString(userUri)}");
            if (!eventTypesResponse.IsSuccessStatusCode)
                return false;
            var typesJson = await eventTypesResponse.Content.ReadAsStringAsync();
            using var typesDoc = JsonDocument.Parse(typesJson);

            var calendars = new Dictionary<string, Calendar>();
            if (typesDoc.RootElement.TryGetProperty("collection", out var eventTypes))
            {
                foreach (var et in eventTypes.EnumerateArray())
                {
                    var uri = et.GetProperty("uri").GetString();
                    calendars[uri] = new Calendar
                    {
                        IDProvider = uri,
                        Name = et.GetProperty("name").GetString(),
                        TimeZone = et.TryGetProperty("timezone", out var tz) ? tz.GetString() : "UTC",
                        BackgroundColor = et.TryGetProperty("color", out var c) ? c.GetString() : "#000000",
                        Selected = true
                    };
                }
            }

            string pageToken = null;
            do
            {
                var eventsUrl = $"{BaseUrl}/scheduled_events" +
                                $"?user={Uri.EscapeDataString(userUri)}" +
                                $"&status=active";

                if (!string.IsNullOrEmpty(pageToken))
                    eventsUrl += $"&page_token={pageToken}";

                var eventsResponse = await client.GetAsync(eventsUrl);

                if (!eventsResponse.IsSuccessStatusCode)
                    return false;

                var eventsJson = await eventsResponse.Content.ReadAsStringAsync();
                using var eventsDoc = JsonDocument.Parse(eventsJson);

                if (eventsDoc.RootElement.TryGetProperty("collection", out var events))
                {
                    foreach (var ev in events.EnumerateArray())
                    {
                        var eventTypeUri = ev.TryGetProperty("event_type", out var et)
                                           ? et.GetString() : null;

                        if (eventTypeUri != null && calendars.TryGetValue(eventTypeUri, out var cal))
                        {
                            cal.Events.Add(new CalendarEvent
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
                                },
                                Calendar = cal,
                                CalendarId = cal.Id
                            });
                        }
                    }
                }

                pageToken = eventsDoc.RootElement.TryGetProperty("pagination", out var pag) &&
                            pag.TryGetProperty("next_page_token", out var token) &&
                            token.ValueKind != JsonValueKind.Null
                            ? token.GetString()
                            : null;

            } while (pageToken != null);

            foreach (var cal in calendars.Values)
            {
                cal.SetEventsReference();
                calendarConnection.Calendars.Add(cal);
            }
            calendarConnection.SetCalendarsReference();

            await _syncService.SyncCalendarConnectionAsync(calendarConnection);
            return true;
        }

        public async Task<bool> RefreshConnectionAsync(CalendarConnection conn)
        {
            var valid = await conn.GetValidAccessTokenAsync(async (refreshToken) =>
            {
                var newToken = await RefreshAccessTokenAsync(refreshToken);
                if (newToken == null)
                    return (null, 0, null);
                return (newToken.AccessToken, newToken.ExpiresIn, newToken.RefreshToken);
            });

            if (!valid)
                return false;

            return await ZiskejData(conn);
        }

        private async Task<CalendlyTokenResponse?> RefreshAccessTokenAsync(string refreshToken)
        {
            using var client = new HttpClient();
            var requestBody = new Dictionary<string, string>
            {
                ["client_id"] = _config["Calendly:ClientId"]!,
                ["client_secret"] = _config["Calendly:ClientSecret"]!,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            };

            var response = await client.PostAsync("https://auth.calendly.com/oauth/token",
                new FormUrlEncodedContent(requestBody));

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<CalendlyTokenResponse>(json);
        }

        public class CalendlyTokenResponse
        {
            [JsonProperty("access_token")]
            public string AccessToken { get; set; } = "";
            [JsonProperty("expires_in")]
            public int ExpiresIn { get; set; }
            [JsonProperty("refresh_token")]
            public string? RefreshToken { get; set; }
        }

    }
}

