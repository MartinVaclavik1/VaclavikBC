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
        private readonly IConfiguration _config;
        public GoogleController(ISyncService syncService, IConfiguration config)
        {
            _syncService = syncService;
            _config = config;
        }
        public async Task<bool> ZiskejData(CalendarConnection callendarConnection)
        {
            using var client = new HttpClient();

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", callendarConnection.AccessToken);

            var response = await client.GetAsync(
                "https://www.googleapis.com/calendar/v3/users/me/calendarList");

            if (!response.IsSuccessStatusCode) 
                return false;

            var content = await response.Content.ReadAsStringAsync();   //next sync token s informacemi o kalendáři => id...

            List<string> calendars = new(); //název kalendáře
            List<string> calendarInfo = new();  //info o kalendáři
            if (JsonDocument.Parse(content).RootElement.TryGetProperty("items", out var items))
            {
                for (int i = 0; i < items.GetArrayLength(); i++)
                {
                    if (items[i].TryGetProperty("id", out var calendar))
                    {
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
                    "?singleEvents=false&timeZone=UTC";

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

                } while (pageToken != null);
                json = calendarInfo[i].Remove(calendarInfo[i].Length - 1) + "," + json.Remove(0, 1);  //smazání } a { 
                Calendar kalendar = JsonConvert.DeserializeObject<Calendar>(json);

                if (kalendar != null) { 
                    callendarConnection.Calendars.Add(kalendar);
                    kalendar.SetEventsReference();
                }
            }
            callendarConnection.SetCalendarsReference();
            await _syncService.SyncCalendarConnectionAsync(callendarConnection);
            return true;
        }

        /// <summary>
        /// vrací úspěšnost znovunačtení. Když se nenačte, tak je potřeba nový oauth
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public async Task<bool> RefreshConnectionAsync(CalendarConnection connection)
        {
            var valid = await connection.GetValidAccessTokenAsync(async (refreshToken) =>
            {
                var newToken = await RefreshAccessTokenAsync(refreshToken);
                if (newToken == null)
                    return (null, 0, null);
                return (newToken.AccessToken, newToken.ExpiresIn, null);
            });

            if (!valid)
                return false;
            
            return await ZiskejData(connection);
        }

        private async Task<GoogleTokenResponse?> RefreshAccessTokenAsync(string refreshToken)
        {
            var client = new HttpClient();
            var requestBody = new Dictionary<string, string>
            {
                ["client_id"] = _config["Google:ClientId"]!,
                ["client_secret"] = _config["Google:ClientSecret"]!,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            };

            var response = await client.PostAsync("https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(requestBody));

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<GoogleTokenResponse>(json);
        }

        public class GoogleTokenResponse
        {
            [JsonProperty("access_token")]
            public string AccessToken { get; set; } = "";
            [JsonProperty("expires_in")]
            public int ExpiresIn { get; set; }
        }
    }
}
