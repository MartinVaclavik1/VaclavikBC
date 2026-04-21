using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VaclavikBC.Models;

namespace VaclavikBC.Data
{
    public class VaclavikBCContext : IdentityDbContext
    {
        public VaclavikBCContext (DbContextOptions<VaclavikBCContext> options)
            : base(options)
        {
        }

        public DbSet<VaclavikBC.Models.Calendar> Calendar { get; set; } = default!;
        public DbSet<VaclavikBC.Models.CalendarEvent> CalendarEvent { get; set; } = default!;
        public DbSet<VaclavikBC.Models.CalendarConnection> CalendarConnection { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<CalendarEvent>(entity =>
            {
                entity.OwnsOne(e => e.StartInfo);
                entity.OwnsOne(e => e.EndInfo);
            });
        }
    }
}
