using Azure.Core;
using System.Net.Http.Headers;
using System.Text.Json;

namespace VaclavikBC.Controllers
{
    public class GoogleController
    {
        public static async void ZiskejData(string accessToken)
        {
            using var client = new HttpClient();

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync(
                "https://www.googleapis.com/calendar/v3/users/me/calendarList");

            var content = await response.Content.ReadAsStringAsync();

            Console.WriteLine(content);

            string pageToken = null;
            using (StreamWriter writer = new StreamWriter("vystup.txt"))
            {
                do
                {
                    //https://www.googleapis.com/calendar/v3/calendars/{NAZEV KALENDARE (id)}/events    bcpracemartin@gmail.com
                    var url = "https://www.googleapis.com/calendar/v3/calendars/primary/events" +
                    "?singleEvents=false";

                    if (pageToken != null)
                        url += $"&pageToken={pageToken}";

                    var ulohy = await client.GetAsync(url);
                    ulohy.EnsureSuccessStatusCode();

                    var json = await ulohy.Content.ReadAsStringAsync();


                    using var doc = JsonDocument.Parse(json);


                    if (doc.RootElement.TryGetProperty("nextPageToken", out var token))
                    {
                        pageToken = token.GetString();
                    }
                    else { pageToken = null; }

                    //Console.WriteLine(json);
                    writer.WriteLine(json);
                } while (pageToken != null);
            }
        }
    }

    
}
