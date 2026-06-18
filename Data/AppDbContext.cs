using BiometricAttendanceSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace BiometricAttendanceSystem.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<Teacher>          Teachers          { get; set; }
        public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
        public DbSet<Department>       Departments       { get; set; }
        public DbSet<Holiday>          Holidays          { get; set; }
        public DbSet<SchoolSettings>              SchoolSettings              { get; set; }
        public DbSet<DeviceReregistrationRequest> DeviceReregistrationRequests { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // One attendance record per teacher per day
            builder.Entity<AttendanceRecord>()
                .HasIndex(a => new { a.TeacherId, a.Date })
                .IsUnique();

            // National ID must be unique
            builder.Entity<Teacher>()
                .HasIndex(t => t.NationalId)
                .IsUnique();

            // Seed the four departments from the proposal
            builder.Entity<Department>().HasData(
                new Department { Id = 1, Name = "Mathematics" },
                new Department { Id = 2, Name = "Humanities" },
                new Department { Id = 3, Name = "Languages" },
                new Department { Id = 4, Name = "Sciences" }
            );

            builder.Entity<Holiday>().HasData(
                new Holiday
                {
                    Id = 1,
                    Name = "Christmas Holiday",
                    StartDate = new DateTime(2026, 12, 15),
                    EndDate = new DateTime(2026, 12, 31),
                    Description = "Christmas break",
                    CreatedDate = new DateTime(2026, 1, 1)
                },
                new Holiday
                {
                    Id = 2,
                    Name = "School Closure - Maintenance",
                    StartDate = new DateTime(2026, 3, 20),
                    EndDate = new DateTime(2026, 3, 27),
                    Description = "Building maintenance",
                    CreatedDate = new DateTime(2026, 1, 1)
                }
            );

            builder.Entity<SchoolSettings>().HasData(
                new SchoolSettings
                {
                    Id = 1,
                    SchoolName = "K.P. Senior Secondary School",
                    ExpectedCheckInTime = new TimeSpan(9, 0, 0),
                    LateArrivalTime = new TimeSpan(10, 0, 0),
                    AllowEarlyCheckIn = true,
                    AllowLateCheckIn = true,
                    RetentionDays = 365,
                    LastUpdated = new DateTime(2026, 1, 1)
                }
            );
        }
    }
}
