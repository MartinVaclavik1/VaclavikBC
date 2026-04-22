namespace VaclavikBC.Models
{
    /// <summary>
    /// ukládá připojení a informace k danému kalednářovému účtu
    /// </summary>
    public class CalendarConnection
    {
        /*
            "kind": "calendar#calendarList",
            "etag": "\"p32nrtd4uieip60o\"",
            "nextSyncToken": "CK--tJ6TpZMDEhdiY3ByYWNlbWFydGluQGdtYWlsLmNvbQ==",
         */
        public int Id { get; set; }
        public string UserId { get; set; }
        public string Email { get; set; }
        public string Provider { get; set; } //Google/Microsoft..
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime ExpirationTime { get; set; } //expires_at: 2026-03-20T14:06:58.6296591+00:00
        public List<Calendar> Calendars { get; set; } = new();

        public void SetCalendarsReference()
        {
            foreach (var kalendarEvent in Calendars)
            {
                kalendarEvent.CalendarConnection = this;
            }
        }
    }
}
