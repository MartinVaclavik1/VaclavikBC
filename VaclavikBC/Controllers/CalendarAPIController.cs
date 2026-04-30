using Ical.Net.DataTypes;
using Ical.Net.Evaluation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;
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
       [FromQuery] DateTime end)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

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

                    if (ev.RecurrenceRules != null && ev.RecurrenceRules.Any())
                    {
                        var occurrences = ExpandRecurrence(ev, start, end);
                        foreach (var occ in occurrences)
                        {
                            if (occ.Start < end && occ.End > start)
                            {
                                result.Add(new EventDto
                                {
                                    Id = $"{ev.ProviderId}_{occ.Start:yyyyMMddHHmmss}",
                                    Title = ev.Title,
                                    Start = occ.Start,
                                    End = occ.End,
                                    Fc = calendar.ForegroundColor,
                                    Bc = calendar.BackgroundColor
                                });
                            }
                        }
                    }
                    else
                    {
                        //kontrola, jestli je v aktuálním týdnu
                        if (eventStart < end && eventEnd > start)
                        {
                            result.Add(new EventDto
                            {
                                Id = ev.ProviderId,
                                Title = ev.Title,
                                Start = eventStart,
                                End = eventEnd,
                                Fc = calendar.ForegroundColor,
                                Bc = calendar.BackgroundColor
                            });
                        }
                    }
                }
            }

            return Ok(result);
        }

        private List<(DateTime Start, DateTime End)> ExpandRecurrence(
    CalendarEvent ev, DateTime rangeStart, DateTime rangeEnd)
        {
            var occurrences = new List<(DateTime, DateTime)>();

            if (ev.RecurrenceRules == null || !ev.RecurrenceRules.Any())
            {
                Debug.WriteLine($"No recurrence rules for event {ev.Title}");
                return occurrences;
            }

            Debug.WriteLine($"Expanding recurrence for '{ev.Title}' between {rangeStart} and {rangeEnd}");
            Debug.WriteLine($"Base event Start: {ev.Start}, End: {ev.End}");

            var calendar = new Ical.Net.Calendar();
            var icalEvent = new Ical.Net.CalendarComponents.CalendarEvent
            {
                DtStart = new CalDateTime(ev.Start, "Europe/Prague"),
                DtEnd = new CalDateTime(ev.End, "Europe/Prague"),

            };


            foreach (var ruleString in ev.RecurrenceRules)
            {
                Debug.WriteLine($"Raw rule: '{ruleString}'");
                if (string.IsNullOrWhiteSpace(ruleString) || !ruleString.StartsWith("RRULE:"))
                    continue;

                var rruleString = ruleString.Substring(6); // remove "RRULE:"
                Debug.WriteLine($"Parsed RRULE: '{rruleString}'");

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

            var calStart = new CalDateTime(rangeStart, "Europe/Prague");
            var calEnd = new CalDateTime(rangeEnd, "Europe/Prague");

            var options = new EvaluationOptions
            {
                MaxUnmatchedIncrementsLimit = 5000
            };

            try
            {
                var allOccurrences = calendar.GetOccurrences(calStart, options)
            .TakeWhile(occ => occ.Period.StartTime.Value <= rangeEnd);
                Debug.WriteLine($"GetOccurrences returned {allOccurrences.Count()} total occurrences before filtering.");

                foreach (var occ in allOccurrences)
                {
                    var period = occ.Period;
                    DateTime start = period.StartTime.Value;
                    DateTime end = period.EndTime?.Value ?? period.EffectiveEndTime.Value;

                    if (start < rangeEnd && end > rangeStart)
                    {
                        occurrences.Add((start, end));
                        Debug.WriteLine($"Added occurrence: {start} - {end}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during GetOccurrences: {ex.Message}");
            }

            Debug.WriteLine($"Returning {occurrences.Count} occurrences within range.");
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
