using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VaclavikBC.Data;
using VaclavikBC.Models;

namespace VaclavikBC.Controllers
{
    public class CalendarConnectionsController : Controller
    {
        private readonly VaclavikBCContext _context;

        public CalendarConnectionsController(VaclavikBCContext context)
        {
            _context = context;
        }

        // GET: CalendarConnections
        public async Task<IActionResult> Index()
        {
            return View(await _context.CalendarConnection.ToListAsync());
        }

        // GET: CalendarConnections/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var calendarConnection = await _context.CalendarConnection
                .FirstOrDefaultAsync(m => m.Id == id);
            if (calendarConnection == null)
            {
                return NotFound();
            }

            return View(calendarConnection);
        }

        // GET: CalendarConnections/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: CalendarConnections/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,UserId,Email,Provider,AccessToken,RefreshToken,NextSyncToken,ExpirationTime")] CalendarConnection calendarConnection)
        {
            if (ModelState.IsValid)
            {
                _context.Add(calendarConnection);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(calendarConnection);
        }

        // GET: CalendarConnections/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var calendarConnection = await _context.CalendarConnection.FindAsync(id);
            if (calendarConnection == null)
            {
                return NotFound();
            }
            return View(calendarConnection);
        }

        // POST: CalendarConnections/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,UserId,Email,Provider,AccessToken,RefreshToken,NextSyncToken,ExpirationTime")] CalendarConnection calendarConnection)
        {
            if (id != calendarConnection.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(calendarConnection);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CalendarConnectionExists(calendarConnection.Id))
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
            return View(calendarConnection);
        }

        // GET: CalendarConnections/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var calendarConnection = await _context.CalendarConnection
                .FirstOrDefaultAsync(m => m.Id == id);
            if (calendarConnection == null)
            {
                return NotFound();
            }

            return View(calendarConnection);
        }

        // POST: CalendarConnections/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var calendarConnection = await _context.CalendarConnection.FindAsync(id);
            if (calendarConnection != null)
            {
                _context.CalendarConnection.Remove(calendarConnection);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CalendarConnectionExists(int id)
        {
            return _context.CalendarConnection.Any(e => e.Id == id);
        }
    }
}
