using Newtonsoft.Json;

namespace VaclavikBC.Models
{
    public class Calendar
    {
        /*
           "kind": "calendar#calendarListEntry",
           "etag": "\"1773067877088367\"",
           "id": "bcpracemartin@gmail.com",
           "summary": "bcpracemartin@gmail.com",
           "timeZone": "Europe/Prague",
           "colorId": "14",
           "backgroundColor": "#9fe1e7",
           "foregroundColor": "#000000",
           "selected": true,
           "accessRole": "owner"
        */
        public int Id { get; set; }
        [JsonProperty("id")]
        public string? IDProvider { get; set; } //např. u státních svátků se nenastavuje
        [JsonProperty("summary")]
        public string Name { get; set; } //summary
        [JsonProperty("timeZone")]
        public string TimeZone { get; set; } //příklad: Europe/prague
        [JsonProperty("backgroundColor")]
        public string? BackgroundColor { get; set; }
        [JsonProperty("foregroundColor")]
        public string? ForegroundColor { get; set; }
        [JsonProperty("selected")]
        public bool Selected { get; set; }
        [JsonProperty("nextSyncToken")]
        public string? NextSyncToken { get; set; }
        [JsonProperty("items")]
        public List<CalendarEvent> Events { get; set; } = new List<CalendarEvent>();
        [JsonIgnore]
        public int CalendarConnectionId { get; set; }
        [JsonIgnore]
        public CalendarConnection CalendarConnection { get; set; }
        public void SetEventsReference()
        {
            foreach (var kalendarEvent in Events)
            {
                kalendarEvent.Calendar = this;
            }
        }
    }
}
