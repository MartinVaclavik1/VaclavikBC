using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VaclavikBC.Data;
using VaclavikBC.Enums;
using VaclavikBC.Hubs;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace VaclavikBC.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CalendarConnectionsAPIController : ControllerBase
    {

        private readonly VaclavikBCContext _context;
        private readonly IHubContext<CalendarSyncHub> _hubContext;
        private readonly GoogleController _googleController;
        private readonly MicrosoftController _microsoftController;
        private readonly CalendlyController _calendlyController;

        public CalendarConnectionsAPIController(VaclavikBCContext context,
            IHubContext<CalendarSyncHub> hubContext, GoogleController googleController, MicrosoftController microsoftController,
            CalendlyController calendlyController)
        {
            _context = context;
            _hubContext = hubContext;
            _googleController = googleController;
            _microsoftController = microsoftController;
            _calendlyController = calendlyController;

        }

        [HttpGet]
        public async Task<IActionResult> GetUserConnections()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var connections = await _context.CalendarConnection
                .Include(c => c.Calendars)
                .Where(c => c.UserId == userId)
                .ToListAsync();
            return Ok(connections);
        }

        [HttpPost("{id}/refresh")]
        public async Task<IActionResult> RefreshConnection(int id)
        {
            var conn = await _context.CalendarConnection.FindAsync(id);
            if (conn == null)
                return NotFound("No Calendar connection found.");
            
            bool successful;
            switch (conn.Provider)
            {   
                case nameof(Providers.Google):
                    successful = await _googleController.RefreshConnectionAsync(conn);
                    if (!successful)
                    {
                        return Unauthorized();
                    }
                    break;
                case nameof(Providers.Microsoft):
                    
                    successful = await _microsoftController.RefreshConnectionAsync(conn);
                    if (!successful)
                    {
                        return Unauthorized();
                    }
                    break;
                case nameof(Providers.Calendly):
                    successful = await _calendlyController.RefreshConnectionAsync(conn);
                    if (!successful)
                    {
                        return Unauthorized();
                    }
                    break;
                default:
                    return NotFound();
                    break;
            }

            await _hubContext.Clients.All.SendAsync("ConnectionChanged", "Calendar data updated");  //znovu načteme kalendářové připojení a události
            await _hubContext.Clients.All.SendAsync("EventsChanged", "Calendar data updated");
            return Ok();
        }

        [HttpPost("{id}/delete")]
        public async Task<IActionResult> DeleteConnection(int id)
        {
            var conn = await _context.CalendarConnection.FindAsync(id);
            if (conn != null)
            {
                _context.CalendarConnection.Remove(conn);
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ConnectionChanged", "Calendar deleted");
                await _hubContext.Clients.All.SendAsync("EventsChanged", "Calendar deleted");
            }
            return Ok();
        }

        [HttpPost("calendar/{calendarId}/toggle")]
        public async Task<IActionResult> ToggleCalendarSelection(int calendarId, [FromBody] bool selected)
        {
            var calendar = await _context.Calendar.FindAsync(calendarId);
            if (calendar == null)
                return NotFound($"Calendar with ID {calendarId} not found.");

            calendar.Selected = selected;
            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("EventsChanged", "Calendar data updated");
            return Ok(new { calendarId, selected });
        }

    }

}
