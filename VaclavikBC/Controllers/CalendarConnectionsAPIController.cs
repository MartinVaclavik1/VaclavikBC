using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VaclavikBC.Data;
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
        public CalendarConnectionsAPIController(VaclavikBCContext context,
            IHubContext<CalendarSyncHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetUserConnections()
        {
            // Assuming you have a way to get current user ID (e.g., from HttpContext.User)
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var connections = await _context.CalendarConnection
                .Include(c => c.Calendars)
                .ToListAsync();
            return Ok(connections);
        }

        [HttpPost("{id}/refresh")]
        public async Task<IActionResult> RefreshConnection(int id)
        {
            //TODO vytvořit všechny notimplemented
            // Trigger sync for this connection (call your sync service)
            throw new NotImplementedException();
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
                await _hubContext.Clients.All.SendAsync("ConnectionCreated", "Calendar data updated");
            }
            return Ok();
        }

        [HttpPost("calendar/{calendarId}/refresh")]
        public async Task<IActionResult> RefreshCalendar(int calendarId)
        {
            // Sync only that calendar
            throw new NotImplementedException();
            return Ok();
        }

        [HttpPost("calendar/{calendarId}/toggle")]
        public async Task<IActionResult> ToggleCalendarSelection(int calendarId, [FromBody] bool selected)
        {
            // Update calendar's Selected property in DB
            throw new NotImplementedException();
            return Ok();
        }
    
    }

}
