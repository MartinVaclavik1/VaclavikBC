using Humanizer;
using Ical.Net.DataTypes;
using Ical.Net.Evaluation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;
using TimeZoneConverter;
using VaclavikBC.Data;
using VaclavikBC.Hubs;
using VaclavikBC.Models;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace VaclavikBC.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CalendarAPIController : ControllerBase
    {
        private readonly VaclavikBCContext _context;

        private readonly IHubContext<CalendarSyncHub> _hubContext;
        public CalendarAPIController(VaclavikBCContext context, IHubContext<CalendarSyncHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        [HttpGet("events")]
        public async Task<IActionResult> GetEvents(
       [FromQuery] DateTime start,
       [FromQuery] DateTime end,
       [FromQuery] string timezone = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            TimeZoneInfo targetZone = TimeZoneInfo.Utc;
            if (!string.IsNullOrEmpty(timezone))
            {
                try
                {
                    targetZone = TZConvert.GetTimeZoneInfo(timezone);
                }
                catch (TimeZoneNotFoundException)
                {
                    targetZone = TimeZoneInfo.Utc;
                }
            }

            var selectedCalendars = await _context.Calendar
                .Include(c => c.Events)
                .Where(c => c.Selected && c.CalendarConnection.UserId == userId)
                .ToListAsync();

            var result = new List<EventDto>();

            foreach (var calendar in selectedCalendars)
            {
                foreach (var ev in calendar.Events)
                {
                    bool isAllDay = ev.StartInfo.Date.HasValue;
                    DateTime eventStart = ev.Start;
                    DateTime eventEnd = ev.End;

                    if (isAllDay)
                    {
                        eventStart = TimeZoneInfo.ConvertTimeToUtc(eventStart.AtMidnight(), targetZone);
                        eventEnd = TimeZoneInfo.ConvertTimeToUtc(eventEnd.AtMidnight(), targetZone);
                    }

                    if (ev.RecurrenceRules != null && ev.RecurrenceRules.Any())
                    {
                        var occurrences = ExpandRecurrence(ev, eventStart, eventEnd, start, end, targetZone);
                        foreach (var occ in occurrences)
                        {
                            var segments = SplitIntoDailySegments(occ.Start, occ.End, start, end, targetZone);
                            foreach (var seg in segments)
                            {
                                if (seg.Start < end && seg.End > start)
                                {
                                
                                    result.Add(new EventDto
                                    {
                                        Id = $"{ev.ProviderId}_{seg.Start:yyyyMMddHHmmss}",
                                        Title = ev.Title,
                                        Start = seg.Start,
                                        End = seg.End,
                                        Fc = calendar.ForegroundColor,
                                        Bc = calendar.BackgroundColor
                                    });
                                }
                            }
                        }
                    }
                    else
                    {
                        var segments = SplitIntoDailySegments(eventStart, eventEnd, start, end, targetZone);
                        foreach (var seg in segments)
                        {
                            if (seg.Start < end && seg.End > start)
                            {
                                result.Add(new EventDto
                                {
                                    Id = ev.ProviderId,
                                    Title = ev.Title,
                                    Start = seg.Start,
                                    End = seg.End,
                                    Fc = calendar.ForegroundColor,
                                    Bc = calendar.BackgroundColor
                                });
                            }
                        }
                    }
                }
            }

            var convertedResult = result.Select(e => new EventDto
            {
                Id = e.Id,
                Title = e.Title,
                Start = TimeZoneInfo.ConvertTimeFromUtc(e.Start, targetZone),
                End = TimeZoneInfo.ConvertTimeFromUtc(e.End, targetZone),
                Fc = e.Fc,
                Bc = e.Bc
            });

            return Ok(convertedResult);
        }
        private DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
        private DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
        private IEnumerable<(DateTime Start, DateTime End)> SplitIntoDailySegments(
    DateTime originalStart, DateTime originalEnd, DateTime weekStart, DateTime weekEnd, TimeZoneInfo zone)
        {
            var segments = new List<(DateTime, DateTime)>();
            
            if (originalStart.Date == originalEnd.Date)
            {
                segments.Add((originalStart, originalEnd));
                return segments;
            }

            originalStart = TimeZoneInfo.ConvertTimeFromUtc(originalStart, zone);
            originalEnd = TimeZoneInfo.ConvertTimeFromUtc(originalEnd, zone);
            weekStart = TimeZoneInfo.ConvertTimeFromUtc(weekStart, zone);
            weekEnd = TimeZoneInfo.ConvertTimeFromUtc(weekEnd, zone);


            DateTime dayStart = originalStart.Date;
            while (dayStart < originalEnd)
            {
                DateTime segmentStart = Max(originalStart, dayStart);    //když jde o 2 den, tak se nastaví čas na 0:00
                DateTime segmentEnd = Min(originalEnd, dayStart.AddDays(1));
                //DateTime clippedStart = Max(segmentStart, weekStart);
                //DateTime clippedEnd = Min(segmentEnd, weekEnd);

                if (segmentStart < segmentEnd)
                {
                    segments.Add((NastavUTC(segmentStart, zone), NastavUTC(segmentEnd, zone)));
                }


                dayStart = dayStart.AddDays(1);
            }

            return segments;
        }
        private DateTime NastavUTC(DateTime datum, TimeZoneInfo info)
        {
            return TimeZoneInfo.ConvertTimeToUtc(datum, info);
        }
        private DateTime NastavNaLocal(DateTime datum)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(datum, TimeZoneInfo.Local);
        }
        private List<(DateTime Start, DateTime End)> ExpandRecurrence(
    CalendarEvent ev, DateTime eventStart, DateTime eventEnd, DateTime rangeStart, DateTime rangeEnd, TimeZoneInfo zone)
        {
            var occurrences = new List<(DateTime, DateTime)>();
            if (ev.RecurrenceRules == null || !ev.RecurrenceRules.Any())
            {
                return occurrences;
            }

            var calendar = new Ical.Net.Calendar();
            var icalEvent = new Ical.Net.CalendarComponents.CalendarEvent
            {
                DtStart = new CalDateTime(TimeZoneInfo.ConvertTimeFromUtc(eventStart, zone), zone.ToString()),
                DtEnd = new CalDateTime(TimeZoneInfo.ConvertTimeFromUtc(eventEnd, zone), zone.ToString()),

            };


            foreach (var ruleString in ev.RecurrenceRules)
            {
                if (string.IsNullOrWhiteSpace(ruleString) || !ruleString.StartsWith("RRULE:"))
                    continue;

                var rruleString = ruleString.Substring(6);

                try
                {
                    var rrule = new RecurrencePattern(rruleString);
                    icalEvent.RecurrenceRules.Add(rrule);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to parse RRULE '{rruleString}': {ex.Message}");
                }
            }

            if (!icalEvent.RecurrenceRules.Any())
            {
                Debug.WriteLine("No valid RRULEs found after parsing.");
                return occurrences;
            }

            calendar.Events.Add(icalEvent);

            var calStart = new CalDateTime(TimeZoneInfo.ConvertTimeFromUtc(rangeStart, zone), zone.ToString());
            var calEnd = new CalDateTime(TimeZoneInfo.ConvertTimeFromUtc(rangeEnd, zone), zone.ToString());

            var options = new EvaluationOptions
            {
                MaxUnmatchedIncrementsLimit = 5000
            };

            try
            {
                var allOccurrences = calendar.GetOccurrences(calStart, options)
            .TakeWhile(occ => occ.Period.StartTime.Value <= rangeEnd);

                foreach (var occ in allOccurrences)
                {
                    var period = occ.Period;
                    DateTime start = period.StartTime.Value;
                    DateTime end = period.EndTime?.Value ?? period.EffectiveEndTime.Value;

                    if (start < rangeEnd && end > rangeStart)
                    {
                        occurrences.Add((TimeZoneInfo.ConvertTimeToUtc(start, zone), TimeZoneInfo.ConvertTimeToUtc(end, zone) ));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during GetOccurrences: {ex.Message}");
            }

            return occurrences;
        }

        // GET: api/<CalendarAPIController>
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/<CalendarAPIController>/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/<CalendarAPIController>
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        // PUT api/<CalendarAPIController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<CalendarAPIController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }

        [HttpPost("calendar/{calendarId}/color")]
        public async Task<IActionResult> UpdateCalendarColor(int calendarId, [FromBody] string color)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var calendar = await _context.Calendar
                .Include(c => c.CalendarConnection)
                .FirstOrDefaultAsync(c => c.Id == calendarId && c.CalendarConnection.UserId == userId);

            if (calendar == null)
                return NotFound();
            if (string.IsNullOrEmpty(color) || !color.StartsWith("#") || color.Length != 7)
                return BadRequest("Invalid color format. Expected #RRGGBB");
            calendar.BackgroundColor = color;

            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("EventsChanged", "Calendar data updated");
            return Ok();
        }

        public class EventDto
        {
            public string Id { get; set; }          //ProviderId or generated instance id
            public string Title { get; set; }
            public DateTime Start { get; set; }     //Local time
            public DateTime End { get; set; }       //Local time
            public string Fc { get; set; }      //Foreground color
            public string Bc { get; set; }       //Background color
        }
    }
}
