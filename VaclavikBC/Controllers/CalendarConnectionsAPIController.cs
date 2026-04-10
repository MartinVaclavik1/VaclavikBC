using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VaclavikBC.Data;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace VaclavikBC.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CalendarConnectionsAPIController : ControllerBase
    {

        private readonly VaclavikBCContext _context;
        public CalendarConnectionsAPIController(VaclavikBCContext context) => _context = context;

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
            // Trigger sync for this connection (call your sync service)
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
            }
            return Ok();
        }

        [HttpPost("calendar/{calendarId}/refresh")]
        public async Task<IActionResult> RefreshCalendar(int calendarId)
        {
            // Sync only that calendar
            return Ok();
        }

        [HttpPost("calendar/{calendarId}/toggle")]
        public async Task<IActionResult> ToggleCalendarSelection(int calendarId, [FromBody] bool selected)
        {
            // Update calendar's Selected property in DB
            return Ok();
        }
    
    }

}
