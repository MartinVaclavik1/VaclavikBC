using Ical.Net.DataTypes;
using Ical.Net.Evaluation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using VaclavikBC.Data;
using VaclavikBC.Models;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace VaclavikBC.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CalendarAPIController : ControllerBase
    {
        private readonly VaclavikBCContext _context;
        public CalendarAPIController(VaclavikBCContext context)
        {
            _context = context;
        }

        [HttpGet("events")]
        public async Task<IActionResult> GetEvents(
       [FromQuery] DateTime start,   
       [FromQuery] DateTime end)
        {
            // Query all selected calendars with their events
            var selectedCalendars = await _context.Calendar
                .Include(c => c.Events)
                .Where(c => c.Selected)
                .ToListAsync();

            var result = new List<EventDto>();

            foreach (var calendar in selectedCalendars)
            {
                foreach (var ev in calendar.Events)
                {
                    // Handle all-day events
                    bool isAllDay = ev.StartInfo.Date.HasValue;
                    DateTime eventStart = ev.Start;
                    DateTime eventEnd = ev.End;

                    // If the event has recurrence rules, expand them
                    if (ev.RecurrenceRules != null && ev.RecurrenceRules.Any())
                    {
                        var occurrences = ExpandRecurrence(ev, start, end);
                        foreach (var occ in occurrences)
                        {
                            // Only add if occurrence overlaps the requested week
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

            // Build the iCal event
            var calendar = new Ical.Net.Calendar();
            var icalEvent = new Ical.Net.CalendarComponents.CalendarEvent
            {
                DtStart = new CalDateTime(ev.Start.ToUniversalTime()),
                DtEnd = new CalDateTime(ev.End.ToUniversalTime()),
                
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
                    // Skip invalid rules
                }
            }

            if (!icalEvent.RecurrenceRules.Any())
            {
                Debug.WriteLine("No valid RRULEs found after parsing.");
                return occurrences;
            }

            calendar.Events.Add(icalEvent);

            // Convert range to CalDateTime with the same time zone as the event
            var calStart = new CalDateTime(rangeStart.ToUniversalTime());
            var calEnd = new CalDateTime(rangeEnd.ToUniversalTime());

            var options = new EvaluationOptions
            {
                MaxUnmatchedIncrementsLimit = 5000 // generous limit for weekly rules
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

                    // Only include if overlaps with the requested range
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
