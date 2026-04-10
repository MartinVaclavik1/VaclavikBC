using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using VaclavikBC.Models;
using VaclavikBC.Services.Interfaces;

namespace VaclavikBC.Controllers
{
    public class GoogleController
    {
        private readonly ISyncService _syncService;
        public GoogleController(ISyncService syncService)
        {
            _syncService = syncService;
        }
        public async void ZiskejData(CalendarConnection callendarConnection)
        {
            using var client = new HttpClient();

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", callendarConnection.AccessToken);

            var response = await client.GetAsync(
                "https://www.googleapis.com/calendar/v3/users/me/calendarList");

            var content = await response.Content.ReadAsStringAsync();   //next sync token s informacemi o kalendáři => id...

            List<string> calendars = new(); //název kalendáře
            List<string> calendarInfo = new();  //info o kalendáři
            if (JsonDocument.Parse(content).RootElement.TryGetProperty("items", out var items))
            {
                for (int i = 0; i < items.GetArrayLength(); i++)
                {
                    if (items[i].TryGetProperty("id", out var calendar))
                    {
                        //Console.WriteLine(calendar);
                        calendars.Add(calendar.ToString());
                        calendarInfo.Add(items[i].ToString());
                    }
                }

            }

            string pageToken = null;


            for (int i = 0; i < calendars.Count; i++)
            {
                var calendar = calendars[i];
                string json = "";
                do
                {
                    //https://www.googleapis.com/calendar/v3/calendars/{NAZEV KALENDARE (id)}/events    bcpracemartin@gmail.com
                    var url = $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendar)}/events" +
                    "?singleEvents=false";

                    if (pageToken != null)
                        url += $"&pageToken={pageToken}";

                    var ulohy = await client.GetAsync(url);
                    ulohy.EnsureSuccessStatusCode();

                    json += await ulohy.Content.ReadAsStringAsync();



                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("nextPageToken", out var token))
                    {
                        pageToken = token.GetString();
                    }
                    else { pageToken = null; }

                    //writer.WriteLine(json);

                } while (pageToken != null);
                json = calendarInfo[i].Remove(calendarInfo[i].Length - 1) + "," + json.Remove(0, 1);  //smazání } a { 
                Console.WriteLine(json);
                Calendar kalendar = JsonConvert.DeserializeObject<Calendar>(json);

                if (kalendar != null) { 
                    callendarConnection.Calendars.Add(kalendar);
                    kalendar.SetEventsReference();
                }
            }
            callendarConnection.SetCalendarsReference();
            await _syncService.SyncCalendarConnectionAsync(callendarConnection);
        }
    }
}
