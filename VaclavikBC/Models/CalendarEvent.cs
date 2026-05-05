using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace VaclavikBC.Models
{
    public class CalendarEvent
    {
        /*
           {
           "kind": "calendar#event",
           "etag": "\"3548030535227294\"",
           "id": "6du74f5ol34urpnklmshad59jk_20900310T160000Z",
           "status": "confirmed",
           "htmlLink": "https://www.google.com/calendar/event?eid=NmR1NzRmNW9sMzR1cnBua2xtc2hhZDU5amtfMjA5MDAzMTBUMTYwMDAwWiBiY3ByYWNlbWFydGluQG0",
           "created": "2026-03-20T14:01:07.000Z",
           "updated": "2026-03-20T14:01:07.613Z",
           "summary": "rocne",
           "creator": {
            "email": "bcpracemartin@gmail.com",
            "self": true
           },
           "organizer": {
            "email": "bcpracemartin@gmail.com",
            "self": true
           },
           "start": {
            "dateTime": "2090-03-10T17:00:00+01:00",
            "timeZone": "Europe/Prague"
           },
           "end": {
            "dateTime": "2090-03-10T18:00:00+01:00",
            "timeZone": "Europe/Prague"
           },
           "recurringEventId": "6du74f5ol34urpnklmshad59jk",
           "originalStartTime": {
            "dateTime": "2090-03-10T17:00:00+01:00",
            "timeZone": "Europe/Prague"
           },
           "iCalUID": "6du74f5ol34urpnklmshad59jk@google.com",
           "sequence": 0,
           "reminders": {
            "useDefault": true
           },
           "eventType": "default"
          }


        {
       "kind": "calendar#event",
       "etag": "\"3548030806429790\"",
       "id": "07tbghie965s1c4tt9h6sltsap",
       "status": "confirmed",
       "htmlLink": "https://www.google.com/calendar/event?eid=MDd0YmdoaWU5NjVzMWM0dHQ5aDZzbHRzYXBfMjAyNjAzMTBUMjEwMDAwWiBiY3ByYWNlbWFydGluQG0",
       "created": "2026-03-20T14:03:23.000Z",
       "updated": "2026-03-20T14:03:23.214Z",
       "summary": "vlastni konec poctem opakovani 10. dnem",
       "creator": {
        "email": "bcpracemartin@gmail.com",
        "self": true
       },
       "organizer": {
        "email": "bcpracemartin@gmail.com",
        "self": true
       },
       "start": {
        "dateTime": "2026-03-10T22:00:00+01:00",
        "timeZone": "Europe/Prague"
       },
       "end": {
        "dateTime": "2026-03-10T23:00:00+01:00",
        "timeZone": "Europe/Prague"
       },
       "recurrence": [
        "RRULE:FREQ=MONTHLY;COUNT=13"
       ],
       "iCalUID": "07tbghie965s1c4tt9h6sltsap@google.com",
       "sequence": 0,
       "reminders": {
        "useDefault": true
       },
       "eventType": "default"
      }
        */
        public int Id { get; set; }

        [JsonProperty("id")]
        public string ProviderId { get; set; }

        [JsonProperty("summary")]
        public string Title { get; set; } = "(Title not set)";

        [JsonProperty("start")]
        public EventDateTime StartInfo { get; set; }

        [JsonProperty("end")]
        public EventDateTime EndInfo { get; set; }

        [JsonProperty("recurrence")]
        public List<string>? RecurrenceRules { get; set; }

        public DateTime Start => StartInfo?.GetDateTime() ?? DateTime.MinValue;
        public DateTime End => EndInfo?.GetDateTime() ?? DateTime.MinValue;
        public string TimeZone => StartInfo?.TimeZone ?? EndInfo?.TimeZone ?? "";

        [JsonIgnore]
        public int CalendarId { get; set; }
        [JsonIgnore]
        public Calendar Calendar { get; set; }

        public string? RRULE => RecurrenceRules?.FirstOrDefault(r => r.StartsWith("RRULE:"))?.Substring(6);
        /*
         * "RRULE:FREQ=DAILY"
         * "RRULE:FREQ=WEEKLY;BYDAY=TU"
         * "RRULE:FREQ=MONTHLY;BYDAY=2TU"
         * "RRULE:FREQ=YEARLY"
         * "RRULE:FREQ=WEEKLY;BYDAY=FR,MO,TH,TU,WE"
         * "RRULE:FREQ=WEEKLY;WKST=SU;UNTIL=20260617T215959Z;BYDAY=TU,TH,SU"
         * "RRULE:FREQ=MONTHLY;COUNT=13"
         */
    }

    [Owned]
    public class EventDateTime
    {
        [JsonProperty("dateTime")]
        [DataType(DataType.DateTime)]
        public DateTimeOffset? DateTime { get; set; }

        [JsonProperty("date")]
        [DataType(DataType.Date)]
        public DateTime? Date { get; set; }

        [JsonProperty("timeZone")]
        public string? TimeZone { get; set; }

        public DateTime GetDateTime()
        {
            if (DateTime.HasValue)
                return DateTime.Value.UtcDateTime;
            if (Date.HasValue)
                return System.DateTime.SpecifyKind(Date.Value, DateTimeKind.Utc);

            throw new InvalidOperationException("No date or dateTime provided");
        }
    }
}
