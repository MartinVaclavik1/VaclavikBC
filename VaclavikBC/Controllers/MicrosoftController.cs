using Azure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Text.Json;
using VaclavikBC.Models;
using VaclavikBC.Services.Interfaces;

namespace VaclavikBC.Controllers;

[Authorize]
public class MicrosoftController : Controller
{
    private readonly ISyncService _syncService;
    private readonly IConfiguration _config;
    public MicrosoftController(ISyncService syncService, IConfiguration config)
    {
        _syncService = syncService;
        _config = config;
    }

    public async Task<bool> ZiskejData(CalendarConnection calendarConnection)
    {
        using var client = new HttpClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", calendarConnection.AccessToken);

        var calendarsResponse = await client.GetAsync("https://graph.microsoft.com/v1.0/me/calendars");
        if (!calendarsResponse.IsSuccessStatusCode)
            return false;
        var calendarsContent = await calendarsResponse.Content.ReadAsStringAsync();

        using var calendarsDoc = JsonDocument.Parse(calendarsContent);
        if (!calendarsDoc.RootElement.TryGetProperty("value", out var calendarsArray))
        {
            throw new Exception("No calendars found in response.");
        }

        var calendarModels = new List<Calendar>();

        foreach (var calElement in calendarsArray.EnumerateArray())
        {
            var calendar = new Calendar
            {
                IDProvider = calElement.GetProperty("id").GetString(),
                Name = calElement.GetProperty("name").GetString(),
                TimeZone = calElement.TryGetProperty("timeZone", out var tz) ? tz.GetString() : "",
                //color má možnost auto => v tom případě nastavíme v základu modrou barvu, co používá outlook
                BackgroundColor = calElement.TryGetProperty("color", out var bg) ? (bg.GetString() == "auto"? "#0f6cbd" : bg.GetString()) : "#0f6cbd",   
                //Foreground nepotřebujeme a ani se neukládá
                //ForegroundColor = calElement.TryGetProperty("foregroundColor", out var fg) ? fg.GetString() : null,
                Selected = calElement.TryGetProperty("isDefaultCalendar", out var def) ? def.GetBoolean() : false,
                // NextSyncToken se nepoužívá v graphAPI
                NextSyncToken = null
            };

            string nextLink = null;
            do
            {
                string url;
                if (string.IsNullOrEmpty(nextLink))
                {
                    url = $"https://graph.microsoft.com/v1.0/me/calendars/{Uri.EscapeDataString(calendar.IDProvider)}/events";
                }
                else
                {
                    url = nextLink;
                }

                var eventsResponse = await client.GetAsync(url);
                if (!eventsResponse.IsSuccessStatusCode)
                    break;

                var eventsJson = await eventsResponse.Content.ReadAsStringAsync();
                using var eventsDoc = JsonDocument.Parse(eventsJson);
                if (eventsDoc.RootElement.TryGetProperty("value", out var eventsArray))
                {
                    foreach (var ev in eventsArray.EnumerateArray())
                    {
                        var calendarEvent = MapEventFromGraph(ev);
                        calendarEvent.Calendar = calendar;
                        calendar.Events.Add(calendarEvent);
                    }
                }

                nextLink = eventsDoc.RootElement.TryGetProperty("@odata.nextLink", out var nextLinkToken)
                    ? nextLinkToken.GetString()
                    : null;

            } while (!string.IsNullOrEmpty(nextLink));

            calendarModels.Add(calendar);
            calendar.SetEventsReference();
        }

        calendarConnection.Calendars = calendarModels;
        calendarConnection.SetCalendarsReference();
        await _syncService.SyncCalendarConnectionAsync(calendarConnection);
        return true;
    }
    private CalendarEvent MapEventFromGraph(JsonElement ev)
    {
        var calendarEvent = new CalendarEvent
        {
            ProviderId = ev.GetProperty("id").GetString(),
            StartInfo = MapEventDateTime(ev.GetProperty("start")),
            EndInfo = MapEventDateTime(ev.GetProperty("end")),
            RecurrenceRules = new List<string>()
        };
        
        if (ev.TryGetProperty("subject", out var subject))  //title je defaultně nastaven
        {
            calendarEvent.Title = subject.GetString();
        }
            

        if (ev.TryGetProperty("recurrence", out var recurrence) && recurrence.ValueKind != JsonValueKind.Null)
        {
            var rrule = ConvertGraphRecurrenceToRRULE(recurrence);
            if (!string.IsNullOrEmpty(rrule))
                calendarEvent.RecurrenceRules.Add(rrule);
        }

        return calendarEvent;
    }

    private EventDateTime MapEventDateTime(JsonElement dateTimeElement)
    {
        var eventDateTime = new EventDateTime();
        if (dateTimeElement.TryGetProperty("dateTime", out var dt))
        {
            eventDateTime.DateTime = DateTimeOffset.Parse(dt.GetString());
        }
        if (dateTimeElement.TryGetProperty("date", out var d))
        {
            eventDateTime.Date = DateTime.Parse(d.GetString());
        }
        if (dateTimeElement.TryGetProperty("timeZone", out var tz))
        {
            eventDateTime.TimeZone = tz.GetString();
        }
        return eventDateTime;
    }

    //namapování rrule, protože microsoft musí být speciální
    private string? ConvertGraphRecurrenceToRRULE(JsonElement recurrence)
    {
        if (recurrence.ValueKind == JsonValueKind.Null || recurrence.ValueKind == JsonValueKind.Undefined)
            return null;

        if (!recurrence.TryGetProperty("pattern", out var pattern) || !recurrence.TryGetProperty("range", out var range))
            return null;

        var parts = new List<string>();

        if (!pattern.TryGetProperty("type", out var typeToken)) return null;
        string type = typeToken.GetString();
        int interval = pattern.TryGetProperty("interval", out var intv) ? intv.GetInt32() : 1;

        switch (type)
        {
            case "daily":
                parts.Add("RRULE:FREQ=DAILY");
                break;
            case "weekly":
                parts.Add("RRULE:FREQ=WEEKLY");
                if (pattern.TryGetProperty("daysOfWeek", out var days) && days.ValueKind == JsonValueKind.Array)
                {
                    var dayList = days.EnumerateArray().Select(d => d.GetString()).ToList();
                    var rruleDays = dayList.Select(d => d.Substring(0, 2).ToUpperInvariant());
                    parts.Add($"BYDAY={string.Join(",", rruleDays)}");
                }
                break;
            case "absoluteMonthly":
                parts.Add("RRULE:FREQ=MONTHLY");
                if (pattern.TryGetProperty("dayOfMonth", out var dom))
                    parts.Add($"BYMONTHDAY={dom.GetInt32()}");
                break;
            case "relativeMonthly":
                parts.Add("RRULE:FREQ=MONTHLY");
                if (pattern.TryGetProperty("index", out var idx) && pattern.TryGetProperty("daysOfWeek", out var rdays))
                {
                    string indexStr = idx.GetString();
                    string indexMap = indexStr switch
                    {
                        "first" => "1",
                        "second" => "2",
                        "third" => "3",
                        "fourth" => "4",
                        "last" => "-1",
                        _ => ""
                    };
                    string day = rdays.EnumerateArray().First().GetString(); 
                    string rruleDay = day.Substring(0, 2).ToUpperInvariant();
                    if (!string.IsNullOrEmpty(indexMap))
                        parts.Add($"BYDAY={indexMap}{rruleDay}");
                }
                break;
            case "absoluteYearly":
                parts.Add("RRULE:FREQ=YEARLY");
                if (pattern.TryGetProperty("month", out var month))
                    parts.Add($"BYMONTH={month.GetInt32()}");
                if (pattern.TryGetProperty("dayOfMonth", out var ydom))
                    parts.Add($"BYMONTHDAY={ydom.GetInt32()}");
                break;
            case "relativeYearly":
                parts.Add("RRULE:FREQ=YEARLY");
                if (pattern.TryGetProperty("month", out var rmonth))
                    parts.Add($"BYMONTH={rmonth.GetInt32()}");
                if (pattern.TryGetProperty("index", out var ridx) && pattern.TryGetProperty("daysOfWeek", out var rydays))
                {
                    string indexStr = ridx.GetString();
                    string indexMap = indexStr switch
                    {
                        "first" => "1",
                        "second" => "2",
                        "third" => "3",
                        "fourth" => "4",
                        "last" => "-1",
                        _ => ""
                    };
                    string day = rydays.EnumerateArray().First().GetString();
                    string rruleDay = day.Substring(0, 2).ToUpperInvariant();
                    if (!string.IsNullOrEmpty(indexMap))
                        parts.Add($"BYDAY={indexMap}{rruleDay}");
                }
                break;
            default:
                return null;
        }

        if (interval > 1)
            parts.Add($"INTERVAL={interval}");

        if (!range.TryGetProperty("type", out var rangeType)) return null;
        string rType = rangeType.GetString();
        if (rType == "endDate" && range.TryGetProperty("endDate", out var endDate))
        {
            string dateStr = endDate.GetString();
            string until = dateStr.Replace("-", "") + "T235959Z";
            parts.Add($"UNTIL={until}");
        }
        else if (rType == "numbered" && range.TryGetProperty("numberOfOccurrences", out var count))
        {
            parts.Add($"COUNT={count.GetInt32()}");
        }
        return string.Join(";", parts);
    }

    public async Task<bool> RefreshConnectionAsync(CalendarConnection connection)
    {

        bool tokenValid = await connection.GetValidAccessTokenAsync(async (refreshToken) =>
        {
            var newToken = await RefreshAccessTokenAsync(refreshToken);
            if (newToken == null)
                return (null, 0, null);
            // Microsoft may return a new refresh token; pass it along.
            return (newToken.AccessToken, newToken.ExpiresIn, newToken.RefreshToken);
        });

        if (!tokenValid)
            return false;

        return await ZiskejData(connection);
    }

    private async Task<MicrosoftTokenResponse?> RefreshAccessTokenAsync(string refreshToken)
    {
        var client = new HttpClient();
        var requestBody = new Dictionary<string, string>
        {
            ["client_id"] = _config["AzureAd:ClientId"]!,
            ["client_secret"] = _config["AzureAd:ClientSecret"]!,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
            ["scope"] = "https://graph.microsoft.com/Calendars.Read offline_access openid profile email"
        };
        

        var response = await client.PostAsync(
        "https://login.microsoftonline.com/common/oauth2/v2.0/token",
        new FormUrlEncodedContent(requestBody)
    );
        //var response = await client.SendAsync(request);
        var responseString = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Microsoft refresh error: {response.StatusCode} - {responseString}");
            return null;
        }
        var json = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<MicrosoftTokenResponse>(json);
    }

    private class MicrosoftTokenResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; } = "";

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; } = "";
    }
}