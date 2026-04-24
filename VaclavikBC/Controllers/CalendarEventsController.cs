using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VaclavikBC.Data;
using VaclavikBC.Models;

namespace VaclavikBC.Controllers
{
    public class CalendarEventsController : Controller
    {
        private readonly VaclavikBCContext _context;

        public CalendarEventsController(VaclavikBCContext context)
        {
            _context = context;
        }

        // GET: CalendarEvents
        public async Task<IActionResult> Index()
        {
            var vaclavikBCContext = _context.CalendarEvent.Include(c => c.Calendar);
            return View(await vaclavikBCContext.ToListAsync());
        }

        // GET: CalendarEvents/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var calendarEvent = await _context.CalendarEvent
                .Include(c => c.Calendar)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (calendarEvent == null)
            {
                return NotFound();
            }

            return View(calendarEvent);
        }

        // GET: CalendarEvents/Create
        public IActionResult Create()
        {
            ViewData["CalendarId"] = new SelectList(_context.Calendar, "Id", "Id");
            return View();
        }

        // POST: CalendarEvents/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Title,TimeZone,Start,End,CalendarId,RRULE")] CalendarEvent calendarEvent)
        {
            if (ModelState.IsValid)
            {
                _context.Add(calendarEvent);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["CalendarId"] = new SelectList(_context.Calendar, "Id", "Id", calendarEvent.CalendarId);
            return View(calendarEvent);
        }

        // GET: CalendarEvents/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var calendarEvent = await _context.CalendarEvent.FindAsync(id);
            if (calendarEvent == null)
            {
                return NotFound();
            }
            ViewData["CalendarId"] = new SelectList(_context.Calendar, "Id", "Id", calendarEvent.CalendarId);
            return View(calendarEvent);
        }

        // POST: CalendarEvents/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,TimeZone,Start,End,CalendarId,RRULE")] CalendarEvent calendarEvent)
        {
            if (id != calendarEvent.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(calendarEvent);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CalendarEventExists(calendarEvent.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["CalendarId"] = new SelectList(_context.Calendar, "Id", "Id", calendarEvent.CalendarId);
            return View(calendarEvent);
        }

        // GET: CalendarEvents/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var calendarEvent = await _context.CalendarEvent
                .Include(c => c.Calendar)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (calendarEvent == null)
            {
                return NotFound();
            }

            return View(calendarEvent);
        }

        // POST: CalendarEvents/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var calendarEvent = await _context.CalendarEvent.FindAsync(id);
            if (calendarEvent != null)
            {
                _context.CalendarEvent.Remove(calendarEvent);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CalendarEventExists(int id)
        {
            return _context.CalendarEvent.Any(e => e.Id == id);
        }
    }
}
